using System.Globalization;
using System.Text.Json;

namespace Zaplio;

/// <summary>Xtream subscription details from the login response (<c>user_info</c>), shown on the subscription card.</summary>
internal sealed record AccountInfo(DateTimeOffset CheckedAt, DateTimeOffset? ExpiresAt, int? MaxConnections,
    int? ActiveConnections, bool IsTrial, string? Status)
{
    public const int WarningDays = 7;
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static AccountInfo Parse(JsonElement userInfo, DateTimeOffset checkedAt)
    {
        DateTimeOffset? expires = long.TryParse(Text(userInfo, "exp_date"), NumberStyles.None, CultureInfo.InvariantCulture, out var unix) && unix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(unix) : null;
        return new AccountInfo(checkedAt, expires, Number(userInfo, "max_connections"), Number(userInfo, "active_cons"),
            Text(userInfo, "is_trial") is "1" or "true", Text(userInfo, "status"));
    }

    /// <summary>Whole days left, rounded down; negative once expired; null for unlimited accounts.</summary>
    public int? DaysLeft(DateTimeOffset now) => ExpiresAt is { } end ? (int)Math.Floor((end - now).TotalDays) : null;

    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } end && end <= now;

    public bool ExpiresSoon(DateTimeOffset now) => DaysLeft(now) is { } days && days < WarningDays && !IsExpired(now);

    /// <summary>One short line for the card, e.g. "28 gün kaldı · 1/2 bağlantı".</summary>
    public string CardLine(DateTimeOffset now)
    {
        var expiry = IsExpired(now) ? Lang.T("Abonelik bitti")
            : DaysLeft(now) is { } days ? days == 0 ? Lang.T("Bugün bitiyor") : Lang.F($"{days} gün kaldı")
            : Lang.T("Süresiz");
        return Connections is { } connections ? $"{expiry} · {connections}" : expiry;
    }

    /// <summary>Footer warning for expiring or expired subscriptions; null when there is nothing to warn about.</summary>
    public string? Warning(string sourceName, DateTimeOffset now) =>
        IsExpired(now) ? Lang.F($"{sourceName} aboneliğinin süresi doldu.")
        : !ExpiresSoon(now) ? null
        : DaysLeft(now) == 0 ? Lang.F($"{sourceName} aboneliği bugün bitiyor.")
        : Lang.F($"{sourceName} aboneliğinin bitmesine {DaysLeft(now)} gün kaldı.");

    public string Details(DateTimeOffset now)
    {
        var lines = new List<string>
        {
            ExpiresAt is { } end ? Lang.F($"Bitiş: {end.ToLocalTime().ToString("d MMMM yyyy HH:mm", Lang.Culture)}") : Lang.T("Bitiş: süresiz"),
            Lang.F($"Bağlantı: {Connections ?? "bilinmiyor"} (açık / izin verilen)")
        };
        if (IsTrial) lines.Add(Lang.T("Deneme hesabı"));
        if (!string.IsNullOrWhiteSpace(Status)) lines.Add(Lang.F($"Durum: {Status}"));
        lines.Add(Lang.F($"Son kontrol: {CheckedAt.ToLocalTime().ToString("d MMM HH:mm", Lang.Culture)} · yenilemek için tıkla"));
        return string.Join("\n", lines);
    }

    /// <summary>Every connection the subscription allows is in use (by other devices when ours was just refused).</summary>
    public bool AllConnectionsInUse => MaxConnections is > 0 and var max && ActiveConnections >= max;

    /// <summary>What to tell the user when a stream would not open because the subscription is full.</summary>
    public string? LimitMessage(string sourceName) => !AllConnectionsInUse ? null
        : (MaxConnections == 1
              ? Lang.F($"{sourceName}: hesabının tek bağlantısı başka bir yerde kullanımda.")
              : Lang.F($"{sourceName}: hesabındaki {MaxConnections} bağlantının hepsi kullanımda.")) +
          Lang.T(" Başka bir cihazda açık yayın varsa kapatıp tekrar dene.");

    private string? Connections => MaxConnections is { } max ? Lang.F($"{ActiveConnections ?? 0}/{max} bağlantı") : null;

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
                _ => null
            }
            : null;

    private static int? Number(JsonElement element, string property) =>
        int.TryParse(Text(element, property), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
}
