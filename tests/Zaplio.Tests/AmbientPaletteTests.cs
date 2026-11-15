using Xunit;

namespace Zaplio.Tests;

public sealed class AmbientPaletteTests
{
    [Fact]
    public void Uses_the_two_edge_colors_and_ignores_letterbox_rows()
    {
        const int width = 12;
        const int height = 10;
        var pixels = new byte[width * height * 4];
        for (var y = 2; y < 8; y++)
        {
            for (var x = 0; x < 4; x++) pixels[(y * width + x) * 4 + 2] = 190;
            for (var x = 8; x < 12; x++) pixels[(y * width + x) * 4] = 200;
        }

        var palette = AmbientPalette.FromBgra(pixels, width, height);

        Assert.Equal(190, palette.Left.R);
        Assert.Equal(0, palette.Left.B);
        Assert.Equal(200, palette.Right.B);
        Assert.Equal(0, palette.Right.R);
        Assert.Equal(116, palette.Left.A);
        Assert.Equal(116, palette.Right.A);
    }

    [Fact]
    public void Black_frames_turn_off_the_glow_and_sparse_colors_stay_dim()
    {
        var pixels = new byte[6 * 4 * 4];
        var dark = AmbientPalette.FromBgra(pixels, 6, 4);

        Assert.Equal(0, dark.Left.A);
        Assert.Equal(0, dark.Right.A);

        pixels[2] = 128;
        var sparse = AmbientPalette.FromBgra(pixels, 6, 4);

        Assert.InRange(sparse.Left.A, (byte)1, (byte)30);
        Assert.Equal(128, sparse.Left.R);
        Assert.Equal(0, sparse.Right.A);
    }
}
