using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;

namespace Zaplio;

/// <summary>
/// Recording (R or the dot button) and slow, smooth speed changes (, and .; E steps frame by frame).
/// A recording copies the stream without re-encoding into Matroska (.mkv), so its quality is exactly the source's and every
/// codec fits (MPEG-TS silently dropped VP9/AV1/Theora video, leaving audio-only files).
/// <list type="bullet">
/// <item>A video from this computer never stops: record marks where you are, stop marks the end, and that span is copied in
/// the background from the file.</item>
/// <item>A provider's stream can only take the copy when it starts (LibVLC 3), and a second connection could break the
/// provider's connection limit, so it restarts where it is; the last frame stays on screen meanwhile instead of going black.</item>
/// </list>
/// Recordings go to Videolar\Zaplio; nothing is recorded unless you start it.
/// </summary>
public partial class MainWindow
{
    private string? _recordFile;
    private readonly List<string> _recordParts = [];
    private string? _recordChannelId;
    private long _recordStartedAt;
    private long _restartAtMs;
    private DispatcherTimer? _recordClock;
    private float _playbackRate = 1f;
    private DispatcherTimer? _rateRamp;

    // A local file's recording: where it began in the file, the file itself, and the last position seen while it ran.
    private long? _clipStart;
    private Channel? _clipChannel;
    private long _clipLast;
    private DispatcherTimer? _freezeTimeout;

    internal bool IsRecording => _recordFile is not null;

    private static readonly System.Windows.Media.SolidColorBrush RecordRed = CreateRecordRed();

    private static System.Windows.Media.SolidColorBrush CreateRecordRed()
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x4D, 0x4F));
        brush.Freeze();
        return brush;
    }

    private async void OnRecord(object sender, RoutedEventArgs e) => await ToggleRecordingAsync();

    private async Task ToggleRecordingAsync()
    {
        if (_playingChannel is not { } channel || _player is not { } player) return;
        if (_recordFile is not null)
        {
            await FinishRecordingAsync(restart: true);
            return;
        }

        try
        {
            Directory.CreateDirectory(RecordingPlan.Folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Kayıt klasörü oluşturulamadı.", exception);
            SetFooter("Kayıt klasörü oluşturulamadı.", isError: true);
            return;
        }
        _recordFile = Path.Combine(RecordingPlan.Folder, RecordingPlan.FileName(channel.Name, DateTime.Now));
        _recordParts.Clear();
        _recordChannelId = channel.Id;
        AppLog.Info("Kayıt başladı.");

        if (LocalMedia.IsLocal(channel) && player.IsSeekable)
        {
            // Nothing restarts: the span is copied from the file when the recording ends.
            _clipStart = _clipLast = Math.Max(0, player.Time);
            _clipChannel = channel;
        }
        else if (CanRecordFromTap && StartTapRecording(_recordFile))
        {
            // Nothing restarts: the stream's flowing copy is written from the next keyframe.
        }
        else
        {
            await FreezeFrameAsync(player);
            await RestartHereAsync(channel, player);
            if (_recordFile is null) return; // the restart failed or another channel took over
        }
        _recordStartedAt = Environment.TickCount64;
        _recordClock ??= CreateRecordClock();
        _recordClock.Start();
        UpdateRecordUi();
        ShowOsd("Kayıt başladı", _clipStart is not null || _tapRecording is not null
            ? "Yayın durmadan kaydediliyor · durdurmak için R" : "Kalite olduğu gibi korunur · durdurmak için R");
    }

    /// <summary>Plays the same stream again from where it is now (with or without the recording copy).</summary>
    private async Task RestartHereAsync(Channel channel, MediaPlayer player)
    {
        if (channel.IsOnDemand && player.IsSeekable && player.Time > 0) _restartAtMs = player.Time;
        await PlayChannelAsync(channel, reconnect: true);
    }

    private async Task FinishRecordingAsync(bool restart)
    {
        if (_recordFile is not { } file) return;
        if (_clipStart is not null)
        {
            if (_player is { } player && ReferenceEquals(_playingChannel, _clipChannel)) _clipLast = Math.Max(0, player.Time);
            await FinishClipAsync(file);
            return;
        }
        if (_tapRecording is not null)
        {
            _recordFile = null;
            _recordChannelId = null;
            _recordClock?.Stop();
            UpdateRecordUi();
            await FinishTapRecordingAsync(file); // nothing restarts
            return;
        }
        _recordFile = null;
        _recordChannelId = null;
        _recordClock?.Stop();
        UpdateRecordUi();
        if (restart && _playingChannel is { } channel && _player is { } current)
        {
            await FreezeFrameAsync(current);
            await RestartHereAsync(channel, current);
        }
        AnnounceRecording(file);
    }

    /// <summary>Playback moved on (another channel, stop, end): the file is already closed with the old stream.</summary>
    private void RecordingInterrupted()
    {
        if (_recordFile is not { } file) return;
        if (_clipStart is not null)
        {
            _ = FinishClipAsync(file); // _clipLast holds the last position seen before playback moved on
            return;
        }
        if (_tapRecording is not null)
        {
            _recordFile = null;
            _recordChannelId = null;
            _recordClock?.Stop();
            UpdateRecordUi();
            _ = FinishTapRecordingAsync(file);
            return;
        }
        _recordFile = null;
        _recordChannelId = null;
        _recordClock?.Stop();
        UpdateRecordUi();
        AnnounceRecording(file);
    }

    /// <summary>Copies the recorded span of a local file into the recording, in the background and without a display.</summary>
    private async Task FinishClipAsync(string file)
    {
        var source = _clipChannel?.Stream;
        var (from, to) = (_clipStart ?? 0, _clipLast);
        if (to < from) (from, to) = (to, from);
        _recordFile = null;
        _recordChannelId = null;
        _clipStart = null;
        _clipChannel = null;
        _recordClock?.Stop();
        UpdateRecordUi();
        if (source is null || to - from < 500 || _engine is not { } engine)
        {
            SetFooter("Kayıt çok kısa; en az yarım saniye kaydet.", isError: true);
            return;
        }

        SetFooter("Kayıt hazırlanıyor…");
        var copied = await Task.Run(async () =>
        {
            try
            {
                using var media = new Media(engine, source);
                foreach (var option in RecordingPlan.ClipOptions(file, from, to)) media.AddOption(option);
                using var copier = new MediaPlayer(engine); // never attached to a video view: the copy has no picture or sound
                var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                copier.EndReached += (_, _) => finished.TrySetResult(true);
                copier.EncounteredError += (_, _) => finished.TrySetResult(false);
                if (!copier.Play(media)) return false;
                // VLC 3 does not always honour the stop time on large files (a 4 GB 4K VP9 video was copied to its end), so the
                // copy is also stopped here once it has gone past the end; MkvClip then trims the overshoot exactly.
                var limit = Environment.TickCount64 + Math.Clamp((to - from) / 5 + 60_000, 60_000, 15 * 60_000);
                while (!finished.Task.IsCompleted && Environment.TickCount64 < limit && copier.Time < to + 2_000)
                    await Task.WhenAny(finished.Task, Task.Delay(50));
                var done = finished.Task.IsCompleted ? finished.Task.Result : copier.Time >= to;
                copier.Stop();
                return done && MkvClip.Trim(file, to - from); // start at zero, end where the recording ended
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                AppLog.Error("Kayıt kopyalanamadı.", exception);
                return false;
            }
        });
        if (_closing) return;
        _recordParts.Clear();
        if (copied) _recordParts.Add(file);
        AnnounceRecording(file);
    }

    private void AnnounceRecording(string file)
    {
        var parts = _recordParts.Where(part => File.Exists(part) && new FileInfo(part).Length > 0).ToArray();
        _recordParts.Clear();
        AppLog.Info($"Kayıt bitti ({parts.Length} parça).");
        if (parts.Length == 0)
        {
            SetFooter("Kayıt alınamadı; bu yayın kaydedilemiyor olabilir.", isError: true);
            return;
        }
        var more = parts.Length > 1 ? $" (+{parts.Length - 1} parça, yayın yeniden bağlandığı için)" : "";
        ShowOsd("Kayıt kaydedildi", Path.GetFileName(parts[0]) + more);
        SetFooter($"Kayıt kaydedildi: {parts[0]}{more}");
    }

    /// <summary>The media options a (re)started stream needs: the recording copy and where to begin. Read once per start.</summary>
    private (string? Record, long StartAt) TakeStartOptions(Channel channel)
    {
        string? record = null;
        if (_recordFile is { } file && _recordChannelId == channel.Id && _clipStart is null && _tapRecording is null)
        {
            var part = RecordingPlan.PartPath(file, _recordParts.Count + 1); // a reconnect continues in a new part, never over the old one
            _recordParts.Add(part);
            record = RecordingPlan.StreamOutput(part);
        }
        var startAt = _restartAtMs;
        _restartAtMs = 0;
        return (record, startAt);
    }

    /// <summary>Remembers where a local recording has got to (called by the playback clock).</summary>
    private void TrackClip(MediaPlayer player)
    {
        if (_clipStart is not null && ReferenceEquals(_playingChannel, _clipChannel)) _clipLast = Math.Max(0, player.Time);
    }

    // ── the frozen frame while a stream restarts ─────────────────────────────────────────────────────

    /// <summary>Holds the current picture on screen until the restarted stream shows its first frame.</summary>
    private async Task FreezeFrameAsync(MediaPlayer player)
    {
        var path = Path.Combine(Path.GetTempPath(), $"zaplio-freeze-{Environment.ProcessId}.png");
        try
        {
            if (!await Task.Run(() => player.TakeSnapshot(0, path, 0, 0)) || !File.Exists(path)) return;
            var frame = new BitmapImage();
            frame.BeginInit();
            frame.CacheOption = BitmapCacheOption.OnLoad; // read now, so the temporary file can go
            frame.UriSource = new Uri(path);
            frame.EndInit();
            frame.Freeze();
            FreezeFrame.Source = frame;
            FreezeFrameHost.BeginAnimation(OpacityProperty, null);
            FreezeFrameHost.Opacity = 1;
            FreezeFrameHost.Visibility = Visibility.Visible;
            _freezeTimeout ??= CreateFreezeTimeout();
            _freezeTimeout.Stop();
            _freezeTimeout.Start();
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            AppLog.Warn("Donmuş kare alınamadı.", exception);
        }
        finally
        {
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private DispatcherTimer CreateFreezeTimeout()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) }; // never leave a still picture over a stream that did not come back
        timer.Tick += (_, _) => ReleaseFreezeFrame();
        return timer;
    }

    /// <summary>The restarted stream is on screen (or playback stopped): the held frame fades away.</summary>
    private void ReleaseFreezeFrame()
    {
        _freezeTimeout?.Stop();
        if (FreezeFrameHost.Visibility != Visibility.Visible) return;
        if (!Motion.Enabled)
        {
            FreezeFrameHost.Visibility = Visibility.Collapsed;
            FreezeFrame.Source = null;
            return;
        }
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { BeginTime = TimeSpan.FromMilliseconds(180) };
        fade.Completed += (_, _) =>
        {
            if (FreezeFrameHost.Opacity > 0.01) return;
            FreezeFrameHost.Visibility = Visibility.Collapsed;
            FreezeFrame.Source = null;
        };
        FreezeFrameHost.BeginAnimation(OpacityProperty, fade);
    }

    private DispatcherTimer CreateRecordClock()
    {
        var clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clock.Tick += (_, _) => UpdateRecordUi();
        return clock;
    }

    private void UpdateRecordUi()
    {
        var recording = _recordFile is not null;
        RecordBadge.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        RecordTime.Text = PlaybackTime.Format(recording ? Environment.TickCount64 - _recordStartedAt : 0);
        if (recording) RecordDot.Fill = RecordRed;
        else RecordDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Muted");
        RecordButton.ToolTip = recording ? "Kaydı durdur (R)" : "Kaydet: oynayanı kalitesini bozmadan kaydet (R)";
        System.Windows.Automation.AutomationProperties.SetName(RecordButton, recording ? "Kaydı durdur" : "Kaydet");
        if (recording && Motion.Enabled && RecordBadgeDot.Opacity >= 1)
            RecordBadgeDot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(700))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        else if (!recording) RecordBadgeDot.BeginAnimation(OpacityProperty, null);
    }

    // ── speed ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One step slower or faster on the speed ladder, changed gradually so the picture and sound do not lurch.</summary>
    private void ChangeSpeed(bool faster) => SetSpeed(PlaybackSpeed.Step(_playbackRate, faster));

    private void SetSpeed(float target)
    {
        if (_player is not { } player || _playingChannel?.IsOnDemand != true) return;
        var steps = Motion.Enabled ? PlaybackSpeed.Ramp(_playbackRate, target) : [target];
        _playbackRate = target;
        ShowOsd(PlaybackSpeed.Label(target), target < 1f ? "Yavaş oynatma · ses perdesi korunur" : "");
        _rateRamp?.Stop();
        var index = 0;
        _rateRamp = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        _rateRamp.Tick += (_, _) =>
        {
            if (!ReferenceEquals(player, _player) || index >= steps.Length)
            {
                _rateRamp?.Stop();
                return;
            }
            player.SetRate(steps[index++]);
        };
        _rateRamp.Start();
    }

    /// <summary>E: shows the next frame and stays paused there.</summary>
    private void StepFrame()
    {
        if (_player is not { } player || _playingChannel?.IsOnDemand != true) return;
        player.NextFrame();
        ShowOsd("Kare kare", "E: sonraki kare · Boşluk: devam");
    }
}
