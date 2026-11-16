using WinMedia = global::Windows.Media;

namespace Zaplio;

internal enum MediaCommand { Play, Pause, Stop, Next, Previous }

/// <summary>
/// Zaplio's entry in the Windows media controls (the card in the volume / quick settings panel and on the lock screen).
/// With it, the keyboard media keys reach Zaplio even while another window is in front.
/// Everything is optional: when Windows refuses (older builds, missing media service) the app simply runs without it.
/// </summary>
internal sealed class MediaSession : IDisposable
{
    private readonly WinMedia.SystemMediaTransportControls? _controls;
    private (string? Title, string? Subtitle, WinMedia.MediaPlaybackStatus Status, bool CanPause, bool CanZap)? _shown;
    private bool _disposed;

    /// <summary>Raised on a background thread; marshal to the UI thread before touching the window.</summary>
    public event Action<MediaCommand>? Command;

    public bool IsActive => !_disposed && _shown?.Title is not null;

    public MediaSession(IntPtr window)
    {
        try
        {
            var controls = WinMedia.SystemMediaTransportControlsInterop.GetForWindow(window);
            controls.IsEnabled = false;
            controls.ButtonPressed += OnButtonPressed;
            _controls = controls;
        }
        catch (Exception exception)
        {
            AppLog.Warn("Windows medya denetimleri kullanılamıyor; medya tuşları yalnızca pencere öndeyken çalışır.", exception);
        }
    }

    /// <param name="title">What is playing; null removes the card.</param>
    public void Update(string? title, string? subtitle, WinMedia.MediaPlaybackStatus status, bool canPause, bool canZap)
    {
        if (_controls is null || _disposed) return;
        var state = (title, subtitle, status, canPause, canZap);
        if (_shown == state) return;
        try
        {
            if (title is null)
            {
                _controls.IsEnabled = false;
                _controls.PlaybackStatus = WinMedia.MediaPlaybackStatus.Closed;
            }
            else
            {
                if (_shown?.Title != title || _shown?.Subtitle != subtitle)
                {
                    var display = _controls.DisplayUpdater;
                    display.Type = WinMedia.MediaPlaybackType.Video;
                    display.VideoProperties.Title = title;
                    display.VideoProperties.Subtitle = subtitle ?? "";
                    display.Update();
                }
                _controls.IsPlayEnabled = _controls.IsPauseEnabled = canPause;
                _controls.IsStopEnabled = true;
                _controls.IsNextEnabled = _controls.IsPreviousEnabled = canZap;
                _controls.PlaybackStatus = status;
                if (!IsActive) _controls.IsEnabled = true;
            }
            _shown = state;
        }
        catch (Exception exception)
        {
            AppLog.Warn("Windows medya kartı güncellenemedi.", exception);
            Dispose();
        }
    }

    internal static MediaCommand? Map(WinMedia.SystemMediaTransportControlsButton button) => button switch
    {
        WinMedia.SystemMediaTransportControlsButton.Play => MediaCommand.Play,
        WinMedia.SystemMediaTransportControlsButton.Pause => MediaCommand.Pause,
        WinMedia.SystemMediaTransportControlsButton.Stop => MediaCommand.Stop,
        WinMedia.SystemMediaTransportControlsButton.Next => MediaCommand.Next,
        WinMedia.SystemMediaTransportControlsButton.Previous => MediaCommand.Previous,
        _ => null
    };

    private void OnButtonPressed(WinMedia.SystemMediaTransportControls sender, WinMedia.SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        if (Map(args.Button) is { } command) Command?.Invoke(command);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Command = null;
        if (_controls is null) return;
        try
        {
            _controls.IsEnabled = false;
            _controls.PlaybackStatus = WinMedia.MediaPlaybackStatus.Closed;
        }
        catch (Exception)
        {
            // The session may already be unavailable.
        }
        try { _controls.ButtonPressed -= OnButtonPressed; }
        catch (Exception) { /* The session may already be unavailable. */ }
    }
}
