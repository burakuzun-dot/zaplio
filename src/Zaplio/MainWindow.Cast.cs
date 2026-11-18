using System.Net.Http;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;

namespace Zaplio;

/// <summary>
/// "Televizyonda aç": sends what plays to a TV on the home network (DLNA). Playback here stops first, so the provider
/// still sees a single connection; the TV gets the stream through <see cref="CastRelay"/>, never the provider address.
/// </summary>
public partial class MainWindow
{
    private sealed class CastSession(DlnaRenderer tv, CastRelay relay, Channel channel, bool live)
    {
        public DlnaRenderer Tv { get; } = tv;
        public CastRelay Relay { get; } = relay;
        public Channel Channel { get; } = channel;
        public bool Live { get; } = live;
        public DlnaState State { get; set; } = DlnaState.Transitioning;
        public TimeSpan? Position { get; set; }
        public TimeSpan? Duration { get; set; }
        public bool SawPlaying { get; set; }
        public bool WarnedFirewall { get; set; }
        public long StartedAt { get; } = Environment.TickCount64;
    }

    private const int FirewallHintAfterMs = 12_000;
    private readonly DlnaClient _dlna = new();
    private readonly DispatcherTimer _castPoll = new() { Interval = TimeSpan.FromSeconds(2) };
    private CastSession? _cast;
    private bool _castPolling;
    private bool _castPollHooked;

    /// <summary>The TV a switch is going to, while <c>_cast</c> is briefly empty between two items.</summary>
    private DlnaRenderer? _castSwitchingTo;
    /// <summary>Chosen while a switch was still running (arrow keys through the list): sent right after, only the last one.</summary>
    private Channel? _castQueued;

    private bool IsCasting => _cast is not null || _castSwitchingTo is not null;

    private async Task StartCastAsync()
    {
        if (_playingChannel is not { } channel || _player is not { } player) return;
        if (LocalMedia.IsLocal(channel))
        {
            SetFooter(Lang.T("Bilgisayardaki videolar henüz televizyona gönderilemiyor."), isError: true);
            return;
        }
        if (DlnaProtocol.MimeFor(channel.Stream) is not { } mime)
        {
            SetFooter(Lang.T("Bu yayının biçimi (HLS) televizyona henüz gönderilemiyor."), isError: true);
            return;
        }

        var live = !channel.IsOnDemand;
        var resumeAt = live ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Math.Max(0, player.Time));
        var picker = new CastWindow(channel.Name, token => _dlna.SearchAsync(TimeSpan.FromSeconds(3), token), _state.LastCastDevice)
            { Owner = this };
        if (picker.ShowDialog() != true || picker.Selected is not { } tv || _closing) return;
        if (_state.LastCastDevice != tv.Udn)
        {
            _state.LastCastDevice = tv.Udn;
            _volumeDirty = true; // saved on close with the other small preferences
        }

        // Free the provider connection first; a movie's place is saved as the player is released.
        await StopPlaybackAsync(Lang.T("Televizyona gönderiliyor…"));
        await CastToAsync(channel, tv, mime, resumeAt);
    }

    /// <summary>
    /// While the TV plays, whatever is chosen next (a channel, the next movie, channel up/down) goes to the same TV instead
    /// of starting here. False when it cannot be sent (HLS, a file on this computer): it then plays here and the TV stops.
    /// </summary>
    private async Task<bool> SwitchCastAsync(Channel channel)
    {
        if (_castSwitchingTo is not null)
        {
            if (!Castable(channel)) return true; // ignored while switching; the TV keeps what it is getting
            _castQueued = channel;
            return true;
        }
        if (_cast is not { } current) return false;
        if (current.Channel.Id == channel.Id) return true; // already on the TV
        if (!Castable(channel))
        {
            SetFooter(Lang.F($"{channel.Name} televizyona gönderilemiyor; bu bilgisayarda açılıyor."), isError: true);
            await EndCastAsync(stopTv: true);
            return false;
        }

        var tv = current.Tv;
        _castSwitchingTo = tv;
        try
        {
            Channel? next = channel;
            var before = current.Channel;
            while (next is not null && !_closing)
            {
                _castQueued = null;
                var resumeAt = next.IsOnDemand && _state.Settings.ResumeOnDemand
                    ? TimeSpan.FromMilliseconds(_state.WatchPositions.GetValueOrDefault(next.Id)) : TimeSpan.Zero;
                await EndCastAsync(stopTv: true, keepPanel: true); // also keeps the previous movie's place
                // Backspace goes back to the channel the TV showed before, as it does here.
                if (!next.IsOnDemand && before is { IsOnDemand: false } && before.Id != next.Id && before.SourceId == next.SourceId)
                    _previousLiveChannel = before;
                if (!await CastToAsync(next, tv, DlnaProtocol.MimeFor(next.Stream)!, resumeAt)) break;
                before = next;
                next = _castQueued is { } queued && queued.Id != before.Id ? queued : null;
            }
        }
        finally
        {
            _castSwitchingTo = null;
            _castQueued = null;
        }
        return true;
    }

    private static bool Castable(Channel channel) => !LocalMedia.IsLocal(channel) && DlnaProtocol.MimeFor(channel.Stream) is not null;

    /// <returns>Whether the TV took it.</returns>
    private async Task<bool> CastToAsync(Channel channel, DlnaRenderer tv, string mime, TimeSpan resumeAt)
    {
        var live = !channel.IsOnDemand;
        CastRelay relay;
        try
        {
            relay = new CastRelay(DlnaClient.AddressTowards(tv.Address), tv.Address, channel.Stream, channel.UserAgent, channel.Referrer, mime, live);
        }
        catch (SocketException exception)
        {
            AppLog.Warn("Televizyona aktarım başlatılamadı.", exception);
            HideCastPanel();
            ShowPlaybackStopped(Lang.T("Televizyona aktarım başlatılamadı"), isError: true);
            SetFooter(Lang.T("Televizyona aktarım başlatılamadı; ağ bağlantını denetle."), isError: true);
            return false;
        }

        var session = new CastSession(tv, relay, channel, live);
        _cast = session;
        ShowCastPanel(session);
        try
        {
            await _dlna.SetUriAsync(tv, relay.Address, channel.Name, mime, live, _lifetime.Token);
            await _dlna.PlayAsync(tv, _lifetime.Token);
            AppLog.Info($"Televizyona gönderildi: {channel.Name} → {tv.Name}");
        }
        catch (Exception exception) when (exception is SourceException or HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            AppLog.Warn($"Televizyon yayını açmadı: {tv.Name}", exception);
            await EndCastAsync(stopTv: false);
            var reason = exception is SourceException ? exception.Message : Lang.T("televizyona ulaşılamadı");
            ShowPlaybackStopped(Lang.F($"{tv.Name} yayını açamadı"), isError: true);
            SetFooter($"{tv.Name}: {reason}", isError: true);
            return false;
        }

        if (resumeAt > TimeSpan.FromSeconds(5)) _ = SeekWhenPlayingAsync(session, resumeAt);
        if (!_castPollHooked)
        {
            _castPoll.Tick += async (_, _) => await PollCastAsync();
            _castPollHooked = true;
        }
        _castPoll.Start();
        return true;
    }

    /// <summary>A movie continues on the TV where it was here, once the TV has started playing it.</summary>
    private async Task SeekWhenPlayingAsync(CastSession session, TimeSpan to)
    {
        for (var attempt = 0; attempt < 20 && _cast == session; attempt++)
        {
            await Task.Delay(750);
            if (session.State != DlnaState.Playing) continue;
            try { await _dlna.SeekAsync(session.Tv, to, _lifetime.Token); }
            catch (Exception exception) when (exception is SourceException or HttpRequestException or TaskCanceledException)
            {
                AppLog.Warn("Televizyonda kalınan yere gidilemedi.", exception);
            }
            return;
        }
    }

    private async Task PollCastAsync()
    {
        if (_cast is not { } session || _castPolling) return;
        _castPolling = true;
        try
        {
            session.State = await _dlna.StateAsync(session.Tv, _lifetime.Token);
            if (!session.Live)
                (session.Position, session.Duration) = await _dlna.PositionAsync(session.Tv, _lifetime.Token);
            if (_cast != session) return;
            if (session.State == DlnaState.Playing) session.SawPlaying = true;

            // Stopped on the TV itself (its remote) or the movie ended there.
            if (session.SawPlaying && session.State is DlnaState.Stopped or DlnaState.NoMedia)
            {
                await EndCastAsync(stopTv: false);
                ShowPlaybackStopped(Lang.F($"{session.Tv.Name}: oynatma bitti"), isError: false);
                return;
            }
            if (!session.Relay.TvConnected && !session.WarnedFirewall &&
                Environment.TickCount64 - session.StartedAt > FirewallHintAfterMs)
            {
                session.WarnedFirewall = true;
                AppLog.Warn("Televizyon aktarım adresine bağlanmadı (güvenlik duvarı?).");
            }
            UpdateCastPanel(session);
        }
        catch (Exception exception) when (exception is SourceException or HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            // The TV may answer slowly while it buffers; the next poll tries again.
        }
        finally
        {
            _castPolling = false;
        }
    }

    /// <summary>Ends sending to the TV. <paramref name="stopTv"/>: also tell the TV to stop (not when it stopped by itself).</summary>
    /// <param name="keepPanel">Switching to the next item on the same TV: the remote stays on screen.</param>
    private async Task EndCastAsync(bool stopTv, bool keepPanel = false)
    {
        if (_cast is not { } session) return;
        _cast = null;
        _castPoll.Stop();
        if (!keepPanel) HideCastPanel();
        if (stopTv)
        {
            using var quick = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await _dlna.StopAsync(session.Tv, quick.Token); }
            catch (Exception exception) when (exception is SourceException or HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                AppLog.Warn("Televizyona durdur komutu gönderilemedi.", exception);
            }
        }
        session.Relay.Dispose();
        if (!session.Live && session.Position is { TotalSeconds: > 5 } position && _state.Settings.ResumeOnDemand)
        {
            _state.WatchPositions[session.Channel.Id] = (long)position.TotalMilliseconds;
            try { SaveState(); }
            catch (SourceException exception) { AppLog.Warn("Televizyondaki konum kaydedilemedi.", exception); }
        }
        AppLog.Info($"Televizyona gönderme bitti: {session.Tv.Name}");
    }

    private async Task ToggleCastPauseAsync()
    {
        if (_cast is not { } session) return;
        try
        {
            if (session.State == DlnaState.Playing)
            {
                await _dlna.PauseAsync(session.Tv, _lifetime.Token);
                session.State = DlnaState.Paused;
            }
            else
            {
                await _dlna.PlayAsync(session.Tv, _lifetime.Token);
                session.State = DlnaState.Playing;
            }
            UpdateCastPanel(session);
        }
        catch (Exception exception) when (exception is SourceException or HttpRequestException or TaskCanceledException)
        {
            SetFooter($"{session.Tv.Name}: {exception.Message}", isError: true);
        }
    }

    private async Task SeekCastAsync(int seconds)
    {
        if (_cast is not { Live: false, Position: { } position } session) return;
        var target = position + TimeSpan.FromSeconds(seconds);
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (session.Duration is { } duration && target > duration - TimeSpan.FromSeconds(3)) target = duration - TimeSpan.FromSeconds(3);
        try
        {
            await _dlna.SeekAsync(session.Tv, target, _lifetime.Token);
            session.Position = target;
            UpdateCastPanel(session);
        }
        catch (Exception exception) when (exception is SourceException or HttpRequestException or TaskCanceledException)
        {
            SetFooter($"{session.Tv.Name}: {exception.Message}", isError: true);
        }
    }

    private void ShowCastPanel(CastSession session)
    {
        CastTvName.Text = session.Tv.Name;
        CastTitle.Text = session.Channel.Name;
        CastBack.Visibility = CastForward.Visibility = session.Live ? Visibility.Collapsed : Visibility.Visible;
        CastPanel.Visibility = Visibility.Visible;
        UpdateVideoOverlay(); // or LibVLC's overlay window above the video swallows the clicks
        UpdateCastPanel(session);
    }

    private void HideCastPanel()
    {
        CastPanel.Visibility = Visibility.Collapsed;
        UpdateVideoOverlay();
    }

    private void UpdateCastPanel(CastSession session)
    {
        var state = session.State switch
        {
            DlnaState.Playing => Lang.T("Oynatılıyor"),
            DlnaState.Paused => Lang.T("Duraklatıldı"),
            DlnaState.Transitioning or DlnaState.Unknown => Lang.T("Televizyon yayını açıyor…"),
            _ => Lang.T("Bekleniyor…")
        };
        var time = !session.Live && session.Position is { } position
            ? $" · {PlaybackTime.Format((long)position.TotalMilliseconds)}" +
              (session.Duration is { TotalSeconds: > 0 } duration ? $" / {PlaybackTime.Format((long)duration.TotalMilliseconds)}" : "")
            : "";
        CastStatus.Text = session.WarnedFirewall && !session.Relay.TvConnected
            ? Lang.T("Televizyon bu bilgisayara bağlanamadı. Windows Güvenlik Duvarı Zaplio'yu engelliyor olabilir: ") +
              Lang.T("Windows Güvenlik › Güvenlik duvarı › Bir uygulamaya izin ver bölümünde Zaplio'ya Özel ağ izni ver.")
            : state + time;
        CastPause.Content = session.State == DlnaState.Paused ? Lang.T("Devam et") : Lang.T("Duraklat");
    }

    private async void OnCastPause(object sender, RoutedEventArgs e) => await ToggleCastPauseAsync();
    private async void OnCastBack(object sender, RoutedEventArgs e) => await SeekCastAsync(-30);
    private async void OnCastForward(object sender, RoutedEventArgs e) => await SeekCastAsync(30);

    private async void OnCastStop(object sender, RoutedEventArgs e)
    {
        var name = _cast?.Tv.Name;
        await EndCastAsync(stopTv: true);
        ShowPlaybackStopped(name is null ? Lang.T("Durduruldu") : Lang.F($"{name}: durduruldu"), isError: false);
    }

    /// <summary>Back to this computer: the TV stops and a movie continues here from where the TV was.</summary>
    private async void OnCastHere(object sender, RoutedEventArgs e)
    {
        if (_cast is not { } session) return;
        await EndCastAsync(stopTv: true); // saves the TV's position for a movie
        await PlayChannelAsync(session.Channel);
    }
}
