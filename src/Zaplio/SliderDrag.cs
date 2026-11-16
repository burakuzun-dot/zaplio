using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Zaplio;

/// <summary>
/// One press anywhere on a slider starts dragging it. WPF's slider with IsMoveToPointEnabled only jumps to the click and
/// needs a second press on the thumb before it follows the mouse; with this it follows from the first press until release.
/// Usage: <c>local:SliderDrag.IsEnabled="True"</c> (the volume slider's style sets it).
/// </summary>
public static class SliderDrag
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SliderDrag), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Slider slider) return;
        // handledEventsToo: the slider's own class handler runs first and may already have marked the press handled.
        if ((bool)e.NewValue)
        {
            slider.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnDown), handledEventsToo: true);
            slider.AddHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler(OnMove), handledEventsToo: true);
            slider.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnUp), handledEventsToo: true);
        }
        else
        {
            slider.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnDown));
            slider.RemoveHandler(UIElement.PreviewMouseMoveEvent, new MouseEventHandler(OnMove));
            slider.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnUp));
        }
    }

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (!slider.IsEnabled || Track(slider) is not { } track) return;
        e.Handled = true; // the thumb must not start a drag of its own
        slider.CaptureMouse();
        slider.Value = track.ValueFromPoint(e.GetPosition(track));
    }

    /// <summary>For tests and the self test: what a press at <paramref name="x"/> on the track would set.</summary>
    internal static double ValueAt(Slider slider, double x) =>
        Track(slider) is { } track ? track.ValueFromPoint(new Point(x, track.ActualHeight / 2)) : slider.Value;

    private static void OnMove(object sender, MouseEventArgs e)
    {
        var slider = (Slider)sender;
        if (!slider.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed || Track(slider) is not { } track) return;
        slider.Value = track.ValueFromPoint(e.GetPosition(track));
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        var slider = (Slider)sender;
        if (!slider.IsMouseCaptured) return;
        slider.ReleaseMouseCapture();
        e.Handled = true;
    }

    private static Track? Track(Slider slider) => slider.Template?.FindName("PART_Track", slider) as Track;
}
