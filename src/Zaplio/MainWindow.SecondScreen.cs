using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Zaplio;

public partial class MainWindow
{
    private SecondScreenWindow? _secondScreen;
    private bool IsSecondScreenActive => _secondScreen?.IsVisible == true;

    private bool HasSecondMonitor => Monitors().Count > 1;

    private void OnSecondScreen(object sender, RoutedEventArgs e) => ToggleSecondScreen();

    private void ToggleSecondScreen()
    {
        if (_closing) return;
        if (IsSecondScreenActive)
        {
            ReturnVideoToMainWindow();
            return;
        }
        if (_player is null)
        {
            SetFooter("İkinci ekranda oynatmak için önce bir yayın başlat.");
            return;
        }
        var monitors = Monitors();
        if (monitors.Count < 2)
        {
            SetFooter("İkinci ekran bulunamadı. Monitörün bağlı ve Windows'ta \"Genişlet\" modunda olmalı.", isError: true);
            return;
        }
        if (_fullscreen) ToggleFullscreen();
        if (_pip) TogglePip();

        var current = MonitorFromWindow(new WindowInteropHelper(this).Handle, 2);
        var target = monitors.First(monitor => monitor.Handle != current);
        // Keep this window independent so the video remains visible when the main window minimizes.
        if (_secondScreen is null)
        {
            _secondScreen = new SecondScreenWindow();
            _secondScreen.ReturnRequested += ReturnVideoToMainWindow;
            _secondScreen.PreviewKeyDown += OnWindowKeyDown;
        }
        var window = _secondScreen;
        var area = target.Bounds;
        const uint noZOrderOrActivate = 0x0014;
        if (!SetWindowPos(new WindowInteropHelper(window).EnsureHandle(), IntPtr.Zero, area.Left, area.Top,
                area.Right - area.Left, area.Bottom - area.Top, noZOrderOrActivate))
        {
            SetFooter("Video ikinci ekrana taşınamadı. Tekrar dene.", isError: true);
            return;
        }

        window.Show();
        VideoView.MoveTo(window.VideoSlot);
        UpdateVodPreview();
        UpdatePlaceholder();
        UpdateExtraControls();
        ShowOsd("İkinci ekran", "Esc ya da çift tık ile geri al");
    }

    private void ReturnVideoToMainWindow()
    {
        if (_closing || !IsSecondScreenActive) return;
        if (WindowState == WindowState.Minimized) SystemCommands.RestoreWindow(this);
        HideSecondScreen();
        Activate();
    }

    private void HideSecondScreen()
    {
        if (_secondScreen is not { IsVisible: true } window) return;

        VideoView.MoveTo(MainVideoSlot);
        // The overlay switches owners on Loaded. Hiding keeps its previous owner alive until then.
        window.Hide();
        UpdateVodPreview();
        UpdatePlaceholder();
        UpdateExtraControls();
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
