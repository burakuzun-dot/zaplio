using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Xunit;

namespace Zaplio.Tests;

public sealed class MotionTests
{
    private static void OnStaThread(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    [Fact]
    public void Indicator_that_was_never_placed_is_set_directly_instead_of_animating_from_auto_width()
    {
        // Regression: the movie tab crashed with "DoubleAnimation cannot use NaN as default origin value" because the indicator
        // still had Width = Auto (NaN) when the first tab change tried to animate it.
        OnStaThread(() =>
        {
            var indicator = new Border { BorderThickness = new Thickness(1) };
            var first = new RadioButton { Content = "Tümü", Width = 80, IsChecked = true, GroupName = "t" };
            var second = new RadioButton { Content = "Favorilerim", Width = 110, GroupName = "t" };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } };
            var host = new Grid { Children = { new Canvas { Children = { indicator } }, tabs } };

            // Attached before the tabs have a size, like a pane that only becomes visible later: no placement can happen yet.
            Motion.TrackSelection(tabs, indicator);
            host.Measure(new Size(400, 40));
            host.Arrange(new Rect(0, 0, 400, 40));
            Assert.True(double.IsNaN(indicator.Width));

            second.IsChecked = true;

            Assert.Equal(110, indicator.Width);
            Assert.False(indicator.HasAnimatedProperties);
        });
    }

    [Fact]
    public void Indicator_is_placed_at_once_when_the_tabs_are_already_laid_out()
    {
        // Regression (found by the self test): at startup the indicator was attached after the tabs' Loaded and SizeChanged
        // had already fired, so the selected tab had no highlight until the first tab change.
        OnStaThread(() =>
        {
            var indicator = new Border { BorderThickness = new Thickness(1) };
            var first = new RadioButton { Content = "Canlı TV", Width = 90, Height = 34, IsChecked = true, GroupName = "w" };
            var second = new RadioButton { Content = "Filmler", Width = 80, Height = 34, GroupName = "w" };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } };
            var host = new Grid { Children = { new Canvas { Children = { indicator } }, tabs } };
            host.Measure(new Size(400, 40));
            host.Arrange(new Rect(0, 0, 400, 40));

            Motion.TrackSelection(tabs, indicator);

            Assert.Equal(90, indicator.Width);
            Assert.Equal(34, indicator.Height);
        });
    }

    [Fact]
    public void Indicator_aligns_to_the_tab_even_during_press_feedback()
    {
        OnStaThread(() =>
        {
            var indicator = new Border();
            var first = new RadioButton { Width = 90, Height = 34 };
            var second = new RadioButton
            {
                Width = 80, Height = 34, IsChecked = true,
                RenderTransform = new ScaleTransform(0.95, 0.95), RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } };
            var host = new Grid { Children = { new Canvas { Children = { indicator } }, tabs } };
            host.Measure(new Size(170, 34));
            host.Arrange(new Rect(0, 0, 170, 34));

            Motion.TrackSelection(tabs, indicator);

            Assert.Equal(90, Assert.IsType<TranslateTransform>(indicator.RenderTransform).X);
            Assert.Equal(0, Canvas.GetTop(indicator));
            Assert.Equal(80, indicator.Width);
        });
    }

    [Fact]
    public void Indicator_never_makes_the_tab_row_grow()
    {
        // Regression: in the movie pane the indicator was sized from tabs that stretched to fill its own cell,
        // so every layout pass made the row taller until the category, search and list were pushed off screen.
        OnStaThread(() =>
        {
            var indicator = new Border { BorderThickness = new Thickness(1) };
            var tabs = new UniformGrid
            {
                Rows = 1,
                Children =
                {
                    new RadioButton { Content = "Tümü", IsChecked = true, GroupName = "v", Height = 30 },
                    new RadioButton { Content = "Favorilerim", GroupName = "v", Height = 30 }
                }
            };
            var host = new Grid { Children = { new Canvas { Children = { indicator } }, tabs } };
            Motion.TrackSelection(tabs, indicator);

            for (var pass = 0; pass < 5; pass++)
            {
                host.Measure(new Size(300, double.PositiveInfinity)); // an Auto row: unlimited height on offer
                host.Arrange(new Rect(0, 0, 300, host.DesiredSize.Height));
                host.UpdateLayout();
            }

            Assert.Equal(30, host.DesiredSize.Height, 3);
            Assert.Equal(30, indicator.Height, 3);
        });
    }

    [Fact]
    public void Indicator_outside_a_canvas_is_refused()
    {
        OnStaThread(() =>
        {
            var indicator = new Border();
            var tabs = new StackPanel();
            _ = new Grid { Children = { indicator, tabs } };

            Assert.Throws<InvalidOperationException>(() => Motion.TrackSelection(tabs, indicator));
        });
    }

    [Fact]
    public void Later_changes_always_leave_a_real_width_behind()
    {
        OnStaThread(() =>
        {
            var indicator = new Border { BorderThickness = new Thickness(1), Width = 80 };
            var first = new RadioButton { Content = "Tümü", Width = 80, IsChecked = true, GroupName = "u" };
            var second = new RadioButton { Content = "Favorilerim", Width = 110, GroupName = "u" };
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Children = { first, second } };
            var host = new Grid { Children = { new Canvas { Children = { indicator } }, tabs } };
            host.Measure(new Size(400, 40));
            host.Arrange(new Rect(0, 0, 400, 40));
            Motion.TrackSelection(tabs, indicator);

            second.IsChecked = true;

            Assert.False(double.IsNaN(indicator.Width));
            Assert.Equal(110, indicator.Width); // base value; an animation may still be running towards it
        });
    }
}
