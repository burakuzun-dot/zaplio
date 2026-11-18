using Xunit;

namespace Zaplio.Tests;

public sealed class SleepTimerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Minutes_run_out_at_the_deadline()
    {
        var timer = new SleepTimer();
        Assert.False(timer.IsOn);
        timer.SetMinutes(30, Now);

        Assert.True(timer.IsOn);
        Assert.False(timer.IsDue(Now.AddMinutes(29)));
        Assert.True(timer.IsDue(Now.AddMinutes(30)));
        Assert.Equal("30 dk sonra", timer.Describe(Now));
        Assert.Equal("1 dk içinde", timer.Describe(Now.AddMinutes(29.5)));
    }

    [Fact]
    public void Sound_fades_only_in_the_last_half_minute()
    {
        var timer = new SleepTimer();
        timer.SetMinutes(15, Now);
        var end = Now.AddMinutes(15);

        Assert.Equal(1, timer.VolumeFactor(end.AddSeconds(-SleepTimer.FadeSeconds - 1)));
        Assert.Equal(0.5, timer.VolumeFactor(end.AddSeconds(-SleepTimer.FadeSeconds / 2.0)), 3);
        Assert.Equal(0, timer.VolumeFactor(end));
    }

    [Fact]
    public void When_this_ends_has_no_clock_for_movies_and_the_programme_end_for_live()
    {
        var timer = new SleepTimer();
        timer.SetEndOfItem(null);
        Assert.Equal(SleepMode.EndOfItem, timer.Mode);
        Assert.Null(timer.Deadline);
        Assert.False(timer.IsDue(Now.AddDays(1))); // the end of the video decides
        Assert.Equal(1, timer.VolumeFactor(Now));
        Assert.Equal("bitince", timer.Describe(Now));

        timer.SetEndOfItem(Now.AddMinutes(40));
        Assert.True(timer.IsDue(Now.AddMinutes(40)));
        Assert.StartsWith("program bitince (", timer.Describe(Now));
    }

    [Fact]
    public void Cancel_turns_it_off()
    {
        var timer = new SleepTimer();
        timer.SetMinutes(60, Now);
        timer.Cancel();
        Assert.False(timer.IsOn);
        Assert.False(timer.IsDue(Now.AddHours(2)));
        Assert.Null(timer.Describe(Now));
    }
}

public sealed class ChannelFailureTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_failed_channel_is_marked_for_a_day_and_cleared_when_it_opens()
    {
        var failures = new Dictionary<string, DateTimeOffset>();
        ChannelFailures.Record(failures, "k1", Now);

        Assert.Equal(Now, ChannelFailures.Recent(failures, "k1", Now.AddHours(23)));
        Assert.Null(ChannelFailures.Recent(failures, "k1", Now.AddHours(24)));
        Assert.Null(ChannelFailures.Recent(failures, "k2", Now));
        Assert.True(ChannelFailures.Clear(failures, "k1"));
        Assert.False(ChannelFailures.Clear(failures, "k1"));
    }

    [Fact]
    public void Old_marks_are_pruned()
    {
        var failures = new Dictionary<string, DateTimeOffset> { ["eski"] = Now.AddDays(-2), ["yeni"] = Now.AddHours(-1) };
        Assert.Equal(1, ChannelFailures.Prune(failures, Now));
        Assert.Equal(["yeni"], failures.Keys);
    }

    [Fact]
    public void Subscription_checks_after_failures_are_spaced_out_per_source()
    {
        var spacing = new CheckSpacing(TimeSpan.FromMinutes(2));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        Assert.True(spacing.TryStart(first, Now));
        Assert.False(spacing.TryStart(first, Now.AddSeconds(30))); // ten failing channels in a row: one request
        Assert.True(spacing.TryStart(second, Now.AddSeconds(30)));
        Assert.True(spacing.TryStart(first, Now.AddMinutes(2)));
    }
}

public sealed class ConnectionLimitTests
{
    private static AccountInfo Account(int? max, int? active) => new(DateTimeOffset.UtcNow, null, max, active, false, "Active");

    [Theory]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, true)]
    [InlineData(2, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(0, 0, false)]  // unlimited / unknown
    public void All_connections_in_use(int max, int active, bool full) => Assert.Equal(full, Account(max, active).AllConnectionsInUse);

    [Fact]
    public void Unknown_counts_never_claim_the_account_is_full()
    {
        Assert.False(Account(null, 5).AllConnectionsInUse);
        Assert.False(Account(2, null).AllConnectionsInUse);
        Assert.Null(Account(2, null).LimitMessage("Liste"));
    }

    [Fact]
    public void The_message_says_what_to_do()
    {
        Assert.Equal("Liste: hesabındaki 2 bağlantının hepsi kullanımda. Başka bir cihazda açık yayın varsa kapatıp tekrar dene.",
            Account(2, 2).LimitMessage("Liste"));
        Assert.StartsWith("Liste: hesabının tek bağlantısı başka bir yerde kullanımda.", Account(1, 1).LimitMessage("Liste"));
    }
}
