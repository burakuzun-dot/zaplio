using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace Zaplio;

public partial class MainWindow
{
    private readonly DispatcherTimer _vodClock = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _vodStarted;
    private bool _seekingVod;
    private bool _updatingVodSeek;
    private long _forcedResume;
    private long _pendingVodPosition;
    private long _lastVodSave;


    private void PrepareVodPlayback(Channel channel)
    {
        _vodStarted = false;
        _pendingVodPosition = _forcedResume > 0 && channel.IsOnDemand ? _forcedResume
            : channel.IsOnDemand && _state.Settings.ResumeOnDemand ? _state.WatchPositions.GetValueOrDefault(channel.Id) : 0;
        _forcedResume = 0;
        _lastVodSave = Environment.TickCount64;
        _seekingVod = false;
        _updatingVodSeek = true;
        VodSeek.Value = 0;
        _updatingVodSeek = false;
        VodSeek.IsEnabled = false;
        VodElapsed.Text = "00:00";
        VodDuration.Text = "—";
    }

    private void UpdateVodPlaybackVisibility()
    {
        var vod = _playingChannel?.IsOnDemand == true;
        VodStop.Visibility = vod || CanPausePlayback ? Visibility.Visible : Visibility.Collapsed;
        VodTimeline.Visibility = vod && !_guideOpen && !Immersive ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnVodPaused(MediaPlayer player, int revision)
    {
        if (player != _player || revision != _playbackRevision || _closing) return;
        SaveVodPosition(player);
        UpdateSelection();
        SetPlaybackStatus("Duraklatıldı");
    }

    private void TickVodPlayback()
    {
        if (_player is not { } player || _playingChannel?.IsOnDemand != true) return;
        var length = player.Length;
        var canSeek = player.IsSeekable && length > 0;
        VodSeek.IsEnabled = canSeek;
        if (canSeek && _pendingVodPosition > 0)
        {
            var target = _pendingVodPosition;
            _pendingVodPosition = 0;
            if (target < length - 3000)
            {
                MarkSeek();
                player.Time = target;
                return;
            }
        }

        var time = Math.Max(0, player.Time);
        _updatingVodSeek = true;
        if (!_seekingVod)
        {
            VodSeek.Maximum = Math.Max(1, length);
            VodSeek.Value = Math.Min(time, VodSeek.Maximum);
            VodElapsed.Text = PlaybackTime.Format(time);
        }
        VodDuration.Text = length > 0 ? PlaybackTime.Format(length) : "—";
        _updatingVodSeek = false;
        if (player.IsPlaying && Environment.TickCount64 - _lastVodSave >= 30_000) SaveVodPosition(player);
    }

    private void SaveVodPosition(MediaPlayer player, bool completed = false)
    {
        if (_playingChannel is not { IsOnDemand: true } channel ||
            !_state.Sources.Any(source => source.Id == channel.SourceId)) return;
        if (!_state.Settings.ResumeOnDemand && !completed) return;
        if (!completed && (!_vodStarted || _pendingVodPosition > 0 || !player.IsSeekable)) return;
        _lastVodSave = Environment.TickCount64;
        var hadPosition = _state.WatchPositions.TryGetValue(channel.Id, out var previous);
        var position = completed ? 0 : player.Time;
        if (!completed && position < 0) return;
        var marked = completed && _state.CompletedItems.Add(channel.Id);
        var watchlistKey = marked && channel.SeriesId == 0 && MovieKeyFor(channel) is { } key && _state.VodWatchlist.Remove(key) ? key : null;
        if (position < 3000)
        {
            if (!hadPosition && !marked) return;
            _state.WatchPositions.Remove(channel.Id);
        }
        else
        {
            if (position == previous) return;
            _state.WatchPositions[channel.Id] = position;
        }
        try { SaveState(); }
        catch (SourceException exception)
        {
            if (marked) _state.CompletedItems.Remove(channel.Id);
            if (watchlistKey is not null) _state.VodWatchlist.Add(watchlistKey);
            if (hadPosition) _state.WatchPositions[channel.Id] = previous;
            else _state.WatchPositions.Remove(channel.Id);
            SetFooter(exception.Message, isError: true);
        }
    }

    private async void OnVodStop(object sender, RoutedEventArgs e) => await StopPlaybackAsync("Durduruldu");

    private void OnVodSeekStart(object sender, MouseButtonEventArgs e) => _seekingVod = VodSeek.IsEnabled;

    private void OnVodSeekEnd(object sender, MouseEventArgs e)
    {
        if (!_seekingVod) return;
        _seekingVod = false;
        SeekVod();
    }

    private void OnVodSeekChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingVodSeek || !_isReady || !VodSeek.IsEnabled) return;
        VodElapsed.Text = PlaybackTime.Format((long)e.NewValue);
        if (!_seekingVod) SeekVod();
    }

    private void SeekVod()
    {
        if (_player is not { IsSeekable: true } player || _playingChannel?.IsOnDemand != true) return;
        _pendingVodPosition = 0;
        MarkSeek();
        player.Time = (long)VodSeek.Value;
    }

}
