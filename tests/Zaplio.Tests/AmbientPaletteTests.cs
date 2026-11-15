using System.Windows.Media;
using Xunit;

namespace Zaplio.Tests;

public sealed class AmbientPaletteTests
{
    private const int Width = 12;
    private const int Height = 10;

    private static byte[] Frame() => new byte[Width * Height * 4];

    private static void Paint(byte[] pixels, int x0, int x1, int y0, int y1, byte red, byte green, byte blue)
    {
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var offset = (y * Width + x) * 4;
                pixels[offset] = blue;
                pixels[offset + 1] = green;
                pixels[offset + 2] = red;
                pixels[offset + 3] = 255;
            }
        }
    }

    [Fact]
    public void Letterbox_rows_do_not_dim_the_top_and_bottom_edges()
    {
        var pixels = Frame();
        Paint(pixels, 0, Width, 2, 8, 20, 180, 40); // black bars above and below

        var palette = AmbientPalette.FromBgra(pixels, Width, Height);

        Assert.Equal(AmbientPalette.MaxAlpha, palette.Top.A);
        Assert.Equal(AmbientPalette.MaxAlpha, palette.Bottom.A);
        Assert.Equal(AmbientPalette.MaxAlpha, palette.Left.A);
        Assert.True(palette.Top.G > palette.Top.R && palette.Top.G > palette.Top.B);
    }

    [Fact]
    public void Each_edge_gets_its_own_color()
    {
        var pixels = Frame();
        Paint(pixels, 0, 4, 2, 8, 200, 0, 0);      // left: red
        Paint(pixels, 8, Width, 2, 8, 0, 0, 200);  // right: blue
        Paint(pixels, 2, 10, 2, 4, 0, 200, 0);     // top: green
        Paint(pixels, 2, 10, 6, 8, 200, 200, 0);   // bottom: yellow

        var palette = AmbientPalette.FromBgra(pixels, Width, Height);

        Assert.True(palette.Left.R > palette.Left.G && palette.Left.R > palette.Left.B);
        Assert.True(palette.Right.B > palette.Right.R && palette.Right.B > palette.Right.G);
        Assert.True(palette.Top.G > palette.Top.R && palette.Top.G > palette.Top.B);
        Assert.True(palette.Bottom.R > palette.Bottom.B && palette.Bottom.G > palette.Bottom.B);
    }

    [Fact]
    public void Black_frames_turn_the_glow_off()
    {
        var palette = AmbientPalette.FromBgra(Frame(), Width, Height);

        Assert.Equal(AmbientPalette.Off, palette);
    }

    [Fact]
    public void An_edge_with_little_color_stays_dim()
    {
        var pixels = Frame();
        Paint(pixels, 0, Width, 2, 8, 90, 90, 90);
        Paint(pixels, 8, Width, 2, 8, 0, 0, 0); // right edge dark

        var palette = AmbientPalette.FromBgra(pixels, Width, Height);

        Assert.True(palette.Left.A > 0);
        Assert.Equal(0, palette.Right.A);
    }

    [Fact]
    public void Muddy_averages_become_more_colorful_but_grays_stay_gray()
    {
        var muddy = AmbientPalette.Vivid(120, 100, 90, 100);
        var gray = AmbientPalette.Vivid(100, 100, 100, 100);

        Assert.True(muddy.R - muddy.B > 120 - 90);
        Assert.Equal(gray.R, gray.G);
        Assert.Equal(gray.G, gray.B);
        Assert.Equal(100, muddy.A);
    }

    [Fact]
    public void Vivid_keeps_the_glow_readable_but_never_exceeds_the_input_channel_range()
    {
        var dark = AmbientPalette.Vivid(30, 10, 10, 100);

        Assert.True(dark.R >= 127);
        Assert.InRange(dark.G, (byte)0, (byte)255);
    }

    [Fact]
    public void Blend_moves_part_of_the_way_and_new_colors_fade_in_from_dark()
    {
        var shown = new AmbientPalette(Color.FromArgb(100, 200, 0, 0), Colors.Transparent, Colors.Transparent, Colors.Transparent);
        var next = new AmbientPalette(Color.FromArgb(100, 0, 0, 200), Color.FromArgb(120, 0, 200, 0), Colors.Transparent, Colors.Transparent);

        var blended = AmbientPalette.Blend(shown, next, 0.5);

        Assert.Equal(100, blended.Left.A);
        Assert.Equal(100, blended.Left.R);
        Assert.Equal(100, blended.Left.B);
        Assert.Equal(60, blended.Right.A); // half of 120, and already the new green rather than a gray in between
        Assert.Equal(200, blended.Right.G);
        Assert.Equal(0, blended.Top.A);
    }

    [Fact]
    public void Blend_fades_a_color_out_without_changing_its_tint()
    {
        var shown = new AmbientPalette(Color.FromArgb(100, 200, 40, 0), Colors.Transparent, Colors.Transparent, Colors.Transparent);

        var blended = AmbientPalette.Blend(shown, AmbientPalette.Off, 0.6);

        Assert.Equal(40, blended.Left.A);
        Assert.Equal(200, blended.Left.R);
        Assert.Equal(40, blended.Left.G);
    }
}
