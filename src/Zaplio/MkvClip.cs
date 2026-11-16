using System.Buffers.Binary;
using System.IO;

namespace Zaplio;

/// <summary>
/// Tidies a Matroska clip copied out of a longer video, in place and without touching the audio or video data:
/// <list type="bullet">
/// <item>its timestamps begin at zero (a copy keeps the original ones, so a clip from 8:45 looked like 8 minutes of nothing);</item>
/// <item>clusters past the wanted length are cut off (VLC's stop time is not always honoured on large files);</item>
/// <item>the segment size, the duration and the index entries that pointed past the new end are fixed.</item>
/// </list>
/// Every rewritten number keeps its original byte width, so nothing moves inside the file.
/// </summary>
internal static class MkvClip
{
    private const uint EbmlHeader = 0x1A45DFA3;
    private const uint Segment = 0x18538067;
    private const uint SeekHead = 0x114D9B74;
    private const uint Seek = 0x4DBB;
    private const uint SeekPosition = 0x53AC;
    private const uint Info = 0x1549A966;
    private const uint TimestampScale = 0x2AD7B1;
    private const uint Duration = 0x4489;
    private const uint Cluster = 0x1F43B675;
    private const uint ClusterTimestamp = 0xE7;
    private const byte Void = 0xEC;

    /// <summary>Rebases and trims <paramref name="path"/> to at most <paramref name="keepMs"/> milliseconds. False if it is not a Matroska file it can handle.</summary>
    internal static bool Trim(string path, long keepMs)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        return Trim(file, keepMs);
    }

    internal static bool Trim(Stream file, long keepMs)
    {
        var length = file.Length;
        var header = Element.Read(file, 0);
        if (header is not { Id: EbmlHeader, Size: { } headerSize }) return false;
        var segment = Element.Read(file, header.DataStart + headerSize);
        if (segment is not { Id: Segment }) return false;

        long scale = 1_000_000;
        (long Position, int Width)? duration = null;
        var seeks = new List<(long Start, long Length, long Target)>();
        var clusters = new List<(long Start, long End, long Stamp, long StampAt, int StampWidth)>();
        var cut = length;
        var position = segment.DataStart;
        var segmentEnd = segment.Size is { } segmentSize ? Math.Min(length, segment.DataStart + segmentSize) : length;

        // First pass: read the layout without changing anything.
        while (position < segmentEnd)
        {
            var element = Element.Read(file, position);
            if (element is null) break;
            if (element.Size is not { } size)
            {
                if (element.Id == Cluster) return false; // a live-written cluster without a size: not produced by our copies
                break;
            }
            var end = element.DataStart + size;
            switch (element.Id)
            {
                case Info:
                    foreach (var child in Children(file, element.DataStart, end))
                    {
                        if (child.Id == TimestampScale && child.Size is { } width) scale = (long)ReadUInt(file, child.DataStart, (int)width);
                        if (child.Id == Duration && child.Size is 4 or 8) duration = (child.DataStart, (int)child.Size!.Value);
                    }
                    break;
                case SeekHead:
                    foreach (var seek in Children(file, element.DataStart, end).Where(child => child.Id == Seek && child.Size is not null))
                    foreach (var field in Children(file, seek.DataStart, seek.DataStart + seek.Size!.Value))
                        if (field.Id == SeekPosition && field.Size is { } width)
                            seeks.Add((seek.Start, seek.DataStart + seek.Size.Value - seek.Start, (long)ReadUInt(file, field.DataStart, (int)width)));
                    break;
                case Cluster:
                    // The timestamp is not always the first child (a CRC-32 may come first).
                    var stamp = Children(file, element.DataStart, end).FirstOrDefault(child => child.Id == ClusterTimestamp);
                    if (stamp?.Size is not { } stampWidth) return false;
                    clusters.Add((element.Start, end, (long)ReadUInt(file, stamp.DataStart, (int)stampWidth), stamp.DataStart, (int)stampWidth));
                    break;
                default:
                    if (clusters.Count > 0) // index (cues), tags and the like after the clusters describe the uncut file
                    {
                        cut = element.Start;
                        position = segmentEnd;
                        continue;
                    }
                    break;
            }
            position = end;
        }
        if (clusters.Count == 0) return false;

        // A copy that starts mid-video may begin with a stray cluster stamped 0 before the real content (seen with VLC 3 on
        // VP9); taking it as the start made the rest look minutes long, or got it all cut. Such leading clusters become padding.
        var gap = 10_000L * 1_000_000 / scale;
        var skip = 0;
        while (skip + 1 < clusters.Count && clusters[skip + 1].Stamp - clusters[skip].Stamp > gap) skip++;
        var origin = clusters[skip].Stamp;
        var keep = keepMs * 1_000_000 / scale;

        // Second pass: rewrite in place.
        var lastKept = -1;
        for (var index = 0; index < clusters.Count; index++)
        {
            var cluster = clusters[index];
            if (index < skip)
            {
                MakeVoid(file, cluster.Start, cluster.End - cluster.Start);
                continue;
            }
            var relative = Math.Max(0, cluster.Stamp - origin);
            if (relative > keep && index > skip)
            {
                cut = cluster.Start;
                break;
            }
            WriteUInt(file, cluster.StampAt, cluster.StampWidth, (ulong)relative);
            lastKept = index;
        }

        file.SetLength(cut);
        if (segment.Size is not null) WriteVint(file, segment.SizeStart, segment.SizeWidth, (ulong)(cut - segment.DataStart));
        // Index pointers past the new end would send players to nowhere: those entries become padding.
        foreach (var (start, total, target) in seeks.Where(seek => segment.DataStart + seek.Target >= cut))
            MakeVoid(file, start, total);
        if (duration is { } durationField && lastKept >= 0)
        {
            // What the clip really holds: up to its last frame (a paused recording can be shorter than its wall-clock length).
            var last = clusters[lastKept];
            var held = Math.Max(0, last.Stamp - origin) + LastBlockOffset(file, last.StampAt, last.End) + 40L * 1_000_000 / scale;
            var units = (double)Math.Min(held, keep);
            Span<byte> bytes = stackalloc byte[8];
            if (durationField.Width == 4) BinaryPrimitives.WriteSingleBigEndian(bytes, (float)units);
            else BinaryPrimitives.WriteDoubleBigEndian(bytes, units);
            file.Position = durationField.Position;
            file.Write(bytes[..durationField.Width]);
        }
        file.Flush();
        return true;
    }

    /// <summary>The largest block time inside a cluster, relative to the cluster (SimpleBlock or Block in a BlockGroup).</summary>
    private static long LastBlockOffset(Stream file, long from, long end)
    {
        long latest = 0;
        Span<byte> time = stackalloc byte[2];
        foreach (var child in Children(file, from, end))
        {
            var block = child.Id == 0xA3 ? child
                : child.Id == 0xA0 && child.Size is { } groupSize
                    ? Children(file, child.DataStart, child.DataStart + groupSize).FirstOrDefault(part => part.Id == 0xA1) : null;
            if (block?.Size is not >= 4) continue;
            file.Position = block.DataStart;
            var lead = file.ReadByte();
            if (lead <= 0) continue;
            var trackWidth = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)lead << 24);
            file.Position = block.DataStart + trackWidth;
            file.ReadExactly(time);
            latest = Math.Max(latest, BinaryPrimitives.ReadInt16BigEndian(time));
        }
        return latest;
    }

    private static IEnumerable<Element> Children(Stream file, long start, long end)
    {
        var position = start;
        while (position < end && Element.Read(file, position) is { } child)
        {
            yield return child;
            if (child.Size is not { } size) yield break;
            position = child.DataStart + size;
        }
    }

    private static ulong ReadUInt(Stream file, long position, int width)
    {
        if (width is < 1 or > 8) return 0;
        Span<byte> bytes = stackalloc byte[width];
        file.Position = position;
        file.ReadExactly(bytes);
        ulong value = 0;
        foreach (var b in bytes) value = (value << 8) | b;
        return value;
    }

    private static void WriteUInt(Stream file, long position, int width, ulong value)
    {
        Span<byte> bytes = stackalloc byte[width];
        for (var index = width - 1; index >= 0; index--, value >>= 8) bytes[index] = (byte)value;
        file.Position = position;
        file.Write(bytes);
    }

    /// <summary>Writes a size in an EBML variable-length field of a given width.</summary>
    private static void WriteVint(Stream file, long position, int width, ulong value)
    {
        Span<byte> bytes = stackalloc byte[width];
        for (var index = width - 1; index >= 0; index--, value >>= 8) bytes[index] = (byte)value;
        bytes[0] |= (byte)(0x80 >> (width - 1));
        file.Position = position;
        file.Write(bytes);
    }

    /// <summary>Turns <paramref name="total"/> bytes at <paramref name="start"/> into one Void element.</summary>
    private static void MakeVoid(Stream file, long start, long total)
    {
        var width = total - 1 <= 127 ? 1 : 8;
        if (total < 1 + width) return;
        file.Position = start;
        file.WriteByte(Void);
        WriteVint(file, start + 1, width, (ulong)(total - 1 - width));
    }

    private sealed record Element(uint Id, long Start, long SizeStart, int SizeWidth, long? Size, long DataStart)
    {
        /// <summary>Reads an element header; <see cref="Size"/> is null for "unknown size".</summary>
        internal static Element? Read(Stream file, long position)
        {
            if (position >= file.Length) return null;
            file.Position = position;
            var first = file.ReadByte();
            if (first <= 0) return null;
            var idWidth = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)first << 24);
            if (idWidth > 4) return null;
            uint id = (uint)first;
            for (var index = 1; index < idWidth; index++)
            {
                var next = file.ReadByte();
                if (next < 0) return null;
                id = (id << 8) | (uint)next;
            }
            var sizeStart = file.Position;
            var lead = file.ReadByte();
            if (lead <= 0) return null;
            var sizeWidth = 1 + System.Numerics.BitOperations.LeadingZeroCount((uint)lead << 24);
            if (sizeWidth > 8) return null;
            ulong size = (ulong)(lead & (0xFF >> sizeWidth));
            var allOnes = size == (ulong)(0xFF >> sizeWidth);
            for (var index = 1; index < sizeWidth; index++)
            {
                var next = file.ReadByte();
                if (next < 0) return null;
                size = (size << 8) | (uint)next;
                allOnes &= next == 0xFF;
            }
            return new Element(id, position, sizeStart, sizeWidth, allOnes || size > long.MaxValue ? null : (long)size, file.Position);
        }
    }
}
