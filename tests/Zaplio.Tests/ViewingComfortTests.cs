using System.Text.Json;
using Xunit;

namespace Zaplio.Tests;

public sealed class ViewingComfortTests
{
    [Fact]
    public void Default_settings_start_vlc_with_the_base_options_only() =>
        Assert.Equal(VlcOptions.Base, VlcOptions.For(new PlaybackSettings()));

    [Fact]
    public void Subtitle_and_sound_choices_become_vlc_options()
    {
        var options = VlcOptions.For(new PlaybackSettings
        {
            SubtitleSize = SubtitleSize.Larger, SubtitleBackground = true, NormalizeVolume = true
        });

        Assert.Contains("--sub-text-scale=160", options);
        Assert.Contains("--freetype-background-opacity=170", options);
        Assert.Contains("--audio-filter=normvol", options);
        Assert.True(options.Take(VlcOptions.Base.Length).SequenceEqual(VlcOptions.Base));
    }

    [Fact]
    public void A_changed_option_needs_a_new_engine_and_the_same_one_does_not()
    {
        var normal = VlcOptions.Key(VlcOptions.For(new PlaybackSettings()));
        Assert.Equal(normal, VlcOptions.Key(VlcOptions.For(new PlaybackSettings { Picture = new PictureAdjust(Brightness: 2) })));
        Assert.NotEqual(normal, VlcOptions.Key(VlcOptions.For(new PlaybackSettings { SubtitleSize = SubtitleSize.Large })));
        Assert.NotEqual(normal, VlcOptions.Key(VlcOptions.For(new PlaybackSettings { NormalizeVolume = true })));
    }

    [Theory]
    [InlineData(0, 80)]   // SubtitleSize is internal; the test class is public
    [InlineData(1, 100)]
    [InlineData(2, 130)]
    [InlineData(3, 160)]
    public void Subtitle_scale(int size, int scale) => Assert.Equal(scale, VlcOptions.SubtitleScale((SubtitleSize)size));

    [Fact]
    public void Picture_steps_stay_gentle_and_in_range()
    {
        Assert.True(PictureAdjust.Neutral.IsNeutral);
        Assert.Equal((1f, 1f, 1f), PictureAdjust.Neutral.Values);
        var bright = new PictureAdjust(Brightness: 2, Contrast: -2, Saturation: 9).Values;
        Assert.Equal(1.16f, bright.Brightness, 3);
        Assert.Equal(0.8f, bright.Contrast, 3);
        Assert.Equal(1.5f, bright.Saturation, 3); // clamped to +2
        Assert.Equal("+1", PictureAdjust.Label(1));
        Assert.Equal("−2", PictureAdjust.Label(-2));
        Assert.Equal("Normal", PictureAdjust.Label(0));
    }

    [Fact]
    public void Choices_are_remembered()
    {
        var state = new LocalState
        {
            Settings = new PlaybackSettings
            {
                SubtitleSize = SubtitleSize.Large, SubtitleBackground = true, NormalizeVolume = true,
                Picture = new PictureAdjust(Brightness: 1, Saturation: -1)
            }
        };
        var restored = JsonSerializer.Deserialize<LocalState>(JsonSerializer.Serialize(state))!;

        Assert.Equal(SubtitleSize.Large, restored.Settings.SubtitleSize);
        Assert.True(restored.Settings.SubtitleBackground);
        Assert.True(restored.Settings.NormalizeVolume);
        Assert.Equal(new PictureAdjust(Brightness: 1, Saturation: -1), restored.Settings.Picture);
    }

    [Fact]
    public void Older_settings_without_these_choices_still_load()
    {
        var restored = JsonSerializer.Deserialize<LocalState>("{\"Settings\":{\"AmbientLight\":false}}")!;
        Assert.Equal(SubtitleSize.Normal, restored.Settings.SubtitleSize);
        Assert.True(restored.Settings.Picture.IsNeutral);
        Assert.False(restored.Settings.NormalizeVolume);
    }
}
