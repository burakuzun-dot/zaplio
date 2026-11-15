using System.Text.Json;
using Xunit;

namespace Zaplio.Tests;

public sealed class VodPreviewTests
{
    private static readonly Source Source = new(Guid.NewGuid(), "Örnek kaynak", SourceKind.Xtream, "https://example.invalid");
    private static readonly VodItem Series = new(42, "Örnek dizi", "Genel", "", null,
        Poster: "https://example.invalid/series.jpg", SeasonCount: 2);

    [Fact]
    public void Uses_episode_summary_and_saved_position()
    {
        var episode = ParseEpisode("""
            { "id": "123", "title": "İlk bölüm", "episode_num": 1,
              "info": { "plot": "Bölümün kısa özeti.", "movie_image": "https://example.invalid/episode.jpg" } }
            """);
        var state = new LocalState();
        state.WatchPositions[episode.Playable!.Id] = 329_000;

        var preview = Assert.IsType<VodPreview>(VodPreview.Create(episode, Series, Source.Id, state));

        Assert.Equal("İlk bölüm", preview.Title);
        Assert.Equal("1. Sezon · 1. Bölüm", preview.Metadata);
        Assert.Equal("Bölümün kısa özeti.", preview.Summary);
        Assert.Equal("https://example.invalid/episode.jpg", preview.Poster);
        Assert.Equal("Devam et · 05:29", preview.Action);
    }

    [Fact]
    public void Plays_from_the_start_when_resume_is_disabled_and_summary_is_missing()
    {
        var episode = ParseEpisode("""{ "id": 123, "episode_num": 1, "info": null }""");
        var state = new LocalState { Settings = new PlaybackSettings { ResumeOnDemand = false } };
        state.WatchPositions[episode.Playable!.Id] = 329_000;

        var preview = Assert.IsType<VodPreview>(VodPreview.Create(episode, Series, Source.Id, state));

        Assert.Null(preview.Summary);
        Assert.Equal(Series.Poster, preview.Poster);
        Assert.Equal("Oynat", preview.Action);
    }

    [Fact]
    public void Shows_series_poster_and_opens_the_episode_list()
    {
        var preview = Assert.IsType<VodPreview>(VodPreview.Create(Series, null, Source.Id, new LocalState()));

        Assert.Equal("Genel · 2 sezon", preview.Metadata);
        Assert.Equal(Series.Poster, preview.Poster);
        Assert.Equal("Bölümleri aç", preview.Action);
    }

    [Fact]
    public void Shows_a_movie_with_its_own_poster_and_play_action()
    {
        var channel = new Channel("film", Source.Id, "Örnek film", "Belgesel", new Uri("https://example.invalid/film"), IsOnDemand: true);
        var movie = new VodItem(43, channel.Name, "Belgesel", "2024", channel, Poster: "https://example.invalid/film.jpg");

        var preview = Assert.IsType<VodPreview>(VodPreview.Create(movie, null, Source.Id, new LocalState()));

        Assert.Equal("Belgesel · 2024", preview.Metadata);
        Assert.Equal(movie.Poster, preview.Poster);
        Assert.Equal("Oynat", preview.Action);
    }

    [Fact]
    public void Ignores_a_selection_from_a_different_source_or_series()
    {
        var episode = ParseEpisode("""{ "id": 123, "episode_num": 1 }""");

        Assert.Null(VodPreview.Create(episode, Series, Guid.NewGuid(), new LocalState()));
        Assert.Null(VodPreview.Create(episode, Series with { ProviderId = 99 }, Source.Id, new LocalState()));
    }

    private static VodItem ParseEpisode(string json)
    {
        using var document = JsonDocument.Parse($$"""{ "1": [{{json}}] }""");
        return Assert.Single(CatalogLoader.ParseEpisodes(Source, Series, (Source.Location, "", ""), document.RootElement));
    }
}
