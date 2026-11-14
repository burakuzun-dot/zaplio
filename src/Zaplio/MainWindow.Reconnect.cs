namespace Zaplio;

public partial class MainWindow
{
    private const int MaxReconnects = 3;
    private int _reconnectAttempts;
    private long _playStartedAt;
    private bool _reconnecting;
    private bool _streamWasPlaying; // set once VLC reports Playing for the current channel

    /// <summary>
    /// Restarts a dropped live stream a few times before giving up. Returns true when the drop was handled here
    /// (a reconnect is pending or the user already moved on), false when the caller should show the stop state.
    /// </summary>
    private async Task<bool> TryReconnectAsync(Channel channel, int revision, string reason)
    {
        if (_reconnecting) return true;
        if (!_state.Settings.AutoReconnect || _closing) return false;
        // A stream the server refused before it ever played (403, banned IP, connection limit…) is not retried:
        // hammering a refusing server makes providers extend their blocks.
        if (!_streamWasPlaying) return false;
        // A stream that ran for a while earns a fresh set of attempts.
        if (_playStartedAt > 0 && Environment.TickCount64 - _playStartedAt > 30_000) _reconnectAttempts = 0;
        if (_reconnectAttempts >= MaxReconnects) return false;

        var attempt = ++_reconnectAttempts;
        _streamHealth.RecordReconnect(Environment.TickCount64);
        _playStartedAt = 0;
        _playbackTimeout.Stop();
        _reconnecting = true;
        AppLog.Warn($"Canlı yayın kesildi ({reason}); yeniden bağlanılıyor {attempt}/{MaxReconnects}: {channel.Name} ({channel.Stream})");
        SetPlaybackStatus($"Bağlantı kesildi · yeniden bağlanılıyor ({attempt}/{MaxReconnects})", isError: true);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
        }
        finally
        {
            _reconnecting = false;
        }
        if (revision != _playbackRevision || _closing || _playingChannel?.Id != channel.Id) return true;
        await PlayChannelAsync(channel, reconnect: true);
        return true;
    }
}
