using System.Globalization;

namespace Zaplio;

internal enum HealthLevel { Measuring, Stable, Unsteady, Weak }

internal readonly record struct HealthReport(HealthLevel Level, double BitsPerSecond, int Samples, int StallSeconds,
    int DamagedSeconds, bool RecentReconnect);

/// <summary>
/// Judges how well the current stream is being delivered from one sample per second of VLC's input statistics.
/// UI-free: callers pass the clock (milliseconds, e.g. Environment.TickCount64), which keeps it unit-testable.
/// </summary>
internal sealed class StreamHealth
{
    public const int Window = 30;          // seconds of playback the verdict looks at
    private const int MinimumSamples = 3;
    private const long SeekGrace = 5_000;  // rebuffering right after a seek is not a network problem
    private const long StallMemory = 1_500;
    private const long ReconnectMemory = 60_000;
    private const int SilenceLimit = 8;    // live HLS fetches in bursts; only a long silence counts

    private readonly record struct Sample(bool Stalled, bool Damaged, long Bits);

    private readonly Queue<Sample> _samples = new();
    private long _readBytes = -1;
    private int _lostPictures = -1;
    private int _damage = -1;
    private int _silence;
    private long _lastStallAt;
    private long _lastReconnectAt;
    private long _lastSeekAt;

    /// <summary>A new stream starts: its counters must not be compared with the previous one's.</summary>
    public void StartStream(bool keepHistory)
    {
        _readBytes = -1;
        _lostPictures = -1;
        _damage = -1;
        _silence = 0;
        if (keepHistory) return; // a reconnect keeps the evidence that the connection dropped
        _samples.Clear();
        _lastStallAt = 0;
        _lastReconnectAt = 0;
        _lastSeekAt = 0;
    }

    public void MarkSeek(long now) => _lastSeekAt = now;

    public bool RecentlySought(long now) => _lastSeekAt > 0 && now - _lastSeekAt < SeekGrace;

    /// <summary>VLC reported buffering after playback had started, i.e. the picture froze.</summary>
    public void RecordStall(long now)
    {
        if (!RecentlySought(now)) _lastStallAt = now;
    }

    public void RecordReconnect(long now) => _lastReconnectAt = now;

    /// <param name="readBytes">VLC's cumulative input byte counter (32-bit; it may wrap).</param>
    /// <param name="onDemand">Films and archives are read ahead and then pause on purpose, so silence is ignored.</param>
    public void AddSample(long now, uint readBytes, int lostPictures, int damage, bool onDemand)
    {
        long bits = 0;
        if (_readBytes >= 0 && readBytes >= _readBytes) bits = (readBytes - _readBytes) * 8;
        _readBytes = readBytes;
        _silence = bits == 0 && !onDemand ? _silence + 1 : 0;

        var damaged = !RecentlySought(now) &&
                      ((_lostPictures >= 0 && lostPictures > _lostPictures) || (_damage >= 0 && damage > _damage));
        _lostPictures = lostPictures;
        _damage = damage;

        var stalled = _lastStallAt > 0 && now - _lastStallAt < StallMemory;
        _samples.Enqueue(new Sample(stalled, damaged, bits));
        while (_samples.Count > Window) _samples.Dequeue();
    }

    public HealthReport Evaluate(long now)
    {
        var recentReconnect = _lastReconnectAt > 0 && now - _lastReconnectAt < ReconnectMemory;
        if (_samples.Count < MinimumSamples)
            return new HealthReport(HealthLevel.Measuring, 0, _samples.Count, 0, 0, recentReconnect);

        var stalls = _samples.Count(sample => sample.Stalled);
        var damaged = _samples.Count(sample => sample.Damaged);
        var bitrate = _samples.TakeLast(5).Average(sample => (double)sample.Bits);
        var level = recentReconnect || stalls >= 3 || _silence >= SilenceLimit ? HealthLevel.Weak
            : stalls >= 1 || damaged >= 3 ? HealthLevel.Unsteady
            : HealthLevel.Stable;
        return new HealthReport(level, bitrate, _samples.Count, stalls, damaged, recentReconnect);
    }

    public static string FormatBits(double bitsPerSecond)
    {
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        return bitsPerSecond switch
        {
            >= 1_000_000 => $"{(bitsPerSecond / 1_000_000).ToString(bitsPerSecond >= 100_000_000 ? "0" : "0.0", turkish)} Mb/s",
            >= 1_000 => $"{(bitsPerSecond / 1_000).ToString("0", turkish)} kb/s",
            _ => $"{bitsPerSecond.ToString("0", turkish)} b/s"
        };
    }
}
