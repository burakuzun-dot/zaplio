using Xunit;

namespace Zaplio.Tests;

public sealed class SpaceKeyTests
{
    [Fact]
    public void TypingKeepsTheSpace() =>
        Assert.Equal(SpaceAction.PassThrough, SpaceKey.Decide(typing: true, repeat: false, hasPlayer: true, canPause: true, canResumeLast: true));

    [Fact]
    public void WithAPlayerSpaceAlwaysPausesOrResumes() =>
        Assert.Equal(SpaceAction.TogglePause, SpaceKey.Decide(typing: false, repeat: false, hasPlayer: true, canPause: true, canResumeLast: false));

    [Fact]
    public void HoldingSpaceDoesNotFlicker() =>
        Assert.Equal(SpaceAction.Ignore, SpaceKey.Decide(typing: false, repeat: true, hasPlayer: true, canPause: true, canResumeLast: false));

    [Fact]
    public void AStreamThatCannotPauseSaysSoInsteadOfPressingAButton() =>
        Assert.Equal(SpaceAction.CannotPause, SpaceKey.Decide(typing: false, repeat: false, hasPlayer: true, canPause: false, canResumeLast: false));

    [Fact]
    public void AfterStoppingSpaceStartsTheLastItemAgain() =>
        Assert.Equal(SpaceAction.ResumeLast, SpaceKey.Decide(typing: false, repeat: false, hasPlayer: false, canPause: false, canResumeLast: true));

    [Fact]
    public void WithNothingToPlayTheFocusedControlKeepsIt() =>
        Assert.Equal(SpaceAction.PassThrough, SpaceKey.Decide(typing: false, repeat: false, hasPlayer: false, canPause: false, canResumeLast: false));
}
