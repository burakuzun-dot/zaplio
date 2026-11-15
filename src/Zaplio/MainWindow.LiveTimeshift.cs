using System.Windows;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

/// <summary>
/// Pause and rewind for live TV.
/// Pause: LibVLC keeps reading a paused live stream into a temporary timeshift file, so playback resumes where it stopped.
/// Rewind: on channels with a provider archive, ← jumps back through the Xtream timeshift address and the archive plays like
/// a film (seekable). "Canlıya dön" returns to the live edge in both cases.
/// </summary>
public partial class MainWindow
{
    private const int LiveRewindSeconds = 30;
    private long _livePausedAt;           // Environment.TickCount64 while paused, otherwise 0
    private long _liveDelayMs;            // how far behind live the picture is after pauses
    private Channel? _liveReturnChannel;  // live row to go back to while watching an archive started from live

    /// <summary>Pausing works for films and, through VLC's timeshift, for live streams once they are playing.</summary>
    private bool CanPausePlayback => _player?.CanPause == true &&
        (_playingChannel?.IsOnDemand == true ? _vodStarted : _playingChannel is not null && _playStartedAt > 0);

    private bool IsLiveDelayed => _playingChannel is { IsOnDemand: false } && (_livePausedAt > 0 || _liveDelayMs > 1_500);

    private bool CanReturnToLive => IsLiveDelayed || (_liveReturnChannel is not null && _playingChannel?.IsOnDemand == true);

    private long CurrentLiveDelay(long now) => _liveDelayMs + (_livePausedAt > 0 ? now - _livePausedAt : 0);

    private void ResetLiveDelay()
    {
        _livePausedAt = 0;
        _liveDelayMs = 0;
    }

    private void OnLivePaused(VlcPlayer player, int revision)
    {
        if (player != _player || revision != _playbackRevision || _closing) return;
        if (_livePausedAt == 0) _livePausedAt = Environment.TickCount64;
        UpdateSelection();
        UpdateLiveStatus();
    }

    /// <summary>Called when VLC reports Playing for a live stream; a resume after pause adds to the delay.</summary>
    private void OnLiveResumed()
    {
        if (_livePausedAt == 0) return;
        _liveDelayMs += Environment.TickCount64 - _livePausedAt;
        _livePausedAt = 0;
    }

    /// <summary>Refreshes the "behind live" status and the return button (called every second and on pause/resume).</summary>
    private void UpdateLiveStatus()
    {
        LiveReturnButton.Visibility = CanReturnToLive ? Visibility.Visible : Visibility.Collapsed;
        if (!IsLiveDelayed) return;
        var delay = PlaybackTime.Format(CurrentLiveDelay(Environment.TickCount64));
        SetPlaybackStatus(_livePausedAt > 0 ? $"Duraklatıldı · canlıdan {delay} geride" : $"Canlıdan {delay} geride");
    }

    private async void OnReturnToLive(object sender, RoutedEventArgs e) => await ReturnToLiveAsync();

    private async Task ReturnToLiveAsync()
    {
        if (_playingChannel is { IsOnDemand: true } && _liveReturnChannel is { } live)
        {
            _liveReturnChannel = null;
            ShowOsd("Canlı yayına dönüldü", live.Name);
            await PlayChannelAsync(live);
            return;
        }
        if (IsLiveDelayed && _playingChannel is { } current)
        {
            ShowOsd("Canlı yayına dönüldü", current.Name);
            await PlayChannelAsync(current, reconnect: true); // reopening the stream lands on the live edge
        }
    }

    /// <summary>Jumps back on channels with a provider archive; elsewhere explains that only pausing is possible.</summary>
    private async Task LiveRewindAsync(int seconds = LiveRewindSeconds)
    {
        if (_playingChannel is not { IsOnDemand: false } live || ActiveSource is not { } source) return;
        var row = ActiveChannels.FirstOrDefault(channel => channel.Id == RowId(live.Id)) ?? live;
        var archiveChannel = VariantsOf(row).Prepend(live).FirstOrDefault(channel => channel.CatchupDays > 0);
        if (archiveChannel is null || source.Kind != SourceKind.Xtream)
        {
            ShowOsd("Bu kanalda geri sarma yok", "Boşluk ile duraklatıp kaldığın yerden devam edebilirsin");
            return;
        }

        var target = DateTimeOffset.Now - TimeSpan.FromMilliseconds(CurrentLiveDelay(Environment.TickCount64)) - TimeSpan.FromSeconds(seconds);
        // Archive addresses have minute precision; start at the minute and seek to the exact second.
        var start = new DateTimeOffset(target.Year, target.Month, target.Day, target.Hour, target.Minute, 0, target.Offset);
        var archive = CatalogLoader.Catchup(source, archiveChannel with { Name = row.Name },
            _catalog.GetValueOrDefault(source.Id)?.TimeZone, start, DateTimeOffset.Now.AddHours(2), "canlıdan geri sarıldı");
        if (archive is null) return;

        _forcedResume = (long)(target - start).TotalMilliseconds;
        ShowOsd($"{seconds} saniye geri", "Arşivden oynatılıyor · → ya da \"Canlıya dön\" ile geri gel");
        await PlayChannelAsync(archive);
        _liveReturnChannel = row;
        UpdateLiveStatus();
    }
}
