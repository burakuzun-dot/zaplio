using Xunit;

namespace Zaplio.Tests;

public sealed class VirtualizingTilePanelTests
{
    [Theory]
    [InlineData(700, 100, 136, 5, 140, 20)] // 700 / 136 = 5 columns of 140
    [InlineData(300, 7, 136, 2, 150, 4)]
    [InlineData(100, 3, 136, 1, 100, 3)]    // narrower than one tile: still one column
    [InlineData(400, 0, 136, 2, 200, 0)]
    public void Fits_as_many_columns_as_the_width_allows(double width, int count, double minimum, int columns, double tileWidth, int rows)
    {
        var layout = VirtualizingTilePanel.Layout(width, count, minimum, 1.5, 56);

        Assert.Equal(columns, layout.Columns);
        Assert.Equal(tileWidth, layout.TileWidth);
        Assert.Equal(rows, layout.Rows);
        Assert.Equal(Math.Round(tileWidth * 1.5 + 56), layout.TileHeight);
    }

    [Fact]
    public void A_huge_minimum_width_gives_a_one_column_list_with_fixed_row_height()
    {
        var layout = VirtualizingTilePanel.Layout(380, 24, 100_000, 0, 68);

        Assert.Equal(1, layout.Columns);
        Assert.Equal(380, layout.TileWidth);
        Assert.Equal(68, layout.TileHeight);
        Assert.Equal(24, layout.Rows);
    }

    [Fact]
    public void Only_the_rows_on_screen_and_one_spare_row_each_side_are_created()
    {
        // 5 columns, rows 200 px tall, 600 px of viewport, scrolled to row 10.
        var (first, last) = VirtualizingTilePanel.VisibleRange(2000, 600, 200, 5, 17_639);

        Assert.Equal(9 * 5, first);            // one spare row above
        Assert.Equal(15 * 5 - 1, last);        // rows 10-13 visible, one spare below
        Assert.True(last - first + 1 <= 40);   // a few dozen tiles, not seventeen thousand
    }

    [Fact]
    public void Range_is_clamped_to_the_list()
    {
        Assert.Equal((0, 6), VirtualizingTilePanel.VisibleRange(0, 600, 200, 5, 7));
        Assert.Equal((0, -1), VirtualizingTilePanel.VisibleRange(0, 600, 200, 5, 0));
        var (first, last) = VirtualizingTilePanel.VisibleRange(1_000_000, 600, 200, 5, 12);
        Assert.Equal(11, last);
        Assert.InRange(first, 0, 11);
    }
}
