using System.Globalization;

namespace Zaplio;

internal enum SubtitleSize { Small, Normal, Large, Larger }

/// <summary>Picture adjustment in steps of −2…+2 for brightness, contrast and colour; all zero is the original picture.</summary>
internal sealed record PictureAdjust(int Brightness = 0, int Contrast = 0, int Saturation = 0)
{
    public const int Steps = 2;
    public static PictureAdjust Neutral { get; } = new();

    public bool IsNeutral => Brightness == 0 && Contrast == 0 && Saturation == 0;

    /// <summary>VLC's adjust filter values (1 is unchanged). Small steps: enough for a dark film without washing it out.</summary>
    public (float Brightness, float Contrast, float Saturation) Values =>
        (1f + Clamp(Brightness) * 0.08f, 1f + Clamp(Contrast) * 0.1f, 1f + Clamp(Saturation) * 0.25f);

    public static int Clamp(int step) => Math.Clamp(step, -Steps, Steps);

    public static string Label(int step) => step switch
    {
        0 => Lang.T("Normal"),
        > 0 => "+" + step.ToString(CultureInfo.InvariantCulture),
        _ => "−" + (-step).ToString(CultureInfo.InvariantCulture)
    };
}

/// <summary>
/// Options LibVLC is started with. Subtitle size, the subtitle box and volume levelling can only be set when the engine
/// starts, so a change makes the next stream open with a new engine (<see cref="Key"/> tells when).
/// Every option here was checked against the VLC version shipped: an unknown one stops LibVLC from starting at all.
/// </summary>
internal static class VlcOptions
{
    /// <summary>The player has its own on-screen display; keep VLC from showing titles, snapshot names or thumbnails.</summary>
    public static readonly string[] Base = ["--no-video-title-show", "--no-osd", "--no-snapshot-preview"];

    public static int SubtitleScale(SubtitleSize size) => size switch
    {
        SubtitleSize.Small => 80,
        SubtitleSize.Large => 130,
        SubtitleSize.Larger => 160,
        _ => 100
    };

    public static string SubtitleLabel(SubtitleSize size) => size switch
    {
        SubtitleSize.Small => Lang.T("Küçük"),
        SubtitleSize.Large => Lang.T("Büyük"),
        SubtitleSize.Larger => Lang.T("Çok büyük"),
        _ => Lang.T("Normal")
    };

    public static string[] For(PlaybackSettings settings)
    {
        var options = new List<string>(Base);
        if (SubtitleScale(settings.SubtitleSize) is var scale and not 100)
            options.Add($"--sub-text-scale={scale.ToString(CultureInfo.InvariantCulture)}");
        if (settings.SubtitleBackground)
        {
            // A dark, see-through box behind the text: readable over bright scenes too.
            options.Add("--freetype-background-opacity=170");
            options.Add("--freetype-background-color=0");
        }
        if (settings.NormalizeVolume)
        {
            // Evens out loud adverts and quiet channels.
            options.Add("--audio-filter=normvol");
            options.Add("--norm-max-level=1.5");
        }
        return options.ToArray();
    }

    /// <summary>Same key, same engine: a change of any option needs a new one.</summary>
    public static string Key(IEnumerable<string> options) => string.Join(' ', options);
}
