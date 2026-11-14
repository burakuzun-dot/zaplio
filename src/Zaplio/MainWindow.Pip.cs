using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Zaplio;

public partial class MainWindow
{
    private const double PipWidth = 480;
    private const double PipHeight = 270;
    private bool _pip;
    private Rect _pipRestoreBounds;
    private WindowState _pipRestoreState;
    private double _pipRestoreMinWidth;
    private double _pipRestoreMinHeight;
    private Point? _videoDragStart;
    private Point _videoDragWindow;

    /// <summary>True while the window shows only the video (fullscreen or picture-in-picture).</summary>
    private bool Immersive => _fullscreen || _pip;

    private void OnPip(object sender, RoutedEventArgs e) => TogglePip();

    private void TogglePip()
    {
        if (!_pip && (_player is null || _secondScreen is not null)) return;
        if (!_pip && _fullscreen) ToggleFullscreen();
        if (!_pip && _guideOpen) SetGuideOpen(false);
        _pip = !_pip;

        if (_pip)
        {
            _pipRestoreState = WindowState;
            _pipRestoreBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            _pipRestoreMinWidth = MinWidth;
            _pipRestoreMinHeight = MinHeight;
            WindowState = WindowState.Normal;
            MinWidth = 320;
            MinHeight = 180;
        }

        ApplyImmersiveLayout();

        if (_pip)
        {
            var area = SystemParameters.WorkArea;
            Width = PipWidth;
            Height = PipHeight;
            Left = area.Right - PipWidth - 16;
            Top = area.Bottom - PipHeight - 16;
            Topmost = true;
        }
        else
        {
            Topmost = false;
            MinWidth = _pipRestoreMinWidth;
            MinHeight = _pipRestoreMinHeight;
            if (!_pipRestoreBounds.IsEmpty)
            {
                Left = _pipRestoreBounds.Left;
                Top = _pipRestoreBounds.Top;
                Width = _pipRestoreBounds.Width;
                Height = _pipRestoreBounds.Height;
            }
            WindowState = _pipRestoreState;
            Activate();
        }
    }

    private void ApplyImmersiveLayout()
    {
        var immersive = Immersive;
        ExitFullscreenButton.Visibility = _fullscreen ? Visibility.Visible : Visibility.Collapsed;
        PipControls.Visibility = _pip ? Visibility.Visible : Visibility.Collapsed;
        var visibility = immersive ? Visibility.Collapsed : Visibility.Visible;
        TitleBar.Visibility = PlayerHeader.Visibility = PlayerControls.Visibility = Footer.Visibility = visibility;
        ApplyLibraryLayout();
        NavigationWidth.Width = new GridLength(immersive ? 0 : 218);
        ChannelWidth.Width = new GridLength(immersive ? 0 : 318);
        // In mini mode leave a thin frame outside the video: the video overlay would otherwise cover the resize border.
        PlayerPane.Margin = _pip ? new Thickness(5) : immersive ? new Thickness(0) : new Thickness(23, 22, 23, 20);
        VideoFrame.BorderThickness = new Thickness(immersive ? 0 : 1);
        VideoFrame.CornerRadius = new CornerRadius(immersive ? 0 : 15);
        UpdateGuide();
    }

    private void OnVideoMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            _videoDragStart = null;
            if (_pip) TogglePip();
            else ToggleFullscreen();
            e.Handled = true;
            return;
        }
        if (!_pip) return;
        var surface = (IInputElement)sender;
        _videoDragStart = ((Visual)sender).PointToScreen(e.GetPosition(surface));
        _videoDragWindow = new Point(Left, Top);
        surface.CaptureMouse();
        e.Handled = true;
    }

    private void OnVideoMouseMove(object sender, MouseEventArgs e)
    {
        if (_videoDragStart is not { } start || !_pip || e.LeftButton != MouseButtonState.Pressed) return;
        var current = ((Visual)sender).PointToScreen(e.GetPosition((IInputElement)sender));
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = _videoDragWindow.X + (current.X - start.X) / dpi.DpiScaleX;
        Top = _videoDragWindow.Y + (current.Y - start.Y) / dpi.DpiScaleY;
    }

    /// <summary>The overlay covers the resize border in mini mode, so the mouse wheel resizes it (16:9, bottom-right anchored).</summary>
    private void OnVideoMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_pip) return;
        var area = SystemParameters.WorkArea;
        var width = Math.Clamp(Width * (e.Delta > 0 ? 1.12 : 1 / 1.12), MinWidth, area.Width * 0.9);
        var height = Math.Round(width * 9 / 16);
        var right = Left + Width;
        var bottom = Top + Height;
        Width = width;
        Height = height;
        Left = Math.Max(area.Left, right - width);
        Top = Math.Max(area.Top, bottom - height);
        e.Handled = true;
    }

    private void OnVideoMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_videoDragStart is null) return;
        _videoDragStart = null;
        ((IInputElement)sender).ReleaseMouseCapture();
    }
}
