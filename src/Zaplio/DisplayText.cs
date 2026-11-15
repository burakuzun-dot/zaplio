using System.Globalization;
using System.Windows.Data;

namespace Zaplio;

/// <summary>Cleans provider-supplied names for display only; matching and storage keep the original text.</summary>
internal static class DisplayText
{
    /// <summary>Drops decorative symbols a provider puts in front of a category name (arrows, stars, emoji, bars).</summary>
    internal static string CleanLabel(string label)
    {
        var start = 0;
        while (start < label.Length && !char.IsLetterOrDigit(label[start]) && label[start] is not ('(' or '[')) start++;
        return start == 0 || start >= label.Length ? label : label[start..];
    }
}

/// <summary>Binds a category name in XAML to its cleaned display text.</summary>
internal sealed class CleanLabelConverter : IValueConverter
{
    public static CleanLabelConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string label ? DisplayText.CleanLabel(label) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
