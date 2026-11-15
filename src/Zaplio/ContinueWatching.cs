namespace Zaplio;

/// <summary>Picks the half-watched movies or episodes for the "İzlemeye devam et" row.</summary>
internal static class ContinueWatching
{
    internal sealed record Entry(Channel Channel, long Position);

    /// <param name="history">Watch history, most recent first.</param>
    /// <returns>Most recent first; one entry per series (its latest episode), finished items and other sources left out.</returns>
    internal static IReadOnlyList<Entry> Select(IEnumerable<WatchEntry> history, Guid sourceId, LibrarySection section,
        IReadOnlyDictionary<string, long> positions, IReadOnlySet<string> completed, int max = 12)
    {
        if (section == LibrarySection.Live) return [];
        var series = section == LibrarySection.Series;
        var seenSeries = new HashSet<long>();
        var result = new List<Entry>();
        foreach (var entry in history)
        {
            var channel = entry.Channel;
            if (!channel.IsOnDemand || channel.SourceId != sourceId || (channel.SeriesId > 0) != series) continue;
            // A series shows only its latest episode, even when that one is finished (then the series is simply left out).
            if (series && !seenSeries.Add(channel.SeriesId)) continue;
            if (completed.Contains(channel.Id) || !positions.TryGetValue(channel.Id, out var position) || position <= 0) continue;
            result.Add(new Entry(channel, position));
            if (result.Count == max) break;
        }
        return result;
    }
}
