using Xunit;

namespace Zaplio.Tests;

public sealed class DisplayTextTests
{
    [Theory]
    [InlineData("▶ Kanal A", "Kanal A")]
    [InlineData("★ Belgesel 1", "Belgesel 1")]
    [InlineData("🎬 Film", "Film")]
    [InlineData("| DE Haber", "DE Haber")]
    [InlineData("Ulusal", "Ulusal")]
    [InlineData("[HD] Kanal", "[HD] Kanal")]
    [InlineData("(Örnek) Liste", "(Örnek) Liste")]
    [InlineData("24 Haber", "24 Haber")]
    [InlineData("▶▶", "▶▶")] // nothing readable left: keep the original
    [InlineData("", "")]
    public void Removes_only_leading_decoration(string label, string expected) => Assert.Equal(expected, DisplayText.CleanLabel(label));

    [Fact]
    public void Channel_group_label_is_cleaned_but_matching_categories_are_untouched()
    {
        var channel = new Channel("a:1", Guid.Empty, "Kanal A", "▶ Ulusal;▶ Haber", new Uri("http://example.com/a"));

        Assert.Equal("Ulusal · Haber", channel.GroupLabel);
        Assert.Equal(["▶ Ulusal", "▶ Haber"], channel.Categories);
    }
}
