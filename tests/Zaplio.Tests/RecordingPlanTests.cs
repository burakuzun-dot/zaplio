using Xunit;

namespace Zaplio.Tests;

public sealed class RecordingPlanTests
{
    private static readonly DateTime Moment = new(2026, 11, 17, 10, 30, 5);

    [Fact]
    public void FileNameCarriesTheTitleAndTheTime()
    {
        Assert.Equal("Zaplio_Kanal A_20261117_103005.mkv", RecordingPlan.FileName("Kanal A", Moment));
    }

    [Fact]
    public void FileNameDropsCharactersWindowsDoesNotAllow()
    {
        var name = RecordingPlan.FileName("Belgesel: 1/2 \"özel\" * 'son'?", Moment);
        Assert.Equal("Zaplio_Belgesel 1 2 özel son_20261117_103005.mkv", name);
    }

    [Fact]
    public void LongOrEmptyTitlesStillGiveAName()
    {
        Assert.True(RecordingPlan.FileName(new string('a', 200), Moment).Length < 100);
        Assert.Equal("Zaplio_20261117_103005.mkv", RecordingPlan.FileName("  ", Moment));
    }

    [Fact]
    public void StreamOutputCopiesWithoutReencoding()
    {
        var option = RecordingPlan.StreamOutput(@"C:\Users\O'Neil\Videolar\Zaplio\kayıt.mkv");

        Assert.Equal(":sout=#duplicate{dst=display,dst=std{access=file,mux=mkv,dst=\"C:/Users/O'Neil/Videolar/Zaplio/kayıt.mkv\"}}", option);
        Assert.DoesNotContain("transcode", option);
    }

    [Fact]
    public void ALocalClipIsCopiedWithoutAPictureBetweenTwoTimes()
    {
        var options = RecordingPlan.ClipOptions(@"C:\v\kayıt.mkv", 61_500, 90_250);

        Assert.Equal([":sout=#std{access=file,mux=mkv,dst=\"C:/v/kayıt.mkv\"}", ":start-time=61.5", ":stop-time=90.25"], options);
        Assert.DoesNotContain(options, option => option.Contains("display") || option.Contains("transcode"));
    }

    [Fact]
    public void TheReadyCopyStaysOnThisComputerUnderASecretPath()
    {
        var option = RecordingPlan.TapOutput(53111, "abc123");

        Assert.Equal(":sout=#duplicate{dst=display,dst=std{access=http,mux=ts,dst=127.0.0.1:53111/abc123.ts}}", option);
        Assert.DoesNotContain("transcode", option);
        Assert.InRange(RecordingPlan.FreeLoopbackPort(), 1024, 65535);
    }

    [Fact]
    public void TheFinishedStreamIsCopiedIntoMatroskaUnchanged() =>
        Assert.Equal(":sout=#std{access=file,mux=mkv,dst=\"C:/v/kayıt.mkv\"}", RecordingPlan.MkvOutput(@"C:\v\kayıt.mkv"));

    [Fact]
    public void RecordReadyIsOnByDefault() => Assert.True(new PlaybackSettings().RecordReady);

    [Fact]
    public void LaterPartsGetTheirOwnFile()
    {
        Assert.Equal(@"C:\v\kayıt.mkv", RecordingPlan.PartPath(@"C:\v\kayıt.mkv", 1));
        Assert.Equal(@"C:\v\kayıt_2.mkv", RecordingPlan.PartPath(@"C:\v\kayıt.mkv", 2));
    }

    [Fact]
    public void StartTimeIsInSeconds() => Assert.Equal(":start-time=83.5", RecordingPlan.StartTime(83_500));

    [Theory]
    [InlineData(1f, false, 0.85f)]
    [InlineData(1f, true, 1.1f)]
    [InlineData(0.25f, false, 0.25f)]
    [InlineData(3f, true, 3f)]
    [InlineData(0.7f, false, 0.6f)]
    [InlineData(0.7f, true, 0.75f)]
    public void SpeedStepsAlongTheLadder(float current, bool faster, float expected) =>
        Assert.Equal(expected, PlaybackSpeed.Step(current, faster));

    [Fact]
    public void SpeedLabelsAreTurkish()
    {
        Assert.Equal("Normal hız", PlaybackSpeed.Label(1f));
        Assert.Equal("0,5×", PlaybackSpeed.Label(0.5f));
    }

    [Fact]
    public void RampEndsExactlyOnTheTarget()
    {
        var ramp = PlaybackSpeed.Ramp(1f, 0.5f);
        Assert.Equal(5, ramp.Length);
        Assert.Equal(0.5f, ramp[^1]);
        Assert.True(ramp.Zip(ramp.Skip(1)).All(pair => pair.First > pair.Second));
        Assert.Equal([2f], PlaybackSpeed.Ramp(2f, 2f));
    }
}
