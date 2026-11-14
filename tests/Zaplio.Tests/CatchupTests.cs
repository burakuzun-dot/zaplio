using Xunit;

namespace Zaplio.Tests;

public sealed class CatchupTests
{
    private static readonly Source Xtream = new(Guid.NewGuid(), "Xtream", SourceKind.Xtream, "http://example.com:8080", "user", "pass");

    private static Channel Live(int archiveDays) => new("id", Xtream.Id, "Kanal A", "Ulusal",
        new Uri("http://example.com:8080/live/user/pass/123.ts"), CatchupDays: archiveDays);

    [Fact]
    public void Builds_timeshift_address_in_server_time_zone()
    {
        var start = new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero); // 18:00 in Istanbul (UTC+3)

        var archive = CatalogLoader.Catchup(Xtream, Live(3), "Europe/Istanbul", start, start.AddMinutes(60), "Ana Haber");

        Assert.NotNull(archive);
        Assert.Equal("http://example.com:8080/timeshift/user/pass/65/2026-11-13:18-00/123.ts", archive.Stream.OriginalString);
        Assert.True(archive.IsOnDemand);
        Assert.Equal(0, archive.CatchupDays);
        Assert.Contains("Ana Haber", archive.Name);
        Assert.NotEqual("id", archive.Id);
    }

    [Fact]
    public void Unknown_end_time_uses_two_hours()
    {
        var start = new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);

        var archive = CatalogLoader.Catchup(Xtream, Live(1), "Etc/UTC", start, null, "Film");

        Assert.Equal("http://example.com:8080/timeshift/user/pass/120/2026-11-13:15-00/123.ts", archive!.Stream.OriginalString);
    }

    [Fact]
    public void Channels_without_archive_or_non_xtream_sources_have_no_catchup()
    {
        var start = DateTimeOffset.Now.AddHours(-1);
        var m3u = Xtream with { Kind = SourceKind.M3uUrl };

        Assert.Null(CatalogLoader.Catchup(Xtream, Live(0), null, start, null, "x"));
        Assert.Null(CatalogLoader.Catchup(m3u, Live(3), null, start, null, "x"));
    }
}

public sealed class WatchHistoryTests
{
    [Fact]
    public void Archive_ids_map_back_to_their_live_channel()
    {
        var source = new Source(Guid.NewGuid(), "X", SourceKind.Xtream, "http://example.com", "u", "p");
        var live = new Channel($"{source.Id:N}:ABC", source.Id, "Kanal A", "Ulusal", new Uri("http://example.com/live/u/p/7.ts"), CatchupDays: 2);
        var archive = CatalogLoader.Catchup(source, live, null, DateTimeOffset.Now.AddHours(-3), null, "Haber")!;

        Assert.Equal(live.Id, WatchHistory.ArchiveChannelId(archive.Id));
        Assert.Null(WatchHistory.ArchiveChannelId(live.Id));
    }

    [Fact]
    public void Shortcut_list_has_no_empty_or_duplicate_entries()
    {
        var items = Shortcuts.All.SelectMany(section => section.Items.Select(item => (section.Title, item.Keys))).ToList();
        Assert.All(Shortcuts.All.SelectMany(section => section.Items), item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Keys));
            Assert.False(string.IsNullOrWhiteSpace(item.Action));
        });
        Assert.Equal(items.Count, items.Distinct().Count());
    }
}
