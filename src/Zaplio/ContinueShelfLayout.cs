using System.ComponentModel;

namespace Zaplio;

/// <summary>
/// Sizes of the "continue watching" cards. Above the list they keep a fixed size; under the player they are fitted to the
/// panel: equal columns across its width, as many rows as its height allows (further rows scroll), and the cards grow to
/// fill the height when they all fit.
/// The item template binds to this object through the list's <c>Tag</c>.
/// </summary>
internal sealed class ContinueShelfLayout : INotifyPropertyChanged
{
    internal const double Gap = 8;
    internal const double MinimumCardWidth = 220;
    internal const double MaximumCardWidth = 340;
    internal const double MinimumCardHeight = 86;
    internal const double MaximumCardHeight = 140;
    private const double CardChrome = 16; // padding and border around the poster
    private const double PosterAspect = 2.0 / 3;

    public double CardWidth { get; private set; } = 236;
    public double CardHeight { get; private set; } = MinimumCardHeight;
    public double PosterWidth { get; private set; } = 48;
    public double PosterHeight { get; private set; } = 70;
    internal int Columns { get; private set; } = 1;
    internal int Rows { get; private set; } = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The fixed size used above the list.</summary>
    internal void UseDefault() => Apply(1, 1, 236, MinimumCardHeight);

    /// <param name="width">Width available for the cards.</param>
    /// <param name="height">Height available for the cards (without the heading).</param>
    /// <param name="count">How many cards there are.</param>
    internal void Fit(double width, double height, int count)
    {
        if (!double.IsFinite(width) || width <= 0) return;
        count = Math.Max(1, count);
        // Every card carries its gap as a right and bottom margin, so a column or row takes card + gap.
        var fit = Math.Max(1, (int)Math.Floor(width / (MinimumCardWidth + Gap)));
        var columns = Math.Min(fit, count);
        var cardWidth = Math.Min(MaximumCardWidth, Math.Floor(width / columns - Gap));

        var needed = (int)Math.Ceiling(count / (double)columns);
        var room = double.IsFinite(height) ? Math.Max(1, (int)Math.Floor(height / (MinimumCardHeight + Gap))) : 1;
        var rows = Math.Min(needed, room);
        var cardHeight = double.IsFinite(height)
            ? Math.Clamp(Math.Floor(height / rows - Gap), MinimumCardHeight, MaximumCardHeight)
            : MinimumCardHeight;
        Apply(columns, rows, cardWidth, cardHeight);
    }

    private void Apply(int columns, int rows, double cardWidth, double cardHeight)
    {
        var posterHeight = cardHeight - CardChrome;
        // The title and "continue · time" need the room more than a big poster does.
        var posterWidth = Math.Min(Math.Round(posterHeight * PosterAspect), Math.Round(cardWidth * 0.3));
        if (Columns == columns && Rows == rows && CardWidth.Equals(cardWidth) && CardHeight.Equals(cardHeight)) return;
        Columns = columns;
        Rows = rows;
        CardWidth = cardWidth;
        CardHeight = cardHeight;
        PosterWidth = posterWidth;
        PosterHeight = posterHeight;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
