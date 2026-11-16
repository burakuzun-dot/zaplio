namespace Zaplio;

/// <summary>
/// How often a drag on the timeline really moves the video. A file on this computer follows the thumb almost frame by frame;
/// a stream from a provider moves at most every half second and only for a real jump, so a drag does not turn into a burst
/// of requests to the provider.
/// </summary>
internal sealed class ScrubThrottle
{
    internal const int LocalInterval = 70;
    internal const int StreamInterval = 450;
    internal const long StreamMinimumJump = 1500;

    private int _interval;
    private long _minimumJump;
    private long _lastTick;
    private long _lastTarget;

    internal void Begin(bool local, long now)
    {
        _interval = local ? LocalInterval : StreamInterval;
        _minimumJump = local ? 0 : StreamMinimumJump;
        _lastTick = now - _interval; // the first move is shown at once
        _lastTarget = -1;
    }

    /// <summary>True when the video should jump to <paramref name="target"/> (milliseconds) now.</summary>
    internal bool ShouldSeek(long target, long now)
    {
        if (target == _lastTarget || now - _lastTick < _interval) return false;
        if (_lastTarget >= 0 && Math.Abs(target - _lastTarget) < _minimumJump) return false;
        _lastTick = now;
        _lastTarget = target;
        return true;
    }
}

/// <summary>
/// The player reports its time in coarse steps; between two reports the timeline moves on by the clock, so the thumb and the
/// elapsed time glide instead of jumping. It never runs more than a second ahead of the last report.
/// </summary>
internal sealed class SmoothPlaybackClock
{
    private const long MaximumLead = 1000;

    private long _reported = -1;
    private long _reportedAt;

    internal void Reset() => _reported = -1;

    /// <param name="reported">The player's time in milliseconds.</param>
    /// <param name="now">A monotonic clock in milliseconds.</param>
    internal long Time(long reported, bool playing, float rate, long now)
    {
        if (reported != _reported)
        {
            _reported = reported;
            _reportedAt = now;
        }
        if (!playing || reported < 0) return Math.Max(0, reported);
        var lead = (long)((now - _reportedAt) * Math.Clamp(rate, 0.25f, 4f));
        return reported + Math.Clamp(lead, 0, MaximumLead);
    }
}
