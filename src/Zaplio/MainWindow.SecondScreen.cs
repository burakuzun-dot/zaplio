using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LibVLCSharp.WPF;

namespace Zaplio;

public partial class MainWindow
{
    private SecondScreenWindow? _secondScreen;
    private VideoView? _attachedView;
    private long _forcedResume;

    /// <summary>The surface new playback attaches to: the second-screen window while it is open, otherwise the main video.</summary>
    private VideoView ActiveVideoView => _secondScreen?.VideoSurface ?? VideoView;

    private bool HasSecondMonitor => Monitors().Count > 1;

    private async void OnSecondScreen(object sender, RoutedEventArgs e) => await ToggleSecondScreenAsync();

    private async Task ToggleSecondScreenAsync()
    {
        if (_secondScreen is not null)
        {
            await CloseSecondScreenAsync(returnVideo: true);
            return;
        }
        if (_playingChannel is not { } channel)
        {
            SetFooter("İkinci ekranda oynatmak için önce bir yayın başlat.");
            return;
        }
        var current = MonitorFromWindow(new WindowInteropHelper(this).Handle, 2);
        var target = Monitors().FirstOrDefault(monitor => monitor.Handle != current);
        if (target.Handle == IntPtr.Zero)
        {
            SetFooter("İkinci ekran bulunamadı. Monitörün bağlı ve Windows'ta \"Genişlet\" modunda olmalı.", isError: true);
            return;
        }
        if (_fullscreen) ToggleFullscreen();
        if (_pip) TogglePip();

        var window = new SecondScreenWindow(); // no Owner: an owned window would minimize together with the main window
        window.ReturnRequested += () => _ = CloseSecondScreenAsync(returnVideo: true);
        window.KeyForwarded += OnWindowKeyDown;
        window.Show();
        var area = target.Bounds;
        SetWindowPos(new WindowInteropHelper(window).Handle, IntPtr.Zero, area.Left, area.Top,
            area.Right - area.Left, area.Bottom - area.Top, 0x0040 /* SWP_SHOWWINDOW */);
        _secondScreen = window;

        await RestartOnActiveSurfaceAsync(channel);
        UpdatePlaceholder();
        UpdateExtraControls();
        ShowOsd("İkinci ekranda oynatılıyor", "Esc ya da çift tık ile geri al");
    }

    /// <param name="returnVideo">Restart the current stream in the main window (false when playback stopped anyway).</param>
    private async Task CloseSecondScreenAsync(bool returnVideo)
    {
        if (_secondScreen is not { } window) return;
        _secondScreen = null;
        if (returnVideo && _playingChannel is { } channel && !_closing) await RestartOnActiveSurfaceAsync(channel);
        window.AllowClose = true;
        window.Close();
        if (_closing) return;
        UpdatePlaceholder();
        UpdateExtraControls();
        Activate();
    }

    /// <summary>
    /// LibVLC cannot move a running video to another window, so the stream is reopened on the new surface;
    /// films and archives continue from the same second.
    /// </summary>
    private async Task RestartOnActiveSurfaceAsync(Channel channel)
    {
        _forcedResume = channel.IsOnDemand && _player is { } player ? Math.Max(0, player.Time) : 0;
        await PlayChannelAsync(channel, reconnect: true);
    }

    private readonly record struct MonitorArea(IntPtr Handle, NativeRectangle Bounds);

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref NativeRectangle rect, IntPtr data);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    private static List<MonitorArea> Monitors()
    {
        var monitors = new List<MonitorArea>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref NativeRectangle _, IntPtr _) =>
        {
            var info = new MonitorInformation { Size = Marshal.SizeOf<MonitorInformation>() };
            if (GetMonitorInfo(monitor, ref info)) monitors.Add(new MonitorArea(monitor, info.Monitor));
            return true;
        }, IntPtr.Zero);
        return monitors;
    }
}
