using Xunit;

namespace Zaplio.Tests;

public sealed class DragScrollTests
{
    [Theory]
    [InlineData(0, 0, false)]     // a plain click
    [InlineData(4, 1, false)]     // a shaky click stays a click
    [InlineData(-5.9, 0, false)]
    [InlineData(6, 0, true)]      // moved far enough sideways: drag
    [InlineData(-30, 4, true)]
    [InlineData(8, 20, false)]    // mostly vertical: not a sideways drag
    public void Small_movements_stay_clicks(double x, double y, bool drag) => Assert.Equal(drag, DragScroll.IsDrag(x, y));

    [Fact]
    public void A_child_losing_the_mouse_does_not_end_the_drag()
    {
        // Regression: starting a drag takes the mouse from the pressed poster button; the button's "lost capture" event bubbles
        // to the strip, which treated it as the end of its own drag, never gave the mouse back and left the resize cursor on.
        var viewer = new object();
        var posterButton = new object();

        Assert.False(DragScroll.IsOwnCaptureLost(viewer, posterButton));
        Assert.True(DragScroll.IsOwnCaptureLost(viewer, viewer));
    }
}
