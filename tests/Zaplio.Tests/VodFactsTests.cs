using System.Text.Json;
using Xunit;

namespace Zaplio.Tests;

public sealed class VodFactsTests
{
    private static VodFacts? Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return VodFacts.Read(document.RootElement);
    }

    [Fact]
    public void ReadsGenreRunningTimeRatingAndCredits()
    {
        var facts = Read("""{"genre":"Komedi / Dram","duration":"01:52:00","rating":"7.4","director":"Yönetmen A","cast":"Oyuncu A, Oyuncu B,  ,Oyuncu A"}""");

        Assert.NotNull(facts);
        Assert.Equal("Komedi, Dram", facts.Genre);
        Assert.Equal("1 sa 52 dk", facts.Duration);
        Assert.Equal("★ 7,4", facts.Rating);
        Assert.Equal("Yönetmen A", facts.Director);
        Assert.Equal("Oyuncu A, Oyuncu B", facts.Cast);
        Assert.Equal(["Komedi, Dram", "1 sa 52 dk", "★ 7,4"], facts.Chips);
    }

    [Theory]
    [InlineData("2700", null, null, "45 dk")]
    [InlineData(null, "02:00:00", null, "2 sa")]
    [InlineData(null, null, "42", "42 dk")]
    [InlineData("0", "00:00:00", "0", null)]
    [InlineData(null, "bilinmiyor", null, null)]
    public void FormatsRunningTime(string? seconds, string? clock, string? minutes, string? expected) =>
        Assert.Equal(expected, VodFacts.FormatDuration(seconds, clock, minutes));

    [Theory]
    [InlineData("8", null, "★ 8,0")]
    [InlineData("0", "3.5", "★ 7,0")]
    [InlineData("", "", null)]
    [InlineData("12", null, null)]
    public void FormatsRatingOutOfTen(string? ten, string? five, string? expected) =>
        Assert.Equal(expected, VodFacts.FormatRating(ten, five));

    [Fact]
    public void PlaceholderValuesAndEmptyInfoGiveNothing()
    {
        Assert.Null(Read("""{"genre":"N/A","rating":"0","cast":"","director":"-"}"""));
        Assert.Null(Read("[]"));
    }

    [Fact]
    public void EpisodeFactsFallBackToTheSeriesCredits()
    {
        var episode = new VodFacts(null, "45 dk", "★ 8,0", null, null);
        var series = new VodFacts("Dram", "50 dk", "★ 7,0", "Yönetmen A", "Oyuncu A");

        Assert.Equal(new VodFacts("Dram", "45 dk", "★ 8,0", "Yönetmen A", "Oyuncu A"), episode.With(series));
    }
}
