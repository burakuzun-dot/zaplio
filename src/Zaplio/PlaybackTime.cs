using System.Globalization;

namespace Zaplio;

internal static class PlaybackTime
{
    internal static string Format(long milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture);
    }
}
