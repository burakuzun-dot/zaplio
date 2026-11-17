using Xunit;

namespace Zaplio.Tests;

public sealed class ThemePaletteTests
{
    public static TheoryData<bool, string> Combinations()
    {
        var data = new TheoryData<bool, string>();
        foreach (var light in new[] { false, true })
        foreach (var accent in ThemePalette.Accents) data.Add(light, accent.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void EveryThemeAndAccentStaysReadable(bool light, string accentKey)
    {
        var colors = ThemePalette.For(light, ThemePalette.Accent(accentKey));
        var label = $"{(light ? "açık" : "koyu")} / {accentKey}";

        foreach (var background in new[] { "Panel", "Canvas", "Surface" })
        {
            Assert.True(ThemePalette.Contrast(colors["Text"], colors[background]) >= 7, $"{label}: yazı / {background}");
            Assert.True(ThemePalette.Contrast(colors["Muted"], colors[background]) >= 4.5, $"{label}: soluk yazı / {background}");
            Assert.True(ThemePalette.Contrast(colors["Accent"], colors[background]) >= 3, $"{label}: vurgu / {background}");
        }
        Assert.True(ThemePalette.Contrast(colors["Text"], colors["Active"]) >= 7, $"{label}: seçili satırdaki yazı");
        foreach (var stop in new[] { "Brand.0", "Brand.1" })
            Assert.True(ThemePalette.Contrast(colors["Ink"], colors[stop]) >= 4.5, $"{label}: ana düğmedeki yazı / {stop}");
        foreach (var key in new[] { "Error", "LiveColor", "Warning" })
            Assert.True(ThemePalette.Contrast(colors[key], colors["Panel"]) >= 3, $"{label}: {key}");
    }

    [Fact]
    public void TheDefaultIsTheOriginalPurpleOnDark()
    {
        var colors = ThemePalette.For(light: false, ThemePalette.Accent(null));
        Assert.Equal(System.Windows.Media.Color.FromRgb(0xC1, 0xB4, 0xFF), colors["Accent"]);
        Assert.Equal(System.Windows.Media.Color.FromRgb(0x12, 0x16, 0x21), colors["Panel"]);
    }

    [Theory]
    [InlineData(0, true, false)]  // Dark
    [InlineData(1, false, true)]  // Light
    [InlineData(2, true, true)]   // System, Windows light
    [InlineData(2, false, false)] // System, Windows dark
    public void SameAsWindowsFollowsWindows(int mode, bool windowsLight, bool expected) =>
        Assert.Equal(expected, ThemePalette.IsLight((AppTheme)mode, windowsLight));
}
