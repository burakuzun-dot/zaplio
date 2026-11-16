using System.Buffers.Binary;
using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class MkvClipTests
{
    // ── a tiny Matroska writer for the tests ──────────────────────────────────────────────────────────

    private static byte[] Id(uint id) => id switch
    {
        > 0xFFFFFF => [(byte)(id >> 24), (byte)(id >> 16), (byte)(id >> 8), (byte)id],
        > 0xFFFF => [(byte)(id >> 16), (byte)(id >> 8), (byte)id],
        > 0xFF => [(byte)(id >> 8), (byte)id],
        _ => [(byte)id]
    };

    private static byte[] Size(long size, int width = 8)
    {
        var bytes = new byte[width];
        for (var index = width - 1; index >= 0; index--, size >>= 8) bytes[index] = (byte)size;
        bytes[0] |= (byte)(0x80 >> (width - 1));
        return bytes;
    }

    private static byte[] El(uint id, params byte[][] children)
    {
        var body = children.SelectMany(child => child).ToArray();
        return [.. Id(id), .. Size(body.Length), .. body];
    }

    private static byte[] UInt(uint id, ulong value, int width)
    {
        var bytes = new byte[width];
        for (var index = width - 1; index >= 0; index--, value >>= 8) bytes[index] = (byte)value;
        return El(id, bytes);
    }

    private static byte[] Float(uint id, double value)
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(bytes, value);
        return El(id, bytes);
    }

    /// <summary>A clip copied from 525 s into a video: four clusters 2 s apart, an index at the end that the seek head points to.</summary>
    private static byte[] CopiedClip()
    {
        byte[] Cluster(ulong ms) => El(0x1F43B675, UInt(0xE7, ms, 4), El(0xA3, [0x81, 0x00, 0x00, 0x80, 1, 2, 3, 4]));
        var info = El(0x1549A966, UInt(0x2AD7B1, 1_000_000, 3), Float(0x4489, 531_655));
        var tracks = El(0x1654AE6B, El(0xAE, UInt(0xD7, 1, 1), UInt(0x83, 1, 1)));
        var clusters = new[] { Cluster(525_000), Cluster(527_000), Cluster(529_000), Cluster(531_000) }.SelectMany(c => c).ToArray();
        var cues = El(0x1C53BB6B, El(0xBB, UInt(0xB3, 525_000, 4)));
        byte[] SeekHead(long cuesOffset) => El(0x114D9B74, El(0x4DBB, El(0x53AB, [0x1C, 0x53, 0xBB, 0x6B]), UInt(0x53AC, (ulong)cuesOffset, 8)));
        var seekLength = SeekHead(0).Length;
        var cuesOffset = seekLength + info.Length + tracks.Length + clusters.Length;
        var segmentBody = new[] { SeekHead(cuesOffset), info, tracks, clusters, cues }.SelectMany(c => c).ToArray();
        return [.. El(0x1A45DFA3, UInt(0x4282, 0x6D6B76, 3)), .. Id(0x18538067), .. Size(segmentBody.Length), .. segmentBody];
    }

    private static List<ulong> ClusterTimestamps(byte[] file)
    {
        var stamps = new List<ulong>();
        for (var index = 0; index + 10 < file.Length; index++)
            if (file[index] == 0x1F && file[index + 1] == 0x43 && file[index + 2] == 0xB6 && file[index + 3] == 0x75)
            {
                var stamp = index + 4 + 8; // cluster ID, 8-byte size, then the timestamp element
                stamps.Add(BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(stamp + 1 + 8, 4)));
            }
        return stamps;
    }

    [Fact]
    public void TheClipStartsAtZeroAndEndsAtTheWantedLength()
    {
        var original = CopiedClip();
        var stream = new MemoryStream();
        stream.Write(original);

        Assert.True(MkvClip.Trim(stream, keepMs: 4_500));
        var result = stream.ToArray();

        Assert.Equal([0UL, 2_000UL, 4_000UL], ClusterTimestamps(result)); // rebased, and the cluster at 6 s is gone
        // The file ends where the fourth cluster began: that cluster and the index after it are cut off.
        var clusterStarts = Enumerable.Range(0, original.Length - 3)
            .Where(index => original[index] == 0x1F && original[index + 1] == 0x43 && original[index + 2] == 0xB6 && original[index + 3] == 0x75).ToList();
        Assert.Equal(clusterStarts[3], result.Length);
    }

    [Fact]
    public void TheSegmentSizeAndDurationDescribeTheClip()
    {
        var stream = new MemoryStream();
        stream.Write(CopiedClip());
        MkvClip.Trim(stream, keepMs: 4_500);
        var result = stream.ToArray();

        var segment = result.AsSpan().IndexOf(new byte[] { 0x18, 0x53, 0x80, 0x67 });
        var size = BinaryPrimitives.ReadInt64BigEndian(result.AsSpan(segment + 4, 8)) & 0x00FF_FFFF_FFFF_FFFF;
        Assert.Equal(result.Length - (segment + 12), size);

        var duration = result.AsSpan().IndexOf(new byte[] { 0x44, 0x89 });
        // What the clip holds: its last cluster at 4 s plus one frame, not the 4.5 s that was asked for.
        Assert.Equal(4_040, BinaryPrimitives.ReadDoubleBigEndian(result.AsSpan(duration + 2 + 8, 8)));
    }

    [Fact]
    public void ASeekHeadEntryPointingPastTheEndBecomesPadding()
    {
        var stream = new MemoryStream();
        stream.Write(CopiedClip());
        MkvClip.Trim(stream, keepMs: 4_500);
        var result = stream.ToArray();

        Assert.Equal(-1, result.AsSpan().IndexOf(new byte[] { 0x4D, 0xBB })); // the Seek entry is now a Void element
        Assert.Contains((byte)0xEC, result);
    }

    [Fact]
    public void AShortClipKeepsEverything()
    {
        var stream = new MemoryStream();
        stream.Write(CopiedClip());
        MkvClip.Trim(stream, keepMs: 60_000);
        Assert.Equal([0UL, 2_000UL, 4_000UL, 6_000UL], ClusterTimestamps(stream.ToArray()));
    }

    [Fact]
    public void SomethingElseIsLeftAlone()
    {
        var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);
        Assert.False(MkvClip.Trim(stream, 1000));
        Assert.Equal(8, stream.Length);
    }
}
