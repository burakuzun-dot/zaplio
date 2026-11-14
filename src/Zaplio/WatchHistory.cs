namespace Zaplio;

internal static class WatchHistory
{
    private const string ArchiveMarker = ":archive:";

    /// <summary>For an archive item id ("{channel id}:archive:{start}") returns the live channel id, otherwise null.</summary>
    public static string? ArchiveChannelId(string id)
    {
        var index = id.IndexOf(ArchiveMarker, StringComparison.Ordinal);
        return index > 0 ? id[..index] : null;
    }
}
