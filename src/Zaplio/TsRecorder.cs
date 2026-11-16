using System.IO;

namespace Zaplio;

/// <summary>
/// Records from an MPEG-TS stream that is already flowing, so a provider's stream never has to restart for a recording.
/// It reads the stream continuously and keeps only what it needs to begin a clean file at any moment: the latest program
/// tables (PAT and PMT) and which PID carries the video. While not recording, every byte is dropped (no disk, almost no CPU).
/// <see cref="Start"/> opens the file and begins at the next video keyframe, after the program tables, so the file plays from
/// its first frame. A new stream (a live reconnect) can keep feeding the same recording.
/// Thread-safe: the stream is fed on a background thread while the window starts and stops recordings.
/// </summary>
internal sealed class TsRecorder : IDisposable
{
    internal const int PacketSize = 188;
    private const byte Sync = 0x47;
    private const long KeyframeWaitBytes = 2 * 1024 * 1024; // start anyway if no keyframe is recognised in this much stream

    private readonly object _gate = new();
    private readonly byte[] _partial = new byte[PacketSize];
    private int _partialLength;
    private byte[]? _pat;
    private byte[]? _pmt;
    private int _pmtPid = -1;
    private int _videoPid = -1;
    private byte _videoType;
    private Stream? _file;
    private bool _waitingForKeyframe;
    private long _waitedBytes;

    internal long WrittenBytes { get; private set; }
    internal bool IsRecording { get { lock (_gate) return _file is not null; } }
    internal int VideoPid => _videoPid;

    /// <summary>Begins writing into <paramref name="file"/> at the next keyframe. The recorder owns the stream until <see cref="Stop"/>.</summary>
    internal void Start(Stream file)
    {
        lock (_gate)
        {
            _file?.Dispose();
            _file = file;
            _waitingForKeyframe = true;
            _waitedBytes = 0;
            WrittenBytes = 0;
        }
    }

    /// <summary>Stops writing and closes the file. Returns how many bytes were recorded.</summary>
    internal long Stop()
    {
        lock (_gate)
        {
            var written = WrittenBytes;
            _file?.Flush();
            _file?.Dispose();
            _file = null;
            return written;
        }
    }

    /// <summary>A new stream begins (another restart of the source): forget the old tables and realign.</summary>
    internal void NewStream()
    {
        lock (_gate)
        {
            _partialLength = 0;
            _pat = _pmt = null;
            _pmtPid = _videoPid = -1;
            if (_file is not null)
            {
                _waitingForKeyframe = true; // continue the same recording from the new stream's first keyframe
                _waitedBytes = 0;
            }
        }
    }

    /// <summary>Feeds the next bytes of the stream, in any chunk size.</summary>
    internal void Feed(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            while (data.Length > 0)
            {
                if (_partialLength == 0)
                {
                    // Resynchronise on a sync byte; whole packets are handled straight from the input.
                    var sync = data.IndexOf(Sync);
                    if (sync < 0) return;
                    data = data[sync..];
                    while (data.Length >= PacketSize && data[0] == Sync)
                    {
                        Packet(data[..PacketSize]);
                        data = data[PacketSize..];
                    }
                    if (data.Length == 0) return;
                    if (data[0] != Sync) continue;
                }
                var take = Math.Min(PacketSize - _partialLength, data.Length);
                data[..take].CopyTo(_partial.AsSpan(_partialLength));
                _partialLength += take;
                data = data[take..];
                if (_partialLength < PacketSize) return;
                _partialLength = 0;
                if (_partial[0] == Sync) Packet(_partial);
            }
        }
    }

    private void Packet(ReadOnlySpan<byte> packet)
    {
        var pid = ((packet[1] & 0x1F) << 8) | packet[2];
        var unitStart = (packet[1] & 0x40) != 0;
        if (pid == 0 && unitStart)
        {
            _pat = packet.ToArray();
            ReadPat(packet);
        }
        else if (pid == _pmtPid && unitStart)
        {
            _pmt = packet.ToArray();
            ReadPmt(packet);
        }

        if (_file is null) return;
        if (_waitingForKeyframe)
        {
            _waitedBytes += PacketSize;
            // VLC's own TS writer sets no random-access mark, so the frame type inside the video is read as well.
            var keyframe = pid == _videoPid && unitStart && (RandomAccess(packet) || StartsKeyframe(packet, _videoType));
            var noVideo = _pmt is not null && _videoPid < 0; // audio-only: any packet will do
            if (!keyframe && !noVideo && _waitedBytes < KeyframeWaitBytes) return;
            if (_pat is null || _pmt is null) return; // a file cannot begin without its program tables
            _waitingForKeyframe = false;
            Write(_pat);
            Write(_pmt);
            if (pid is 0 || pid == _pmtPid) return;
        }
        Write(packet);
    }

    private void Write(ReadOnlySpan<byte> packet)
    {
        _file!.Write(packet);
        WrittenBytes += PacketSize;
    }

    /// <summary>The random-access (keyframe) mark in the adaptation field.</summary>
    internal static bool RandomAccess(ReadOnlySpan<byte> packet)
    {
        var adaptation = (packet[3] >> 4) & 0x3;
        return adaptation is 2 or 3 && packet[4] > 0 && (packet[5] & 0x40) != 0;
    }

    /// <summary>
    /// Whether the video frame starting in this packet can be decoded on its own: an H.264 IDR or SPS, an H.265 IRAP or
    /// parameter set, an MPEG-1/2 sequence or GOP header. Other video types count every frame start (good enough to begin).
    /// </summary>
    internal static bool StartsKeyframe(ReadOnlySpan<byte> packet, byte streamType)
    {
        var offset = 4;
        var adaptation = (packet[3] >> 4) & 0x3;
        if (adaptation is 2 or 3) offset += 1 + packet[4];
        if (adaptation is 0 or 2 || offset + 9 > PacketSize) return false;
        var payload = packet[offset..];
        if (payload[0] != 0 || payload[1] != 0 || payload[2] != 1) return false; // not a PES start
        var es = 9 + payload[8];
        if (es >= payload.Length) return false;
        if (streamType is not (0x01 or 0x02 or 0x1B or 0x24)) return true;
        var data = payload[es..];
        for (var index = 0; index + 3 < data.Length; index++)
        {
            if (data[index] != 0 || data[index + 1] != 0 || data[index + 2] != 1) continue;
            var code = data[index + 3];
            var found = streamType switch
            {
                0x1B => (code & 0x1F) is 5 or 7,                         // H.264: IDR slice, sequence parameter set
                0x24 => ((code >> 1) & 0x3F) is (>= 16 and <= 21) or (>= 32 and <= 34), // H.265: IRAP, VPS/SPS/PPS
                _ => code is 0xB3 or 0xB8                                // MPEG-1/2: sequence header, GOP
            };
            if (found) return true;
        }
        return false;
    }

    private static ReadOnlySpan<byte> Section(ReadOnlySpan<byte> packet)
    {
        var offset = 4;
        var adaptation = (packet[3] >> 4) & 0x3;
        if (adaptation is 2 or 3) offset += 1 + packet[4];
        if (adaptation is 0 or 2 || offset >= PacketSize) return [];
        offset += 1 + packet[offset]; // pointer field
        return offset < PacketSize ? packet[offset..] : [];
    }

    private void ReadPat(ReadOnlySpan<byte> packet)
    {
        var section = Section(packet);
        if (section.Length < 12 || section[0] != 0x00) return;
        var length = Math.Min(((section[1] & 0x0F) << 8) | section[2], section.Length - 3);
        for (var entry = 8; entry + 4 <= 3 + length - 4; entry += 4)
        {
            var program = (section[entry] << 8) | section[entry + 1];
            if (program == 0) continue; // network PID
            _pmtPid = ((section[entry + 2] & 0x1F) << 8) | section[entry + 3];
            return;
        }
    }

    private void ReadPmt(ReadOnlySpan<byte> packet)
    {
        var section = Section(packet);
        if (section.Length < 12 || section[0] != 0x02) return;
        var length = Math.Min(((section[1] & 0x0F) << 8) | section[2], section.Length - 3);
        var end = 3 + length - 4; // before the CRC
        var position = 12 + (((section[10] & 0x0F) << 8) | section[11]);
        _videoPid = -1;
        while (position + 5 <= end)
        {
            var type = section[position];
            var pid = ((section[position + 1] & 0x1F) << 8) | section[position + 2];
            var info = ((section[position + 3] & 0x0F) << 8) | section[position + 4];
            if (_videoPid < 0 && IsVideo(type))
            {
                _videoPid = pid;
                _videoType = type;
            }
            position += 5 + info;
        }
    }

    /// <summary>MPEG-1/2, MPEG-4 part 2, H.264, H.265, VC-1, AVS and AV1 private types used in transport streams.</summary>
    private static bool IsVideo(byte type) => type is 0x01 or 0x02 or 0x10 or 0x1B or 0x24 or 0x42 or 0xD1 or 0xEA;

    public void Dispose() => Stop();
}
