using System.Diagnostics;
using LibVLCSharp.Shared;

namespace Zaplio;

/// <summary>
/// Writes how long pausing, resuming and seeking take to the log, so a slow one can be traced from the log alone
/// (it once was the "Kayda hazır" copy making movies slow to pause and seek). Only the latest request is followed: a
/// newer one, or another player, ends the measurement of the previous one.
/// </summary>
public partial class MainWindow
{
    private const int TimingLimitMs = 30_000;
    private int _timingRevision;

    /// <summary>Call right after <c>SetPause</c>: done when the player is paused, or playing and its clock moved on again.</summary>
    private void TimePause(MediaPlayer player, bool pausing)
    {
        var from = player.Time;
        Follow(player, pausing ? "Duraklatma" : "Devam",
            pausing
                ? current => current.State == VLCState.Paused
                : current => current.State == VLCState.Playing && current.Time > from + 150);
    }

    /// <summary>Call right after setting <c>Time</c>: done when the picture is at the new place (and moving, unless paused).</summary>
    private void TimeSeek(MediaPlayer player, long target)
    {
        var from = player.Time;
        Follow(player, $"Sarma ({PlaybackTime.Format(from)} → {PlaybackTime.Format(target)})", current =>
            current.State == VLCState.Paused
                ? Math.Abs(current.Time - target) < 2_000
                : current.Time > target + 150 && current.Time < target + 6_000);
    }

    /// <summary>
    /// Checks every 25 ms on the UI thread. Players are released on the UI thread only after <c>_player</c> is cleared,
    /// so checking that it is still the current player first means a released one is never touched.
    /// </summary>
    private async void Follow(MediaPlayer player, string what, Func<MediaPlayer, bool> done)
    {
        var mine = ++_timingRevision;
        var name = _playingChannel?.Name;
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < TimingLimitMs)
        {
            if (_timingRevision != mine || _closing || !ReferenceEquals(_player, player)) return; // superseded or gone
            if (done(player))
            {
                AppLog.Info($"{what}: {name} · {clock.ElapsedMilliseconds} ms");
                return;
            }
            await Task.Delay(25);
        }
        if (_timingRevision == mine) AppLog.Warn($"{what} {TimingLimitMs / 1000} saniyede tamamlanmadı: {name}");
    }
}
