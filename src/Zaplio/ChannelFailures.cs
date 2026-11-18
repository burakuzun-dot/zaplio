namespace Zaplio;

/// <summary>
/// Live channels that would not open lately, so the list can show them dimmed. Learned only from the user's own tries
/// (nothing is probed); a channel that opens again is cleared at once, and marks fade out after <see cref="Memory"/>.
/// </summary>
internal static class ChannelFailures
{
    public static readonly TimeSpan Memory = TimeSpan.FromHours(24);

    public static void Record(IDictionary<string, DateTimeOffset> failures, string channelId, DateTimeOffset now) =>
        failures[channelId] = now;

    /// <returns>Whether a mark was removed.</returns>
    public static bool Clear(IDictionary<string, DateTimeOffset> failures, string channelId) => failures.Remove(channelId);

    /// <summary>When the channel last failed, if that is recent enough to show.</summary>
    public static DateTimeOffset? Recent(IReadOnlyDictionary<string, DateTimeOffset> failures, string channelId, DateTimeOffset now) =>
        failures.TryGetValue(channelId, out var at) && now - at < Memory ? at : null;

    /// <summary>Drops marks older than <see cref="Memory"/>; returns how many.</summary>
    public static int Prune(IDictionary<string, DateTimeOffset> failures, DateTimeOffset now)
    {
        var old = failures.Where(pair => now - pair.Value >= Memory).Select(pair => pair.Key).ToList();
        foreach (var key in old) failures.Remove(key);
        return old.Count;
    }
}

/// <summary>Spaces out the subscription checks made after a failed stream, so failures never turn into a burst of requests.</summary>
internal sealed class CheckSpacing(TimeSpan gap)
{
    private readonly Dictionary<Guid, DateTimeOffset> _last = new();

    /// <summary>True (and remembered) when the source was not checked within the gap.</summary>
    public bool TryStart(Guid source, DateTimeOffset now)
    {
        if (_last.TryGetValue(source, out var last) && now - last < gap) return false;
        _last[source] = now;
        return true;
    }
}
