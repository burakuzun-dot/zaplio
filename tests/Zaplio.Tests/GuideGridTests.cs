using Xunit;

namespace Zaplio.Tests;

public sealed class GuideGridTests
{
    private static readonly DateTimeOffset From = new(2026, 11, 16, 18, 0, 0, TimeSpan.FromHours(3));
    private static readonly DateTimeOffset To = From.AddHours(26);
    private static readonly DateTimeOffset Now = From.AddHours(2).AddMinutes(10);

    private static GuideProgramme Programme(double startHours, double? endHours, string title) =>
        new(From.AddHours(startHours), endHours is { } end ? From.AddHours(end) : null, title, "");

    [Fact]
    public void Cells_are_placed_by_time_and_marked_past_on_air_or_upcoming()
    {
        var cells = GuideGrid.Cells([Programme(0, 1, "Geçmiş"), Programme(2, 3, "Şimdi"), Programme(3, 4.5, "Sonra")], From, To, Now);

        Assert.Equal(3, cells.Count);
        Assert.Equal(0, cells[0].Left);
        Assert.Equal(60 * GuideGrid.PixelsPerMinute - 2, cells[0].Width);
        Assert.True(cells[0].IsPast);
        Assert.False(cells[0].IsOnAir);
        Assert.Equal(120 * GuideGrid.PixelsPerMinute, cells[1].Left);
        Assert.True(cells[1].IsOnAir);
        Assert.False(cells[1].IsPast);
        Assert.False(cells[2].IsOnAir || cells[2].IsPast);
        Assert.Equal(90 * GuideGrid.PixelsPerMinute - 2, cells[2].Width);
    }

    [Fact]
    public void Programmes_are_clipped_to_the_window_and_those_outside_are_dropped()
    {
        var cells = GuideGrid.Cells([Programme(-5, -4, "Çok eski"), Programme(-1, 1, "Başı dışarıda"), Programme(25, 30, "Sonu dışarıda"),
            Programme(40, 41, "Çok ileride")], From, To, Now);

        Assert.Equal(["Başı dışarıda", "Sonu dışarıda"], cells.Select(cell => cell.Title));
        Assert.Equal(0, cells[0].Left);
        Assert.Equal(60 * GuideGrid.PixelsPerMinute - 2, cells[0].Width);
        Assert.Equal(GuideGrid.Width(From, To), cells[1].Left + cells[1].Width + 2);
    }

    [Fact]
    public void A_programme_without_an_end_runs_until_the_next_one_or_half_an_hour()
    {
        var cells = GuideGrid.Cells([Programme(1, null, "Açık uçlu"), Programme(2.5, null, "Son")], From, To, Now);

        Assert.Equal(90 * GuideGrid.PixelsPerMinute - 2, cells[0].Width);
        Assert.Equal(30 * GuideGrid.PixelsPerMinute - 2, cells[1].Width);
    }

    [Fact]
    public void Window_starts_two_hours_before_the_current_hour_and_covers_a_day_ahead()
    {
        var (from, to) = GuideGrid.Window(new DateTimeOffset(2026, 11, 16, 20, 47, 13, TimeSpan.FromHours(3)));

        Assert.Equal(0, from.Minute);
        Assert.Equal(TimeSpan.FromHours(26), to - from);
        Assert.Equal(26 * 60 * GuideGrid.PixelsPerMinute, GuideGrid.Width(from, to));
    }

    [Fact]
    public void One_ruler_mark_per_hour()
    {
        var marks = GuideGrid.HourMarks(From, To);

        Assert.Equal(26, marks.Count);
        Assert.Equal(0, marks[0].Left);
        Assert.Equal(60 * GuideGrid.PixelsPerMinute, marks[1].Left);
        Assert.Contains(":00", marks[0].Label);
    }
}
