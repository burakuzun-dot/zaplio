using System.Windows.Media;

namespace Zaplio;

internal enum AppTheme { Dark, Light, System }

/// <summary>An accent the user can pick: its name in settings and the shades it needs on dark and on light backgrounds.</summary>
/// <param name="Bright">The accent as text and lines on dark backgrounds.</param>
/// <param name="Deep">The accent as text and lines on light backgrounds (darker, for contrast).</param>
/// <param name="Strong">The saturated brand tone (buttons, the bolt's glow).</param>
internal sealed record AccentChoice(string Key, string Name, Color Bright, Color Deep, Color Strong);

/// <summary>
/// The colours of the interface for a theme and an accent. Pure data, so contrast can be tested: the theme's resource keys
/// (Canvas, Panel, Text, Accent…) get these colours at start-up and whenever the setting changes.
/// </summary>
internal static class ThemePalette
{
    private static Color Rgb(uint value) => Color.FromRgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);

    internal static readonly AccentChoice[] Accents =
    [
        new("purple", "Mor", Rgb(0xC1B4FF), Rgb(0x5B3FD9), Rgb(0x7B61FF)),
        new("blue", "Mavi", Rgb(0x9CC4FF), Rgb(0x1F5FCC), Rgb(0x3D7DFF)),
        new("teal", "Turkuaz", Rgb(0x8EE6DA), Rgb(0x0B7A6E), Rgb(0x1FB5A3)),
        new("green", "Yeşil", Rgb(0xA6E3A1), Rgb(0x2E7D32), Rgb(0x43A047)),
        new("orange", "Turuncu", Rgb(0xFFC48C), Rgb(0xB35400), Rgb(0xF07C1A)),
        new("pink", "Pembe", Rgb(0xF5A9D0), Rgb(0xB0306E), Rgb(0xE0518F))
    ];

    internal static AccentChoice Accent(string? key) => Accents.FirstOrDefault(accent => accent.Key == key) ?? Accents[0];

    /// <summary>Whether the light palette applies (for "Sistemle aynı", Windows' app theme decides).</summary>
    internal static bool IsLight(AppTheme mode, bool windowsUsesLight) => mode == AppTheme.Light || mode == AppTheme.System && windowsUsesLight;

    /// <summary>Every theme colour by resource key; gradients are listed as "Key.0", "Key.1" for their stops.</summary>
    internal static IReadOnlyDictionary<string, Color> For(bool light, AccentChoice accent)
    {
        var colors = light
            ? new Dictionary<string, Color>
            {
                ["Canvas"] = Rgb(0xF3F4F9), ["Panel"] = Rgb(0xFFFFFF), ["Surface"] = Rgb(0xEDEFF5), ["Line"] = Rgb(0xD5D9E4),
                ["Hover"] = Rgb(0xE4E7F0), ["Text"] = Rgb(0x161925), ["Muted"] = Rgb(0x555C70), ["Error"] = Rgb(0xC0262D),
                ["LiveColor"] = Rgb(0x1B7F4B), ["Warning"] = Rgb(0x8A6100), ["PlayerEdge"] = Rgb(0xC9CEDB),
                ["PlayerControlsSurface.0"] = Rgb(0xFFFFFF), ["PlayerControlsSurface.1"] = Rgb(0xF1F3F8),
                ["Accent"] = accent.Deep, ["Active"] = Blend(accent.Strong, Rgb(0xFFFFFF), 0.86),
                ["ActiveLine"] = Blend(accent.Strong, Rgb(0xFFFFFF), 0.55), ["BrandPurple"] = accent.Strong,
                // White button text needs the deep tone: the saturated one is too light for it with green, teal and orange.
                ["Brand.0"] = accent.Deep, ["Brand.1"] = Blend(accent.Deep, Rgb(0x000000), 0.15), ["Ink"] = Rgb(0xFFFFFF),
                ["ZpSurfaceHoverBrush"] = Rgb(0xE4E7F0), ["ZpIconMutedBrush"] = Rgb(0x6B7186)
            }
            : new Dictionary<string, Color>
            {
                ["Canvas"] = Rgb(0x0E1020), ["Panel"] = Rgb(0x121621), ["Surface"] = Rgb(0x1A2030), ["Line"] = Rgb(0x292F40),
                ["Hover"] = Rgb(0x232A3D), ["Text"] = Rgb(0xF4F3FA), ["Muted"] = Rgb(0xABB1C5), ["Error"] = Rgb(0xFFABA9),
                ["LiveColor"] = Rgb(0x8FE3B8), ["Warning"] = Rgb(0xF2D27A), ["PlayerEdge"] = Rgb(0x34384B),
                ["PlayerControlsSurface.0"] = Rgb(0x202436), ["PlayerControlsSurface.1"] = Rgb(0x141724),
                ["Accent"] = accent.Bright, ["Active"] = Blend(accent.Strong, Rgb(0x121621), 0.82),
                ["ActiveLine"] = Blend(accent.Strong, Rgb(0x121621), 0.52), ["BrandPurple"] = accent.Strong,
                ["Brand.0"] = Blend(accent.Bright, Rgb(0xFFFFFF), 0.1), ["Brand.1"] = Blend(accent.Bright, accent.Strong, 0.45),
                ["Ink"] = Blend(accent.Strong, Rgb(0x000000), 0.75),
                ["ZpSurfaceHoverBrush"] = Rgb(0x1C2038), ["ZpIconMutedBrush"] = Rgb(0x7D83A6)
            };
        return colors;
    }

    /// <summary>Mixes <paramref name="color"/> towards <paramref name="toward"/> by <paramref name="amount"/> (0 = unchanged, 1 = toward).</summary>
    internal static Color Blend(Color color, Color toward, double amount) => Color.FromRgb(
        (byte)Math.Round(color.R + (toward.R - color.R) * amount),
        (byte)Math.Round(color.G + (toward.G - color.G) * amount),
        (byte)Math.Round(color.B + (toward.B - color.B) * amount));

    /// <summary>WCAG contrast ratio between two colours (1–21).</summary>
    internal static double Contrast(Color a, Color b)
    {
        static double Luminance(Color color)
        {
            static double Channel(byte value)
            {
                var c = value / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }
        var (light, dark) = (Luminance(a), Luminance(b));
        if (light < dark) (light, dark) = (dark, light);
        return (light + 0.05) / (dark + 0.05);
    }
}
