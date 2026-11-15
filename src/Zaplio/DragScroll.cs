using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zaplio;

/// <summary>
/// Lets a horizontal strip be moved with the mouse: drag it left or right, or turn the wheel.
/// A press that moves less than a few pixels is still a normal click on the item under it.
/// Usage: <c>local:DragScroll.IsEnabled="True"</c> on a <see cref="ScrollViewer"/>.
/// </summary>
public static class DragScroll
{
    /// <summary>Movement (in pixels) after which a press becomes a drag instead of a click.</summary>
    internal const double Threshold = 6;

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(DragScroll), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(DragState), typeof(DragScroll));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private sealed class DragState
    {
        public Point Start;
        public double StartOffset;
        public bool Pressed;
        public bool Dragging;
    }

    /// <summary>True once the pointer has moved far enough sideways for the press to count as a drag.</summary>
    internal static bool IsDrag(double movedX, double movedY) => Math.Abs(movedX) >= Threshold && Math.Abs(movedX) > Math.Abs(movedY);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ScrollViewer viewer) return;
        if ((bool)e.NewValue)
        {
            viewer.SetValue(StateProperty, new DragState());
            viewer.PreviewMouseLeftButtonDown += OnDown;
            viewer.PreviewMouseMove += OnMove;
            viewer.PreviewMouseLeftButtonUp += OnUp;
            viewer.LostMouseCapture += OnLostCapture;
            viewer.PreviewMouseWheel += OnWheel;
        }
        else
        {
            viewer.PreviewMouseLeftButtonDown -= OnDown;
            viewer.PreviewMouseMove -= OnMove;
            viewer.PreviewMouseLeftButtonUp -= OnUp;
            viewer.LostMouseCapture -= OnLostCapture;
            viewer.PreviewMouseWheel -= OnWheel;
        }
    }

    private static DragState State(ScrollViewer viewer) => (DragState)viewer.GetValue(StateProperty);

    private static void OnDown(object sender, MouseButtonEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        var state = State(viewer);
        state.Start = e.GetPosition(viewer);
        state.StartOffset = viewer.HorizontalOffset;
        state.Pressed = true;
        state.Dragging = false;
    }

    private static void OnMove(object sender, MouseEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        var state = State(viewer);
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            End(viewer, state); // the button was released somewhere we did not see: never keep the mouse
            return;
        }
        if (!state.Pressed) return;
        var position = e.GetPosition(viewer);
        var moved = position - state.Start;
        if (!state.Dragging)
        {
            if (!IsDrag(moved.X, moved.Y)) return;
            state.Dragging = true;
            // Takes the press away from the poster button, so releasing does not click it. The button then reports that it lost
            // the mouse, and that event bubbles up to this viewer: OnLostCapture must ignore it (see IsOwnCaptureLost).
            if (!viewer.CaptureMouse())
            {
                state.Dragging = false;
                return;
            }
            viewer.Cursor = Cursors.SizeWE;
        }
        viewer.ScrollToHorizontalOffset(state.StartOffset - moved.X);
        e.Handled = true;
    }

    private static void OnUp(object sender, MouseButtonEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        var state = State(viewer);
        var dragged = state.Dragging;
        End(viewer, state);
        if (dragged) e.Handled = true; // a drag is not a click on the poster under the pointer
    }

    /// <summary>Ends a press or drag in every case: gives the mouse back and restores the cursor.</summary>
    private static void End(ScrollViewer viewer, DragState state)
    {
        state.Pressed = false;
        state.Dragging = false;
        if (viewer.IsMouseCaptured) viewer.ReleaseMouseCapture();
        viewer.ClearValue(FrameworkElement.CursorProperty);
    }

    /// <summary>
    /// LostMouseCapture bubbles: the viewer also hears it when a child (the pressed poster button) loses the mouse, which is
    /// exactly what happens when the viewer takes it to start a drag. Only the viewer's own loss ends the drag.
    /// </summary>
    internal static bool IsOwnCaptureLost(object viewer, object originalSource) => ReferenceEquals(viewer, originalSource);

    private static void OnLostCapture(object sender, MouseEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        if (!IsOwnCaptureLost(viewer, e.OriginalSource)) return;
        End(viewer, State(viewer));
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = (ScrollViewer)sender;
        if (viewer.ScrollableWidth <= 0) return; // nothing to move: let the wheel reach whatever is behind
        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta * 0.6);
        e.Handled = true;
    }
}
