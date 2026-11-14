using Xunit;

namespace Zaplio.Tests;

public sealed class ChannelVariantsTests
{
    private static Channel Ch(string name, string id = "") =>
        new(id.Length > 0 ? id : name, Guid.Empty, name, "Ulusal", new Uri("http://example.com/" + Uri.EscapeDataString(name)));

    [Theory]
    [InlineData("Kanal A HD", "kanal a")]
    [InlineData("Kanal A FHD", "kanal a")]
    [InlineData("Kanal A 4K", "kanal a")]
    [InlineData("Kanal A ʜᴅ", "kanal a ʜᴅ")] // small-caps lookalikes are not touched
    [InlineData("Belgesel 1 H.265(Hevc)🎬", "belgesel 1")]
    [InlineData("Belgesel 1 4K🎬", "belgesel 1")]
    [InlineData("Kanal B EU HD", "kanal b eu")]
    [InlineData("Örnek TV 1080p 50 FPS", "ornek tv")]
    [InlineData("HDTV Haber", "hdtv haber")] // "HD" inside a word is not a quality tag
    public void Key_ignores_quality_words_only(string name, string expected) => Assert.Equal(expected, ChannelVariants.Key(name));

    [Theory]
    [InlineData("Kanal A 4K", 2160, false, "4K")]
    [InlineData("Kanal A FHD", 1080, false, "FHD")]
    [InlineData("Kanal A HD", 720, false, "HD")]
    [InlineData("Kanal A", 0, false, "")]
    [InlineData("Belgesel 1 H.265(Hevc)", 0, true, "HEVC")]
    [InlineData("Film 1080p HEVC", 1080, true, "FHD HEVC")]
    public void Reads_quality(string name, int rank, bool hevc, string label)
    {
        var quality = ChannelVariants.Quality(name);
        Assert.Equal(rank, quality.Rank);
        Assert.Equal(hevc, quality.Hevc);
        Assert.Equal(label, quality.Label);
    }

    [Fact]
    public void Display_name_drops_tags_and_empty_brackets()
    {
        Assert.Equal("Belgesel 1 🎬", ChannelVariants.DisplayName("Belgesel 1 H.265(Hevc)🎬"));
        Assert.Equal("Kanal A", ChannelVariants.DisplayName("Kanal A FHD"));
        Assert.Equal("HD", ChannelVariants.DisplayName("HD")); // never returns an empty name
    }

    [Fact]
    public void Groups_variants_and_keeps_others_apart()
    {
        var groups = ChannelVariants.Group([Ch("Kanal A HD"), Ch("Kanal B HD"), Ch("Kanal A FHD"), Ch("Kanal A 4K"), Ch("Kanal B EU HD")]);

        Assert.Equal(3, groups.Count);
        Assert.Equal("Kanal A", groups[0].DisplayName);
        Assert.Equal(3, groups[0].Channels.Count);
        Assert.False(groups[1].HasAlternatives); // Kanal B and Kanal B EU are different channels
    }

    [Theory]
    [InlineData(QualityMode.Auto, 0, "Kanal A FHD")]            // not measured yet → FHD
    [InlineData(QualityMode.Auto, 50_000_000, "Kanal A 4K")]
    [InlineData(QualityMode.Auto, 12_000_000, "Kanal A FHD")]
    [InlineData(QualityMode.Auto, 6_000_000, "Kanal A HD")]
    [InlineData(QualityMode.Auto, 1_000_000, "Kanal A HD")]     // nothing fits → lowest available
    [InlineData(QualityMode.Highest, 1_000_000, "Kanal A 4K")]
    [InlineData(QualityMode.Saver, 100_000_000, "Kanal A HD")]
    public void Chooses_by_connection_and_mode(QualityMode mode, double peak, string expected)
    {
        var group = ChannelVariants.Group([Ch("Kanal A HD"), Ch("Kanal A 4K"), Ch("Kanal A FHD")])[0];
        Assert.Equal(expected, ChannelVariants.Choose(group, mode, peak).Name);
    }

    [Fact]
    public void A_pinned_variant_wins()
    {
        var group = ChannelVariants.Group([Ch("Kanal A HD"), Ch("Kanal A 4K")])[0];
        Assert.Equal("Kanal A HD", ChannelVariants.Choose(group, QualityMode.Highest, 0, pinnedId: "Kanal A HD").Name);
    }

    [Fact]
    public void Fallbacks_try_lower_qualities_first_then_higher()
    {
        var group = ChannelVariants.Group([Ch("Kanal A 4K"), Ch("Kanal A SD"), Ch("Kanal A HD"), Ch("Kanal A FHD")])[0];
        var current = group.Channels.Single(channel => channel.Name == "Kanal A FHD");

        Assert.Equal(["Kanal A HD", "Kanal A SD", "Kanal A 4K"], ChannelVariants.Fallbacks(group, current).Select(channel => channel.Name));
    }
}
