using System.Windows.Controls;
using System.Windows.Threading;

namespace Zaplio;

/// <summary>"Uyku zamanlayıcısı" in the player's Seçenekler menu (see <see cref="SleepTimer"/>).</summary>
public partial class MainWindow
{
    private readonly SleepTimer _sleep = new();
    private readonly DispatcherTimer _sleepTick = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _sleepWarned;
    private bool _sleepTickHooked;

    private MenuItem BuildSleepMenu()
    {
        var now = DateTimeOffset.UtcNow;
        var group = new MenuItem { Header = _sleep.IsOn ? Lang.F($"Uyku zamanlayıcısı: {_sleep.Describe(now)}") : Lang.T("Uyku zamanlayıcısı") };
        Add(Lang.T("Kapalı"), !_sleep.IsOn, CancelSleep);
        foreach (var minutes in SleepTimer.Choices)
            Add(minutes < 60 ? $"{minutes} dakika" : minutes % 60 == 0 ? $"{minutes / 60} saat" : $"{minutes / 60} saat {minutes % 60} dakika",
                false, () => StartSleep(minutes));
        if (_playingChannel is { IsOnDemand: true } item && !LocalMedia.IsLocal(item) || _playingChannel is { } local && LocalMedia.IsLocal(local))
            Add(_playingChannel?.SeriesId > 0 ? Lang.T("Bu bölüm bitince") : Lang.T("Bu video bitince"),
                _sleep.Mode == SleepMode.EndOfItem && _sleep.Deadline is null, () => StartSleepAtEnd(null));
        else if (CurrentProgrammeEnd(now) is { } end)
            Add(Lang.F($"Program bitince ({end.ToLocalTime():HH\\:mm})"), _sleep.Mode == SleepMode.EndOfItem && _sleep.Deadline == end,
                () => StartSleepAtEnd(end));
        return group;

        void Add(string text, bool isChecked, Action select)
        {
            var option = new MenuItem { Header = text, IsCheckable = true, IsChecked = isChecked };
            option.Click += (_, _) => select();
            group.Items.Add(option);
        }
    }

    /// <summary>End of the live programme on air now, from the guide (null when the guide does not know it).</summary>
    private DateTimeOffset? CurrentProgrammeEnd(DateTimeOffset now) =>
        PlayingLiveChannel is { } channel
            ? (_guideRequests.GetValueOrDefault(channel.SourceId)?.Guide?.ForChannel(channel) ?? [])
                .LastOrDefault(programme => programme.IsOnAir(now))?.End
            : null;

    private void StartSleep(int minutes)
    {
        _sleep.SetMinutes(minutes, DateTimeOffset.UtcNow);
        BeginSleep(Lang.F($"{minutes} dakika sonra oynatma yavaşça durur."));
    }

    private void StartSleepAtEnd(DateTimeOffset? programmeEnd)
    {
        _sleep.SetEndOfItem(programmeEnd);
        BeginSleep(programmeEnd is { } end
            ? Lang.F($"Program bitince ({end.ToLocalTime():HH\\:mm}) oynatma durur.")
            : Lang.T("Bitince oynatma durur; sıradaki başlamaz."));
    }

    private void BeginSleep(string message)
    {
        _sleepWarned = false;
        if (!_sleepTickHooked)
        {
            _sleepTick.Tick += (_, _) => TickSleep();
            _sleepTickHooked = true;
        }
        _sleepTick.Start();
        RestoreVolume();
        AppLog.Info($"Uyku zamanlayıcısı kuruldu: {_sleep.Describe(DateTimeOffset.UtcNow)}");
        ShowOsd(Lang.T("Uyku zamanlayıcısı"), message);
    }

    private void CancelSleep()
    {
        if (!_sleep.IsOn) return;
        _sleep.Cancel();
        _sleepTick.Stop();
        RestoreVolume();
        ShowOsd(Lang.T("Uyku zamanlayıcısı"), Lang.T("Kapatıldı"));
    }

    private void TickSleep()
    {
        var now = DateTimeOffset.UtcNow;
        if (!_sleep.IsOn || _closing)
        {
            _sleepTick.Stop();
            return;
        }
        if (_sleep.IsDue(now))
        {
            _ = SleepNowAsync();
            return;
        }
        if (_player is { } player && !_muted) player.Volume = (int)Math.Round(_state.Volume * _sleep.VolumeFactor(now));
        if (!_sleepWarned && _sleep.Remaining(now) is { TotalSeconds: <= 60 })
        {
            _sleepWarned = true;
            ShowOsd(Lang.T("Uyku zamanlayıcısı"), Lang.T("1 dakika sonra duracak · kapatmak için Seçenekler › Uyku zamanlayıcısı"));
        }
    }

    /// <summary>Stops playback for the night. A movie or episode keeps its place (saved when the player is released).</summary>
    private async Task SleepNowAsync()
    {
        _sleep.Cancel();
        _sleepTick.Stop();
        AppLog.Info("Uyku zamanlayıcısı oynatmayı durdurdu.");
        if (_player is not null) await StopPlaybackAsync(Lang.T("Uyku zamanlayıcısı durdurdu · iyi geceler"));
        RestoreVolume();
    }

    /// <summary>True when a "when this ends" timer was waiting for this end: playback stops instead of going on.</summary>
    private bool SleepAtThisEnd() => _sleep.Mode == SleepMode.EndOfItem && _sleep.Deadline is null;

    private void RestoreVolume()
    {
        if (_player is { } player) player.Volume = _muted ? 0 : _state.Volume;
    }
}
