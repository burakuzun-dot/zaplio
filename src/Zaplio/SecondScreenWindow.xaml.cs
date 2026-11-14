using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.WPF;

namespace Zaplio;

/// <summary>
/// Borderless video-only window on another monitor. The main window keeps the channel list and guide; keys pressed here
/// are handed back to the main window so zapping works from either screen.
/// </summary>
public partial class SecondScreenWindow : Window
{
    private readonly DispatcherTimer _hintTimer = new() { Interval = TimeSpan.FromSeconds(4) };

    public SecondScreenWindow()
    {
        InitializeComponent();
        _hintTimer.Tick += (_, _) =>
        {
            _hintTimer.Stop();
            Hint.Visibility = Visibility.Collapsed;
        };
        _hintTimer.Start();
    }

    internal VideoView VideoSurface => Video;

    /// <summary>Set before closing on purpose; otherwise closing (e.g. Alt+F4) asks the main window to take the video back.</summary>
    internal bool AllowClose { get; set; }

    internal event Action? ReturnRequested;
    internal event KeyEventHandler? KeyForwarded;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            ReturnRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ReturnRequested?.Invoke();
            e.Handled = true;
            return;
        }
        KeyForwarded?.Invoke(this, e);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ReturnRequested?.Invoke();
    }
}
