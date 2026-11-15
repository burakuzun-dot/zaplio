using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Zaplio;

/// <summary>
/// Small, shared motion helpers. Every effect is skipped when Windows animation effects are turned off
/// (Settings › Accessibility › Visual effects), so the interface then simply appears in its final state.
/// </summary>
internal static class Motion
{
    internal static bool Enabled => SystemParameters.ClientAreaAnimation;

    private static readonly IEasingFunction EaseOut = Freeze(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction Settle = Freeze(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 });

    private static IEasingFunction Freeze(EasingFunctionBase easing)
    {
        easing.Freeze();
        return easing;
    }

    // ── Reveal: fade in while sliding up a little ─────────────────────────────────────────────────────

    /// <summary>Fades an element in while it slides up by <paramref name="offset"/> pixels.</summary>
    internal static void Reveal(UIElement element, double offset = 10, int milliseconds = 260)
    {
        if (!Enabled) return;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds * 0.85))
        {
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.Stop
        });
        if (Translation(element) is { } shift)
            shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = EaseOut,
                FillBehavior = FillBehavior.Stop
            });
    }

    /// <summary>Fades an element in (used for posters and logos that arrive from the network).</summary>
    internal static void FadeIn(UIElement element, int milliseconds = 200)
    {
        if (!Enabled) return;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = EaseOut,
            FillBehavior = FillBehavior.Stop
        });
    }

    /// <summary>
    /// The rows currently on screen drift in one after another. Only called when the user switches a list
    /// (group, category, tab), never while scrolling or when a row changes.
    /// </summary>
    internal static void StaggerRows(ListBox list, int maxRows = 14)
    {
        if (!Enabled) return;
        list.Dispatcher.BeginInvoke(() =>
        {
            for (var index = 0; index < Math.Min(list.Items.Count, maxRows); index++)
            {
                if (list.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem row) continue;
                var delay = TimeSpan.FromMilliseconds(index * 24);
                var length = TimeSpan.FromMilliseconds(240);
                row.BeginAnimation(UIElement.OpacityProperty, Delayed(0, 1, delay, length));
                if (Translation(row) is { } shift) shift.BeginAnimation(TranslateTransform.YProperty, Delayed(8, 0, delay, length));
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Holds <paramref name="from"/> during the delay (so the row does not flash), then eases to <paramref name="to"/>.</summary>
    private static DoubleAnimationUsingKeyFrames Delayed(double from, double to, TimeSpan delay, TimeSpan length) => new()
    {
        FillBehavior = FillBehavior.Stop,
        KeyFrames =
        {
            new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)),
            new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(delay)),
            new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(delay + length), EaseOut)
        }
    };

    private static TranslateTransform? Translation(UIElement element)
    {
        switch (element.RenderTransform)
        {
            case TranslateTransform translate:
                return translate;
            case null:
            case MatrixTransform { Matrix.IsIdentity: true }:
                var created = new TranslateTransform();
                element.RenderTransform = created;
                return created;
            default:
                return null; // the element already uses its transform for something else
        }
    }

    // ── Press feedback: buttons dip slightly while held ───────────────────────────────────────────────

    /// <summary>Installs the press feedback once for every button in the application.</summary>
    internal static void InstallPressFeedback()
    {
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonDownEvent,
            new RoutedEventHandler((sender, _) => Press(sender as ButtonBase, true)), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseLeftButtonUpEvent,
            new RoutedEventHandler((sender, _) => Press(sender as ButtonBase, false)), handledEventsToo: true);
        EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.MouseLeaveEvent,
            new RoutedEventHandler((sender, _) => Press(sender as ButtonBase, false)), handledEventsToo: true);
    }

    private static void Press(ButtonBase? button, bool down)
    {
        // Scroll bar parts and the drop-down part of combo boxes would make whole controls jump.
        if (button is null or RepeatButton || button.TemplatedParent is ComboBox or ScrollBar) return;
        ScaleTransform scale;
        switch (button.RenderTransform)
        {
            case ScaleTransform existing:
                scale = existing;
                break;
            case null:
            case MatrixTransform { Matrix.IsIdentity: true }:
                if (!down || !Enabled) return;
                scale = new ScaleTransform();
                button.RenderTransformOrigin = new Point(0.5, 0.5);
                button.RenderTransform = scale;
                break;
            default:
                return;
        }
        if (!Enabled)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            return;
        }
        var target = down ? 0.95 : 1;
        if (!down && Math.Abs(scale.ScaleX - 1) < 0.001 && !scale.HasAnimatedProperties) return;
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(down ? 90 : 220)) { EasingFunction = down ? EaseOut : Settle };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    // ── Sliding selection for segmented tabs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Moves <paramref name="indicator"/> under the checked radio button, sliding it from tab to tab.
    /// The indicator must sit in a <see cref="Canvas"/> laid over the tabs: a canvas never asks for space, so the indicator
    /// cannot change the size of the tabs. (Sized from the tabs inside the same grid cell, it grew the cell, which stretched
    /// the tabs, which grew the indicator again — the row kept growing until it pushed the rest of the pane off screen.)
    /// </summary>
    internal static void TrackSelection(Panel tabs, FrameworkElement indicator)
    {
        if (indicator.Parent is not Canvas)
            throw new InvalidOperationException("The selection indicator must be placed inside a Canvas.");
        indicator.IsHitTestVisible = false;
        var shift = new TranslateTransform();
        indicator.RenderTransform = shift;

        void Move(bool animate)
        {
            if (tabs.Children.OfType<RadioButton>().FirstOrDefault(tab => tab.IsChecked == true) is not { ActualWidth: > 0 } tab ||
                indicator.Parent is not UIElement host) return;
            var position = tab.TranslatePoint(new Point(0, 0), host);
            indicator.Height = tab.ActualHeight;
            Canvas.SetTop(indicator, position.Y);
            // Width starts as Auto (NaN) until the first placement. A Width animation without a start value would then try to
            // animate from NaN and throw on the next frame, so the first placement is always direct. (ActualWidth cannot be used to
            // detect this: the 1 px border alone makes it 2.)
            if (!animate || !Enabled || double.IsNaN(indicator.Width))
            {
                shift.BeginAnimation(TranslateTransform.XProperty, null);
                indicator.BeginAnimation(FrameworkElement.WidthProperty, null);
                shift.X = position.X;
                indicator.Width = tab.ActualWidth;
                return;
            }
            var length = TimeSpan.FromMilliseconds(280);
            shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(shift.X, position.X, length) { EasingFunction = Settle });
            indicator.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(indicator.ActualWidth, tab.ActualWidth, length) { EasingFunction = EaseOut });
            // Keep the base values at the target so nothing is left at Auto when the animation is replaced or stopped.
            shift.X = position.X;
            indicator.Width = tab.ActualWidth;
        }

        foreach (var tab in tabs.Children.OfType<RadioButton>()) tab.Checked += (_, _) => Move(animate: true);
        tabs.SizeChanged += (_, _) => Move(animate: false);
        tabs.Loaded += (_, _) => Move(animate: false);
    }
}
