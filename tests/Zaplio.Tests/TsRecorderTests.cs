using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class TsRecorderTests
{
    private const int VideoPid = 0x100;
    private const int AudioPid = 0x101;
    private const int PmtPid = 0x1000;

    private static byte[] Packet(int pid, bool unitStart, bool keyframe = false, byte fill = 0xAA)
    {
        var packet = new byte[TsRecorder.PacketSize];
        Array.Fill(packet, fill);
        packet[0] = 0x47;
        packet[1] = (byte)((unitStart ? 0x40 : 0) | ((pid >> 8) & 0x1F));
        packet[2] = (byte)pid;
        if (keyframe)
        {
            packet[3] = 0x30; // adaptation field and payload
            packet[4] = 1;
            packet[5] = 0x40; // random access
        }
        else packet[3] = 0x10;
        return packet;
    }

    private static byte[] Pat()
    {
        var packet = Packet(0, unitStart: true, fill: 0xFF);
        byte[] section = [0x00, 0x00, 0xB0, 0x0D, 0x00, 0x01, 0xC1, 0x00, 0x00, 0x00, 0x01, (byte)(0xE0 | (PmtPid >> 8)), (byte)(PmtPid & 0xFF), 0, 0, 0, 0];
        section.CopyTo(packet, 4); // pointer field 0, then the table
        return packet;
    }

    private static byte[] Pmt()
    {
        var packet = Packet(PmtPid, unitStart: true, fill: 0xFF);
        byte[] section =
        [
            0x00, 0x02, 0xB0, 0x17, 0x00, 0x01, 0xC1, 0x00, 0x00, (byte)(0xE0 | (VideoPid >> 8)), (byte)(VideoPid & 0xFF), 0xF0, 0x00,
            0x1B, (byte)(0xE0 | (VideoPid >> 8)), (byte)(VideoPid & 0xFF), 0xF0, 0x00,
            0x0F, (byte)(0xE0 | (AudioPid >> 8)), (byte)(AudioPid & 0xFF), 0xF0, 0x00,
            0, 0, 0, 0
        ];
        section.CopyTo(packet, 4);
        return packet;
    }

    private static List<int> Pids(byte[] file) =>
        Enumerable.Range(0, file.Length / TsRecorder.PacketSize)
            .Select(index => ((file[index * 188 + 1] & 0x1F) << 8) | file[index * 188 + 2]).ToList();

    [Fact]
    public void FindsTheVideoStreamInTheProgramTables()
    {
        using var recorder = new TsRecorder();
        recorder.Feed(Pat());
        recorder.Feed(Pmt());
        Assert.Equal(VideoPid, recorder.VideoPid);
    }

    [Fact]
    public void NothingIsKeptWhileNotRecording()
    {
        using var recorder = new TsRecorder();
        for (var index = 0; index < 1000; index++) recorder.Feed(Packet(VideoPid, false));
        Assert.Equal(0, recorder.WrittenBytes);
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void ARecordingBeginsWithTheTablesAndTheNextKeyframe()
    {
        using var recorder = new TsRecorder();
        recorder.Feed([.. Pat(), .. Pmt(), .. Packet(VideoPid, true), .. Packet(AudioPid, true)]);
        var file = new MemoryStream();
        recorder.Start(new NonClosing(file));
        recorder.Feed([.. Packet(VideoPid, true), .. Packet(AudioPid, true)]); // not a keyframe yet: skipped
        recorder.Feed([.. Packet(VideoPid, true, keyframe: true), .. Packet(AudioPid, true), .. Packet(VideoPid, false)]);
        var written = recorder.Stop();
        recorder.Feed(Packet(VideoPid, true, keyframe: true)); // after stopping: ignored

        Assert.Equal([0, PmtPid, VideoPid, AudioPid, VideoPid], Pids(file.ToArray()));
        Assert.Equal(5 * 188, written);
        Assert.True(TsRecorder.RandomAccess(file.ToArray().AsSpan(2 * 188, 188)));
    }

    [Fact]
    public void PacketsSplitAcrossChunksAndLeadingNoiseAreHandled()
    {
        using var recorder = new TsRecorder();
        var file = new MemoryStream();
        byte[] stream = [0x00, 0x12, .. Pat(), .. Pmt(), .. Packet(VideoPid, true, keyframe: true), .. Packet(AudioPid, true)];
        recorder.Start(new NonClosing(file));
        foreach (var chunk in stream.Chunk(61)) recorder.Feed(chunk); // odd chunk size: packets arrive in pieces
        recorder.Stop();

        Assert.Equal([0, PmtPid, VideoPid, AudioPid], Pids(file.ToArray()));
    }

    [Fact]
    public void AReconnectContinuesTheSameFileFromTheNewStreamsKeyframe()
    {
        using var recorder = new TsRecorder();
        var file = new MemoryStream();
        recorder.Start(new NonClosing(file));
        recorder.Feed([.. Pat(), .. Pmt(), .. Packet(VideoPid, true, keyframe: true)]);
        recorder.NewStream();
        recorder.Feed([.. Packet(VideoPid, false), .. Pat(), .. Pmt(), .. Packet(VideoPid, true, keyframe: true)]);
        recorder.Stop();

        Assert.Equal([0, PmtPid, VideoPid, 0, PmtPid, VideoPid], Pids(file.ToArray()));
    }

    [Fact]
    public void WithoutAKeyframeMarkItStartsAnyway()
    {
        using var recorder = new TsRecorder();
        recorder.Feed([.. Pat(), .. Pmt()]);
        var file = new MemoryStream();
        recorder.Start(new NonClosing(file));
        for (var index = 0; index < 4 * 1024 * 1024 / 188 + 10; index++) recorder.Feed(Packet(VideoPid, false));
        Assert.True(recorder.Stop() > 0);
    }

    /// <summary>Keeps the memory stream readable after the recorder disposes it.</summary>
    private sealed class NonClosing(MemoryStream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
    }
}
