using System.Globalization;

namespace Zaplio;

/// <summary>Geometry of the grid programme guide: channels are rows, time runs left to right.</summary>
internal static class GuideGrid
{
    /// <summary>Width of one minute; an hour is 240 px wide.</summary>
    internal const double PixelsPerMinute = 4;

    private const double Gap = 2;
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <param name="Left">Distance from the start of the time window.</param>
    /// <param name="IsPast">Ended before now (playable from the archive when the channel keeps one).</param>
    internal sealed record Cell(double Left, double Width, string Title, string Time, string Description, bool IsOnAir, bool IsPast,
        DateTimeOffset Start, DateTimeOffset? End);

    /// <summary>The time window shown: from two hours before the current hour (recent, archive) to a day ahead.</summary>
    internal static (DateTimeOffset From, DateTimeOffset To) Window(DateTimeOffset now)
    {
        var local = now.ToLocalTime();
        var hour = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
        return (hour.AddHours(-2), hour.AddHours(24));
    }

    internal static double Offset(DateTimeOffset time, DateTimeOffset from) => (time - from).TotalMinutes * PixelsPerMinute;

    internal static double Width(DateTimeOffset from, DateTimeOffset to) => Offset(to, from);

    /// <summary>
    /// Cells of one channel inside the window. Programmes are clipped to the window; one without an end time runs until the
    /// next programme, or for half an hour when it is the last.
    /// </summary>
    internal static IReadOnlyList<Cell> Cells(IReadOnlyList<GuideProgramme> programmes, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        var cells = new List<Cell>();
        for (var index = 0; index < programmes.Count; index++)
        {
            var programme = programmes[index];
            var end = programme.End ?? (index + 1 < programmes.Count && programmes[index + 1].Start > programme.Start
                ? programmes[index + 1].Start : programme.Start.AddMinutes(30));
            if (end <= from || programme.Start >= to || end <= programme.Start) continue;
            var start = programme.Start < from ? from : programme.Start;
            var stop = end > to ? to : end;
            var width = Math.Max(Gap * 2, Offset(stop, start) - Gap);
            var time = $"{programme.Start.LocalDateTime.ToString("HH:mm", Lang.Culture)} – {end.LocalDateTime.ToString("HH:mm", Lang.Culture)}";
            cells.Add(new Cell(Offset(start, from), width, programme.Title, time, programme.Description,
                programme.Start <= now && end > now, end <= now, programme.Start, programme.End ?? end));
        }
        return cells;
    }

    /// <summary>Hour labels for the ruler ("21:00"; midnight carries the day: "16 Kas 00:00").</summary>
    internal static IReadOnlyList<(double Left, string Label)> HourMarks(DateTimeOffset from, DateTimeOffset to)
    {
        var marks = new List<(double, string)>();
        for (var hour = from; hour < to; hour = hour.AddHours(1))
        {
            var local = hour.LocalDateTime;
            marks.Add((Offset(hour, from), local.ToString(local.Hour == 0 ? "d MMM HH:mm" : "HH:mm", Turkish)));
        }
        return marks;
    }
}
