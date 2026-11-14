using Xunit;

namespace Zaplio.Tests;

public sealed class StreamHealthTests
{
    private readonly StreamHealth _health = new();
    private long _now = 1_000;
    private uint _bytes;

    public StreamHealthTests() => _health.StartStream(keepHistory: false);

    /// <summary>Feeds one sample per second.</summary>
    private void Play(int seconds, uint bytesPerSecond = 500_000, bool onDemand = false, int lost = 0, int damage = 0)
    {
        for (var i = 0; i < seconds; i++)
        {
            _now += 1_000;
            _bytes += bytesPerSecond;
            _health.AddSample(_now, _bytes, lost, damage, onDemand);
        }
    }

    private HealthLevel Level => _health.Evaluate(_now).Level;

    [Fact]
    public void Needs_a_few_seconds_before_judging()
    {
        Play(2);
        Assert.Equal(HealthLevel.Measuring, Level);
    }

    [Fact]
    public void Steady_stream_is_stable_and_reports_bitrate()
    {
        Play(10);
        var report = _health.Evaluate(_now);
        Assert.Equal(HealthLevel.Stable, report.Level);
        Assert.Equal(4_000_000, report.BitsPerSecond); // 500 kB/s
    }

    [Fact]
    public void One_stall_is_unsteady_and_three_are_weak()
    {
        Play(5);
        _health.RecordStall(_now + 500);
        Play(1);
        Assert.Equal(HealthLevel.Unsteady, Level);

        for (var i = 0; i < 2; i++)
        {
            _health.RecordStall(_now + 500);
            Play(1);
        }
        Assert.Equal(HealthLevel.Weak, Level);
    }

    [Fact]
    public void Rebuffering_after_a_seek_is_ignored()
    {
        Play(5);
        _health.MarkSeek(_now);
        _health.RecordStall(_now + 500);
        Play(1, lost: 5, damage: 3);
        Assert.Equal(HealthLevel.Stable, Level);
    }

    [Fact]
    public void Long_silence_is_weak_for_live_but_normal_for_films()
    {
        Play(5);
        Play(8, bytesPerSecond: 0);
        Assert.Equal(HealthLevel.Weak, Level);

        var film = new StreamHealth();
        film.StartStream(keepHistory: false);
        uint bytes = 0;
        for (var second = 1; second <= 13; second++)
        {
            if (second <= 5) bytes += 500_000; // read ahead, then VLC's buffer is full
            film.AddSample(second * 1_000, bytes, 0, 0, onDemand: true);
        }
        Assert.Equal(HealthLevel.Stable, film.Evaluate(13_000).Level);
    }

    [Fact]
    public void Damaged_pictures_make_it_unsteady()
    {
        Play(5);
        for (var lost = 1; lost <= 3; lost++) Play(1, lost: lost);
        Assert.Equal(HealthLevel.Unsteady, Level);
    }

    [Fact]
    public void A_reconnect_is_weak_for_a_minute()
    {
        Play(5);
        _health.RecordReconnect(_now);
        Assert.Equal(HealthLevel.Weak, Level);
        _health.StartStream(keepHistory: true);
        Play(61);
        Assert.Equal(HealthLevel.Stable, Level);
    }

    [Fact]
    public void Counter_wrap_does_not_throw_or_count_as_traffic()
    {
        _health.AddSample(1_000, uint.MaxValue - 10, 0, 0, false);
        _health.AddSample(2_000, 5, 0, 0, false);
        _health.AddSample(3_000, 500_005, 0, 0, false);
        var report = _health.Evaluate(3_000);
        Assert.Equal(HealthLevel.Stable, report.Level);
    }

    [Theory]
    [InlineData(66_000_000, "66,0 Mb/s")]
    [InlineData(150_000_000, "150 Mb/s")]
    [InlineData(555_000, "555 kb/s")]
    [InlineData(800, "800 b/s")]
    public void Formats_speeds_in_turkish(double bits, string expected) => Assert.Equal(expected, StreamHealth.FormatBits(bits));
}
