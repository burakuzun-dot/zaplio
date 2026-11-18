using System.Globalization;

namespace Zaplio;

internal sealed partial class CatalogLoader
{
    /// <summary>
    /// Builds an Xtream archive (timeshift) item for a programme that already started. Providers expect the start
    /// time in the server's own time zone, formatted yyyy-MM-dd:HH-mm, and the length in minutes.
    /// </summary>
    public static Channel? Catchup(Source source, Channel channel, string? serverTimeZone, DateTimeOffset start, DateTimeOffset? end,
        string title)
    {
        if (source.Kind != SourceKind.Xtream || channel.CatchupDays <= 0 || channel.IsOnDemand) return null;
        var file = channel.Stream.Segments[^1];
        if (!long.TryParse(file.Split('.')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var streamId)) return null;

        var zone = FindTimeZone(serverTimeZone);
        var serverStart = zone is null ? start.ToLocalTime() : TimeZoneInfo.ConvertTime(start, zone);
        var minutes = end is { } finish && finish > start ? (int)Math.Ceiling((finish - start).TotalMinutes) + 5 : 120;
        var connection = XtreamAddress(source);
        var stamp = serverStart.ToString("yyyy-MM-dd:HH-mm", CultureInfo.InvariantCulture);
        var stream = new Uri($"{connection.Root}/timeshift/{connection.Username}/{connection.Password}/" +
                             $"{minutes.ToString(CultureInfo.InvariantCulture)}/{stamp}/{streamId.ToString(CultureInfo.InvariantCulture)}.ts");
        var label = start.LocalDateTime.ToString("d MMM HH:mm", Lang.Culture);
        return channel with
        {
            Id = $"{channel.Id}:archive:{start.UtcDateTime:yyyyMMddHHmm}",
            Name = $"{channel.Name} · {title} ({label})",
            Stream = stream,
            IsOnDemand = true,
            CatchupDays = 0
        };
    }

    private static TimeZoneInfo? FindTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            AppLog.Warn($"Sunucu saat dilimi tanınmadı: {id}; yerel saat kullanılıyor.");
            return null;
        }
    }
}
