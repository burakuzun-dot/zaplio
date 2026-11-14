using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Zaplio;

public enum QualityMode { Auto, Highest, Saver }

/// <param name="Rank">Vertical resolution class (2160, 1080, 720, 480) or 0 when the name does not say.</param>
internal readonly record struct VariantQuality(int Rank, bool Hevc)
{
    /// <summary>Unknown quality is treated like HD when comparing.</summary>
    public int EffectiveRank => Rank == 0 ? 720 : Rank;

    public string Label => (Rank switch { 2160 => "4K", 1080 => "FHD", 720 => "HD", 480 => "SD", _ => "" }) +
                           (Hevc ? (Rank == 0 ? "HEVC" : " HEVC") : "");
}

/// <summary>One channel offered in several qualities or as duplicate feeds ("Kanal A HD", "Kanal A FHD", "Kanal A 4K").</summary>
internal sealed record VariantGroup(string Key, string DisplayName, IReadOnlyList<Channel> Channels)
{
    public bool HasAlternatives => Channels.Count > 1;
}

/// <summary>
/// Groups quality variants of the same channel, picks the right one for the connection and orders fallbacks.
/// UI-free and deterministic so it can be unit-tested.
/// </summary>
internal static partial class ChannelVariants
{
    /// <summary>Normalised name without quality/codec words, used to find variants of the same channel.</summary>
    public static string Key(string name)
    {
        var withoutTags = QualityWords().Replace(name, " ");
        var folded = new StringBuilder(withoutTags.Length);
        foreach (var character in withoutTags.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            folded.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }
        return Spaces().Replace(folded.ToString().Replace('ı', 'i'), " ").Trim();
    }

    public static VariantQuality Quality(string name)
    {
        var rank = 0;
        foreach (Match match in QualityWords().Matches(name))
        {
            var word = match.Value.ToUpperInvariant().Replace(" ", "");
            var value = word switch
            {
                "4K" or "UHD" or "2160" or "2160P" => 2160,
                "FHD" or "FULLHD" or "1080" or "1080P" or "1080I" => 1080,
                "HD" or "720" or "720P" => 720,
                "SD" or "480" or "480P" or "576" or "576P" => 480,
                _ => 0
            };
            rank = Math.Max(rank, value);
        }
        return new VariantQuality(rank, HevcWord().IsMatch(name));
    }

    /// <summary>The name with quality words and leftover brackets removed, e.g. "Kanal A FHD 🎬" → "Kanal A 🎬".</summary>
    public static string DisplayName(string name)
    {
        var cleaned = EmptyBrackets().Replace(QualityWords().Replace(name, " "), " ");
        cleaned = Spaces().Replace(cleaned, " ").Trim(' ', '-', '|', '·', ':');
        return cleaned.Length == 0 ? name.Trim() : cleaned;
    }

    /// <summary>Groups channels by <see cref="Key"/>, keeping the list order of each group's first channel.</summary>
    public static List<VariantGroup> Group(IEnumerable<Channel> channels) =>
        channels.GroupBy(channel => Key(channel.Name))
            .Select(group => new VariantGroup(group.Key, DisplayName(group.First().Name), group.ToList()))
            .ToList();

    /// <summary>Highest quality the connection can carry, from the fastest download seen this session.</summary>
    public static int RankLimit(QualityMode mode, double peakBitsPerSecond) => mode switch
    {
        QualityMode.Highest => int.MaxValue,
        QualityMode.Saver => 0,
        _ when peakBitsPerSecond <= 0 => 1080,              // nothing measured yet: FHD is a safe default
        _ when peakBitsPerSecond >= 35_000_000 => 2160,
        _ when peakBitsPerSecond >= 10_000_000 => 1080,
        _ when peakBitsPerSecond >= 4_000_000 => 720,
        _ => 480
    };

    /// <summary>The variant to show and play: the user's pin if any, otherwise the best one within the limit.</summary>
    public static Channel Choose(VariantGroup group, QualityMode mode, double peakBitsPerSecond, string? pinnedId = null)
    {
        if (pinnedId is not null && group.Channels.FirstOrDefault(channel => channel.Id == pinnedId) is { } pinned) return pinned;
        var limit = RankLimit(mode, peakBitsPerSecond);
        var ranked = group.Channels.Select((channel, index) => (channel, index, quality: Quality(channel.Name))).ToList();
        var fitting = ranked.Where(item => item.quality.EffectiveRank <= limit).ToList();
        var pick = fitting.Count > 0
            ? fitting.OrderByDescending(item => item.quality.EffectiveRank).ThenBy(item => item.quality.Hevc).ThenBy(item => item.index).First()
            : ranked.OrderBy(item => item.quality.EffectiveRank).ThenBy(item => item.index).First();
        return pick.channel;
    }

    /// <summary>
    /// Alternatives to try when <paramref name="current"/> fails: lower qualities first (closest first), because a failing
    /// stream is most often a bandwidth problem; higher ones last.
    /// </summary>
    public static IEnumerable<Channel> Fallbacks(VariantGroup group, Channel current)
    {
        var rank = Quality(current.Name).EffectiveRank;
        return group.Channels.Where(channel => channel.Id != current.Id)
            .Select((channel, index) => (channel, index, distance: Quality(channel.Name).EffectiveRank - rank))
            .OrderBy(item => item.distance > 0).ThenBy(item => Math.Abs(item.distance)).ThenBy(item => item.index)
            .Select(item => item.channel);
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(4K|UHD|2160p?|FHD|FULL\s?HD|1080[pi]?|HD|720p?|SD|480p?|576p?|HEVC|H\.?26[45]|AVC|\d{2}\s?FPS)(?![\p{L}\p{N}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QualityWords();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(HEVC|H\.?265)(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HevcWord();

    [GeneratedRegex(@"[\(\[\{]\s*[\)\]\}]")]
    private static partial Regex EmptyBrackets();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
