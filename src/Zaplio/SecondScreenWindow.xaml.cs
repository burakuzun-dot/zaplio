using System.ComponentModel;
using System.Windows;

namespace Zaplio;

/// <summary>
/// Borderless window for the playback view when it is on another monitor.
/// </summary>
public partial class SecondScreenWindow : Window
{
    private bool _allowClose;

    public SecondScreenWindow() => InitializeComponent();

    internal event Action? ReturnRequested;

    internal void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            // Return the view after WPF finishes processing this close request.
            Dispatcher.BeginInvoke(() => ReturnRequested?.Invoke());
        }
        base.OnClosing(e);
    }
}
