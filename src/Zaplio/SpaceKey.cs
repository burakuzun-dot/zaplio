namespace Zaplio;

/// <summary>What the space bar does. It belongs to play/pause before anything else, wherever the focus is;
/// only typing into a text box keeps it (a space is a character there).</summary>
internal enum SpaceAction
{
    /// <summary>Let the focused control have it (typing, or nothing to play).</summary>
    PassThrough,
    /// <summary>Held down: ignore the repeats so one press is one toggle.</summary>
    Ignore,
    TogglePause,
    /// <summary>Nothing is playing but something was: start it again (on-demand resumes where it stopped).</summary>
    ResumeLast,
    /// <summary>A stream that cannot pause; say so instead of pressing a focused button.</summary>
    CannotPause
}

internal static class SpaceKey
{
    internal static SpaceAction Decide(bool typing, bool repeat, bool hasPlayer, bool canPause, bool canResumeLast)
    {
        if (typing) return SpaceAction.PassThrough;
        if (!hasPlayer) return canResumeLast ? (repeat ? SpaceAction.Ignore : SpaceAction.ResumeLast) : SpaceAction.PassThrough;
        if (repeat) return SpaceAction.Ignore;
        return canPause ? SpaceAction.TogglePause : SpaceAction.CannotPause;
    }
}
