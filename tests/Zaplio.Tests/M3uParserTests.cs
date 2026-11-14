using System.IO;
using Xunit;

namespace Zaplio.Tests;

public sealed class M3uParserTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"zaplio-test-{Guid.NewGuid():N}.m3u");
    private readonly CatalogLoader _loader = new();

    public void Dispose()
    {
        _loader.Dispose();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private async Task<LoadedCatalog> LoadAsync(string playlist)
    {
        await File.WriteAllTextAsync(_file, playlist);
        return await _loader.LoadAsync(new Source(Guid.NewGuid(), "Test", SourceKind.M3uFile, _file), CancellationToken.None);
    }

    private const string Playlist = """
        #EXTM3U x-tvg-url="http://epg.example.com/guide.xml"
        #EXTINF:-1 tvg-id="kanal-a.tr" tvg-name="Kanal A" tvg-logo="http://logo.example.com/kanal-a.png" group-title="Ulusal;Haber",Kanal A HD
        #EXTVLCOPT:http-user-agent=TestAgent
        http://stream.example.com/kanal-a.m3u8
        #EXTINF:-1,Kanal, virgüllü
        http://stream.example.com/2.ts
        not-a-url
        #EXTINF:-1 group-title="Spor",
        rtmp://stream.example.com/3
        """;

    [Fact]
    public async Task Reads_channels_with_attributes()
    {
        var catalog = await LoadAsync(Playlist);

        Assert.Equal(3, catalog.Channels.Count);
        var first = catalog.Channels[0];
        Assert.Equal("Kanal A HD", first.Name);
        Assert.Equal(["Ulusal", "Haber"], first.Categories);
        Assert.Equal("kanal-a.tr", first.GuideId);
        Assert.Equal("Kanal A", first.GuideName);
        Assert.Equal("http://logo.example.com/kanal-a.png", first.Logo);
        Assert.Equal("TestAgent", first.UserAgent);
        Assert.Equal(new Uri("http://epg.example.com/guide.xml"), Assert.Single(catalog.GuideLocations));
    }

    [Fact]
    public async Task Keeps_commas_in_names_and_fills_defaults()
    {
        var catalog = await LoadAsync(Playlist);

        var second = catalog.Channels[1];
        Assert.Equal("Kanal, virgüllü", second.Name);
        Assert.Equal("Genel", second.Group);
        Assert.Null(second.Logo);
        Assert.Null(second.UserAgent); // options never leak into the next entry

        var third = catalog.Channels[2];
        Assert.Equal("Kanal 3", third.Name);
        Assert.Equal("Spor", third.Group);
        Assert.Equal("rtmp", third.Stream.Scheme);
    }

    [Fact]
    public async Task Channel_ids_are_stable_between_loads()
    {
        var source = new Source(Guid.NewGuid(), "Test", SourceKind.M3uFile, _file);
        await File.WriteAllTextAsync(_file, Playlist);
        var first = await _loader.LoadAsync(source, CancellationToken.None);
        var second = await _loader.LoadAsync(source, CancellationToken.None);

        Assert.Equal(first.Channels.Select(channel => channel.Id), second.Channels.Select(channel => channel.Id));
        Assert.All(first.Channels, channel => Assert.StartsWith($"{source.Id:N}:", channel.Id));
    }

    [Fact]
    public async Task Playlist_without_playable_entries_is_rejected()
    {
        await Assert.ThrowsAsync<SourceException>(() => LoadAsync("#EXTM3U\n#EXTINF:-1,Boş\nnot-a-url\n"));
    }
}
