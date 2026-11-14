using System.IO;
using Xunit;

namespace Zaplio.Tests;

/// <summary>Uses a temporary cache folder so the user's real cache is never touched.</summary>
public sealed class CatalogCacheTests : IDisposable
{
    private readonly string _previousFolder = CatalogCache.Folder;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"zaplio-cache-test-{Guid.NewGuid():N}");
    private readonly CatalogCache _cache = new();
    private readonly Source _source = new(Guid.NewGuid(), "Xtream", SourceKind.Xtream, "http://example.com", "user", "pass");

    public CatalogCacheTests() => CatalogCache.Folder = _folder;

    public void Dispose()
    {
        CatalogCache.Folder = _previousFolder;
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private Channel SampleChannel => new($"{_source.Id:N}:1", _source.Id, "Kanal A", "Ulusal;Haber",
        new Uri("http://example.com/live/user/pass/1.ts"), Logo: "http://logo/1.png", CatchupDays: 3);

    [Fact]
    public void Catalog_round_trips_with_logos_archive_and_time_zone()
    {
        _cache.Save(_source, new LoadedCatalog([SampleChannel], [new Uri("http://example.com/xmltv.php")], "Europe/Istanbul"));

        var hit = _cache.Load(_source);

        Assert.NotNull(hit);
        Assert.True(hit.IsFresh);
        var channel = Assert.Single(hit.Value.Channels);
        Assert.Equal(SampleChannel.Id, channel.Id);
        Assert.Equal("http://logo/1.png", channel.Logo);
        Assert.Equal(3, channel.CatchupDays);
        Assert.Equal(["Ulusal", "Haber"], channel.Categories);
        Assert.Equal("Europe/Istanbul", hit.Value.TimeZone);
    }

    [Fact]
    public void Files_are_not_readable_as_plain_text()
    {
        _cache.Save(_source, new LoadedCatalog([SampleChannel], []));
        var bytes = File.ReadAllBytes(Assert.Single(Directory.GetFiles(_folder)));
        Assert.DoesNotContain("pass", System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Changed_account_details_invalidate_the_cache()
    {
        _cache.Save(_source, new LoadedCatalog([SampleChannel], []));
        Assert.Null(_cache.Load(_source with { Password = "new-password" }));
    }

    [Fact]
    public void Movie_lists_round_trip_and_delete_removes_everything()
    {
        var movie = new VodItem(42, "Film", "Aksiyon", "2024", null, Poster: "http://poster/42.jpg");
        _cache.SaveVod(_source, LibrarySection.Movies, [movie]);
        _cache.Save(_source, new LoadedCatalog([SampleChannel], []));

        Assert.Equal("http://poster/42.jpg", Assert.Single(_cache.LoadVod(_source, LibrarySection.Movies)!.Value).Poster);
        Assert.Null(_cache.LoadVod(_source, LibrarySection.Series));

        _cache.Delete(_source.Id);
        Assert.Null(_cache.Load(_source));
        Assert.Null(_cache.LoadVod(_source, LibrarySection.Movies));
    }
}
