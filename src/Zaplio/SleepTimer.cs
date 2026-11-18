using System.Globalization;

namespace Zaplio;

internal enum SleepMode { Off, Minutes, EndOfItem }

/// <summary>
/// "Uyku zamanlayıcısı": stops playback after a number of minutes, when the movie or episode ends, or when the live
/// programme on air ends (from the guide). The sound fades out over the last <see cref="FadeSeconds"/> seconds.
/// </summary>
internal sealed class SleepTimer
{
    public const int FadeSeconds = 30;
    public static readonly int[] Choices = [15, 30, 45, 60, 90, 120];
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public SleepMode Mode { get; private set; }

    /// <summary>When playback stops; null for "when this ends" on a movie or episode (the end decides).</summary>
    public DateTimeOffset? Deadline { get; private set; }

    public bool IsOn => Mode != SleepMode.Off;

    public void SetMinutes(int minutes, DateTimeOffset now)
    {
        Mode = SleepMode.Minutes;
        Deadline = now.AddMinutes(minutes);
    }

    /// <param name="programmeEnd">For live TV the end of the programme on air; null for a movie or episode.</param>
    public void SetEndOfItem(DateTimeOffset? programmeEnd)
    {
        Mode = SleepMode.EndOfItem;
        Deadline = programmeEnd;
    }

    public void Cancel()
    {
        Mode = SleepMode.Off;
        Deadline = null;
    }

    public TimeSpan? Remaining(DateTimeOffset now) => Deadline is { } end ? end - now : null;

    public bool IsDue(DateTimeOffset now) => Deadline is { } end && end <= now;

    /// <summary>Volume factor: 1 until the fade starts, then down to 0 at the deadline.</summary>
    public double VolumeFactor(DateTimeOffset now) =>
        Remaining(now) is { } left && left.TotalSeconds < FadeSeconds ? Math.Clamp(left.TotalSeconds / FadeSeconds, 0, 1) : 1;

    /// <summary>Short text for the menu header and messages, e.g. "23 dk sonra", "22.30'da", "bitince".</summary>
    public string? Describe(DateTimeOffset now)
    {
        if (!IsOn) return null;
        if (Deadline is not { } end) return "bitince";
        if (Mode == SleepMode.EndOfItem) return Lang.F($"program bitince ({end.ToLocalTime().ToString("HH:mm", Lang.Culture)})");
        var minutes = (int)Math.Ceiling(Math.Max(0, (end - now).TotalMinutes));
        return minutes <= 1 ? Lang.T("1 dk içinde") : Lang.F($"{minutes} dk sonra");
    }
}
