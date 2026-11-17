using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Zaplio;

/// <summary>
/// Puts the colours of <see cref="ThemePalette"/> into the application's resources. The XAML refers to these brushes with
/// DynamicResource, so replacing one updates every window already open, without a restart. (Changing a brush in place does not
/// work: WPF freezes brushes stored in a resource dictionary, even ones added at run time.)
/// The video area and what is drawn over it (badges, the reel, cinema mode) stay dark in both themes.
/// </summary>
internal static class ThemeManager
{
    /// <summary>True while the light palette is in use (the ambient light and other dark-only effects check it).</summary>
    internal static bool IsLight { get; private set; }

    /// <summary>
    /// The dark palette with the current accent, for parts that stay dark in both themes (the video area and what sits on it).
    /// Merged into their resources, it wins over the application's palette for everything inside them.
    /// </summary>
    internal static ResourceDictionary DarkIsland { get; } = new();

    /// <summary>The dictionary that defines <paramref name="key"/> (the theme file is a merged dictionary of the application's).</summary>
    private static ResourceDictionary? Owner(ResourceDictionary dictionary, object key)
    {
        foreach (var merged in dictionary.MergedDictionaries.Reverse())
            if (Owner(merged, key) is { } found) return found;
        return dictionary.Contains(key) ? dictionary : null;
    }

    internal static void Apply(ResourceDictionary resources, AppTheme mode, string? accentKey)
    {
        IsLight = ThemePalette.IsLight(mode, WindowsUsesLight());
        var accent = ThemePalette.Accent(accentKey);
        foreach (var (key, color) in ThemePalette.For(IsLight, accent))
        {
            var dot = key.IndexOf('.');
            if (dot < 0) SetBrush(resources, key, color);
            else SetStop(resources, key[..dot], int.Parse(key[(dot + 1)..]), color);
        }
        FillDarkIsland(resources, accent);
    }

    private static void FillDarkIsland(ResourceDictionary resources, AccentChoice accent)
    {
        foreach (var (key, color) in ThemePalette.For(false, accent))
        {
            var dot = key.IndexOf('.');
            if (dot < 0)
            {
                DarkIsland[key] = new SolidColorBrush(color);
                continue;
            }
            var name = key[..dot];
            if ((DarkIsland.Contains(name) ? DarkIsland[name] : resources[name]) is not GradientBrush gradient) continue;
            var copy = gradient.Clone();
            var index = int.Parse(key[(dot + 1)..]);
            if (index < copy.GradientStops.Count) copy.GradientStops[index].Color = color;
            DarkIsland[name] = copy;
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, Color color)
    {
        if (resources[key] is SolidColorBrush { IsFrozen: false } brush) brush.Color = color;
        else if (Owner(resources, key) is { } owner) owner[key] = new SolidColorBrush(color); // not primed: windows opened later get it
    }

    private static void SetStop(ResourceDictionary resources, string key, int index, Color color)
    {
        if (resources[key] is not GradientBrush gradient || index >= gradient.GradientStops.Count) return;
        if (!gradient.IsFrozen) gradient.GradientStops[index].Color = color;
        else if (Owner(resources, key) is { } owner)
        {
            var copy = gradient.Clone();
            copy.GradientStops[index].Color = color;
            owner[key] = copy;
        }
    }

    /// <summary>Windows' own app theme (Settings › Personalisation › Colours), for "Sistemle aynı".</summary>
    internal static bool WindowsUsesLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 1;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
