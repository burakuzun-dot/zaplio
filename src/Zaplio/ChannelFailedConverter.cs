using System.Globalization;
using System.Windows.Data;

namespace Zaplio;

/// <summary>
/// Lets a channel row ask whether the channel would not open lately. Bound together with the main window's
/// <c>FailureMarks</c> counter, so rows re-check when a mark is added or removed without rebuilding the list.
/// With <c>ConverterParameter=text</c> it gives the tooltip, otherwise true/false.
/// </summary>
internal sealed class ChannelFailedConverter : IMultiValueConverter
{
    public static ChannelFailedConverter Instance { get; } = new();

    /// <summary>Set by the main window once the state is loaded.</summary>
    public static Func<Channel, DateTimeOffset?>? Lookup { get; set; }

    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var failedAt = values.Length > 0 && values[0] is Channel channel ? Lookup?.Invoke(channel) : null;
        if (parameter as string != "text") return failedAt is not null;
        return failedAt is { } at
            ? Lang.F($"Son denemede açılmadı ({at.ToLocalTime().ToString("HH:mm", Lang.Culture)}). Açılınca bu işaret kalkar.")
            : null;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => [];
}
