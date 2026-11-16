using Xunit;

namespace Zaplio.Tests;

public sealed class ContinueShelfLayoutTests
{
    private const double Gap = ContinueShelfLayout.Gap;

    [Fact]
    public void FourCardsFillAWideRowInEqualColumns()
    {
        var layout = new ContinueShelfLayout();
        layout.Fit(914, 230, 4);

        Assert.Equal(4, layout.Columns);
        Assert.Equal(1, layout.Rows);
        Assert.True(layout.Columns * (layout.CardWidth + Gap) <= 914);
        Assert.True(914 - layout.Columns * (layout.CardWidth + Gap) < layout.Columns);
        Assert.Equal(ContinueShelfLayout.MaximumCardHeight, layout.CardHeight);
    }

    [Fact]
    public void ANarrowerPanelWrapsIntoRowsThatShareTheHeight()
    {
        var layout = new ContinueShelfLayout();
        layout.Fit(646, 200, 4);

        Assert.Equal(2, layout.Columns);
        Assert.Equal(2, layout.Rows);
        Assert.True(layout.Rows * (layout.CardHeight + Gap) <= 200);
        Assert.True(layout.CardHeight >= ContinueShelfLayout.MinimumCardHeight);
    }

    [Fact]
    public void RowsThatDoNotFitKeepTheSmallestHeightAndScroll()
    {
        var layout = new ContinueShelfLayout();
        layout.Fit(646, 120, 6);

        Assert.Equal(1, layout.Rows);
        Assert.True(6 > layout.Columns * layout.Rows);
        Assert.InRange(layout.CardHeight, ContinueShelfLayout.MinimumCardHeight, ContinueShelfLayout.MaximumCardHeight);
    }

    [Fact]
    public void AFewCardsDoNotStretchBeyondTheMaximumWidth()
    {
        var layout = new ContinueShelfLayout();
        layout.Fit(1200, 140, 1);

        Assert.Equal(1, layout.Columns);
        Assert.Equal(ContinueShelfLayout.MaximumCardWidth, layout.CardWidth);
    }

    [Fact]
    public void ThePosterLeavesRoomForTheTitle()
    {
        var layout = new ContinueShelfLayout();
        layout.Fit(460, 300, 2);

        Assert.True(layout.PosterWidth <= layout.CardWidth * 0.3 + 1);
        Assert.Equal(layout.CardHeight - 16, layout.PosterHeight);
    }

    [Fact]
    public void AboveTheListTheCardsKeepTheirFixedSize()
    {
        var layout = new ContinueShelfLayout();
        layout.Fit(900, 300, 3);
        layout.UseDefault();

        Assert.Equal(236, layout.CardWidth);
        Assert.Equal(ContinueShelfLayout.MinimumCardHeight, layout.CardHeight);
    }
}
