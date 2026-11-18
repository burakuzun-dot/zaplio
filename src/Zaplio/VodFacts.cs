using System.Globalization;
using System.Text.Json;

namespace Zaplio;

/// <summary>
/// Genre, running time, rating and credits of a movie, series or episode, read from the "info" object that the Xtream API
/// already sends with the detail and episode responses (no extra request). Every field is optional.
/// </summary>
internal sealed record VodFacts(string? Genre, string? Duration, string? Rating, string? Director, string? Cast)
{
    internal bool IsEmpty => Genre is null && Duration is null && Rating is null && Director is null && Cast is null;

    /// <summary>The short facts shown as chips: genre, running time, rating.</summary>
    internal IReadOnlyList<string> Chips => new[] { Genre, Duration, Rating }.OfType<string>().ToArray();

    internal static VodFacts? Read(JsonElement info)
    {
        if (info.ValueKind != JsonValueKind.Object) return null;
        var facts = new VodFacts(
            Clean(Text(info, "genre")),
            FormatDuration(Text(info, "duration_secs"), Text(info, "duration"), Text(info, "episode_run_time")),
            FormatRating(Text(info, "rating"), Text(info, "rating_5based")),
            Clean(Text(info, "director")),
            Clean(Text(info, "cast") ?? Text(info, "actors")));
        return facts.IsEmpty ? null : facts;
    }

    /// <summary>Episode facts first, the series' genre and credits where the episode has none.</summary>
    internal VodFacts With(VodFacts? fallback) => fallback is null ? this
        : new VodFacts(Genre ?? fallback.Genre, Duration, Rating, Director ?? fallback.Director, Cast ?? fallback.Cast);

    internal static string? FormatDuration(string? seconds, string? clock, string? minutes)
    {
        TimeSpan? length = null;
        if (double.TryParse(seconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var secs) && secs > 0) length = TimeSpan.FromSeconds(secs);
        else if (TimeSpan.TryParse(clock, CultureInfo.InvariantCulture, out var parsed) && parsed > TimeSpan.Zero) length = parsed;
        else if (double.TryParse(minutes, NumberStyles.Float, CultureInfo.InvariantCulture, out var mins) && mins > 0) length = TimeSpan.FromMinutes(mins);
        if (length is not { } value || value.TotalMinutes < 1 || value.TotalHours > 24) return null;
        var hours = (int)value.TotalHours;
        return hours == 0 ? Lang.F($"{value.Minutes} dk") : value.Minutes == 0 ? $"{hours} sa" : Lang.F($"{hours} sa {value.Minutes} dk");
    }

    internal static string? FormatRating(string? tenBased, string? fiveBased)
    {
        var rating = Number(tenBased) is > 0 and <= 10 and var ten ? ten : Number(fiveBased) is > 0 and <= 5 and var five ? five * 2 : 0;
        return rating > 0 ? $"★ {rating.ToString("0.0", Lang.Culture)}" : null;

        static double Number(string? text) =>
            double.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    /// <summary>Trims, collapses spaces and commas, and drops placeholder values some providers send ("N/A", "0").</summary>
    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim() is "N/A" or "n/a") return null;
        var parts = text.Split([',', '/', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0 && part is not ("N/A" or "n/a" or "0" or "-")).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }
}
