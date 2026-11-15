using System.Globalization;
using System.Windows.Data;

namespace Zaplio;

/// <summary>
/// Lets channel rows ask the main window whether a channel is a favourite without putting that flag on the catalog record
/// (favourites live in the user state and also cover quality variants).
/// </summary>
internal sealed class ChannelFavoriteConverter : IValueConverter
{
    public static ChannelFavoriteConverter Instance { get; } = new();

    /// <summary>Set by the main window once the state is loaded.</summary>
    public static Func<Channel, bool>? Lookup { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Channel channel && Lookup?.Invoke(channel) == true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
