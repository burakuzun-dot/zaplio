using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Zaplio;

/// <summary>An entry of the movie / series list: a poster tile at the top level, a plain row for the episodes of a series.</summary>
internal interface IVodListItem
{
    bool IsTile { get; }
}

/// <summary>Chooses the poster tile or the row template for an <see cref="IVodListItem"/>.</summary>
public sealed class VodTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Tile { get; set; }
    public DataTemplate? Row { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is IVodListItem { IsTile: true } ? Tile : Row;
}

/// <summary>
/// Rounded clip for content whose size is only known at layout time (posters that stretch with their tile):
/// bind <c>ActualWidth</c> and <c>ActualHeight</c>, pass the corner radius as the parameter.
/// </summary>
public sealed class RoundedClipConverter : IMultiValueConverter
{
    public static RoundedClipConverter Instance { get; } = new();

    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double width || values[1] is not double height || width <= 0 || height <= 0) return null;
        var radius = parameter is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
        clip.Freeze();
        return clip;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
