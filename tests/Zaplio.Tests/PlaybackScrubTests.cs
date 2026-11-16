using Xunit;

namespace Zaplio.Tests;

public sealed class PlaybackScrubTests
{
    [Fact]
    public void ALocalFileFollowsTheThumbClosely()
    {
        var throttle = new ScrubThrottle();
        throttle.Begin(local: true, now: 1000);

        Assert.True(throttle.ShouldSeek(5_000, 1000));
        Assert.False(throttle.ShouldSeek(5_200, 1030)); // too soon
        Assert.True(throttle.ShouldSeek(5_200, 1000 + ScrubThrottle.LocalInterval));
        Assert.False(throttle.ShouldSeek(5_200, 2000)); // same place
    }

    [Fact]
    public void AStreamMovesRarelyAndOnlyForRealJumps()
    {
        var throttle = new ScrubThrottle();
        throttle.Begin(local: false, now: 0);
        var sent = 0;
        for (long now = 0, target = 10_000; now < 2_000; now += 16, target += 200) // a two-second drag at 60 events a second
            if (throttle.ShouldSeek(target, now)) sent++;

        Assert.InRange(sent, 1, 5);
        Assert.True(throttle.ShouldSeek(60_000, 5_000));
        Assert.False(throttle.ShouldSeek(60_500, 6_000)); // under the minimum jump
    }

    [Fact]
    public void TheClockGlidesBetweenReportsButNotFarAhead()
    {
        var clock = new SmoothPlaybackClock();

        Assert.Equal(10_000, clock.Time(10_000, playing: true, rate: 1, now: 0));
        Assert.Equal(10_100, clock.Time(10_000, playing: true, rate: 1, now: 100));
        Assert.Equal(10_200, clock.Time(10_000, playing: true, rate: 2, now: 100));
        Assert.Equal(11_000, clock.Time(10_000, playing: true, rate: 1, now: 5_000)); // capped at one second ahead
        Assert.Equal(10_250, clock.Time(10_250, playing: true, rate: 1, now: 5_100)); // a new report takes over
    }

    [Fact]
    public void APausedPlayerShowsItsOwnTime()
    {
        var clock = new SmoothPlaybackClock();
        clock.Time(7_000, playing: true, rate: 1, now: 0);
        Assert.Equal(7_000, clock.Time(7_000, playing: false, rate: 1, now: 900));
        Assert.Equal(0, clock.Time(-1, playing: true, rate: 1, now: 900));
    }
}
