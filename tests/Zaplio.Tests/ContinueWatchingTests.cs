using Xunit;

namespace Zaplio.Tests;

public sealed class ContinueWatchingTests
{
    private static readonly Guid Source = Guid.NewGuid();

    private static Channel Movie(string id, Guid? source = null) =>
        new(id, source ?? Source, "Film " + id, "Film", new Uri("http://example.com/" + id), IsOnDemand: true);

    private static Channel Episode(string id, long series) =>
        new(id, Source, "Dizi · " + id, "Dizi", new Uri("http://example.com/" + id), IsOnDemand: true, SeriesId: series, EpisodeId: 1);

    private static WatchEntry Watched(Channel channel) => new(channel, DateTimeOffset.UtcNow);

    [Fact]
    public void Keeps_half_watched_movies_of_this_source_in_history_order()
    {
        var history = new[] { Watched(Movie("b")), Watched(Movie("done")), Watched(Movie("a")), Watched(Movie("other", Guid.NewGuid())),
            Watched(Movie("never")), Watched(Episode("e1", 7)) };
        var positions = new Dictionary<string, long> { ["a"] = 60_000, ["b"] = 5_000, ["done"] = 9_000, ["other"] = 1_000, ["e1"] = 4_000 };

        var picked = ContinueWatching.Select(history, Source, LibrarySection.Movies, positions, new HashSet<string> { "done" });

        Assert.Equal(["b", "a"], picked.Select(entry => entry.Channel.Id));
        Assert.Equal(5_000, picked[0].Position);
    }

    [Fact]
    public void Series_show_only_their_latest_episode()
    {
        var history = new[] { Watched(Episode("s1e3", 1)), Watched(Episode("s1e2", 1)), Watched(Episode("s2e1", 2)), Watched(Movie("m")) };
        var positions = new Dictionary<string, long> { ["s1e3"] = 1_000, ["s1e2"] = 2_000, ["s2e1"] = 3_000, ["m"] = 4_000 };

        var picked = ContinueWatching.Select(history, Source, LibrarySection.Series, positions, new HashSet<string>());

        Assert.Equal(["s1e3", "s2e1"], picked.Select(entry => entry.Channel.Id));
    }

    [Fact]
    public void A_series_whose_latest_episode_is_finished_is_left_out()
    {
        var history = new[] { Watched(Episode("s1e3", 1)), Watched(Episode("s1e2", 1)) };
        var positions = new Dictionary<string, long> { ["s1e2"] = 2_000 };

        var picked = ContinueWatching.Select(history, Source, LibrarySection.Series, positions, new HashSet<string> { "s1e3" });

        Assert.Empty(picked);
    }

    [Fact]
    public void Live_section_and_limit()
    {
        var history = Enumerable.Range(0, 20).Select(index => Watched(Movie("m" + index))).ToArray();
        var positions = history.ToDictionary(entry => entry.Channel.Id, _ => 10_000L);

        Assert.Empty(ContinueWatching.Select(history, Source, LibrarySection.Live, positions, new HashSet<string>()));
        Assert.Equal(5, ContinueWatching.Select(history, Source, LibrarySection.Movies, positions, new HashSet<string>(), max: 5).Count);
    }
}
