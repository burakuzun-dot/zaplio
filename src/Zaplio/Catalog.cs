using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Zaplio;

internal enum SourceKind { M3uFile, M3uUrl, Xtream }

internal sealed record Source(Guid Id, string Name, SourceKind Kind, string Location,
    string Username = "", string Password = "", string? GuideLocation = null)
{
    public string TypeLabel => Kind switch
    {
        SourceKind.M3uFile => "M3U dosyası",
        SourceKind.M3uUrl => "M3U bağlantısı",
        _ => "Xtream API"
    };
    public string Icon => Kind switch
    {
        SourceKind.M3uFile => IconGlyphs.Document,
        SourceKind.M3uUrl => IconGlyphs.Link,
        _ => IconGlyphs.Plug
    };

    public override string ToString() => Name;
}

internal sealed record Channel(string Id, Guid SourceId, string Name, string Group, Uri Stream,
    string? UserAgent = null, string? Referrer = null, string? GuideId = null, string? GuideName = null,
    bool IsOnDemand = false, long SeriesId = 0, long EpisodeId = 0, string? Logo = null, int CatchupDays = 0, string? Badge = null)
{
    public string[] Categories { get; } = Group.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    public string GroupLabel => string.Join(" · ", Categories.Select(DisplayText.CleanLabel));
    public int BadgeTone => Id[^1] % 4;
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "•" : StringInfo.GetNextTextElement(Name).ToUpper(CultureInfo.CurrentCulture);
    public override string ToString() => Name;
}

/// <param name="TimeZone">Xtream server time zone (IANA id); archive addresses are expressed in it.</param>
internal sealed record LoadedCatalog(IReadOnlyList<Channel> Channels, IReadOnlyList<Uri> GuideLocations, string? TimeZone = null,
    AccountInfo? Account = null);

internal sealed class SourceException(string message) : Exception(message)
{
}

internal sealed partial class CatalogLoader : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly string[] StreamSchemes = ["http", "https", "rtsp", "rtmp", "rtmps", "udp", "rtp"];

    public async Task<LoadedCatalog> LoadAsync(Source source, CancellationToken cancellationToken)
    {
        var catalog = source.Kind switch
        {
            SourceKind.M3uFile => ParseM3u(source, await File.ReadAllTextAsync(source.Location, cancellationToken).ConfigureAwait(false), null),
            SourceKind.M3uUrl => await LoadM3uUrlAsync(source, cancellationToken).ConfigureAwait(false),
            _ => await LoadXtreamAsync(source, cancellationToken).ConfigureAwait(false)
        };

        return catalog.Channels.Count == 0 && source.Kind != SourceKind.Xtream
            ? throw new SourceException("Bu kaynakta oynatılabilir kanal bulunamadı.") : catalog;
    }

    public static Uri HttpAddress(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var address) ||
            address.Scheme is not ("http" or "https") || string.IsNullOrEmpty(address.Host))
            throw new SourceException("http:// veya https:// ile başlayan geçerli bir adres gir.");

        return address;
    }

    public static string UserMessage(Exception exception) => exception switch
    {
        SourceException => exception.Message,
        OperationCanceledException => "Bağlantı zaman aşımına uğradı.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized }
            => "Sunucu girişi reddetti. Kullanıcı adını ve şifreyi kontrol et.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden }
            => "Sağlayıcı erişimi reddetti (403). VPN (ör. Cloudflare WARP) açıksa kapatıp tekrar dene; değilse bağlantı sınırı aşılmış, IP adresin geçici olarak engellenmiş ya da hesap süresi dolmuş olabilir.",
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.TooManyRequests }
            => "Sağlayıcı çok fazla istek aldığını bildirdi (429). Birkaç dakika bekleyip tekrar dene.",
        HttpRequestException => "Sunucuya ulaşılamadı veya geçerli bir yanıt alınamadı.",
        JsonException => "Xtream sunucusu beklenen biçimde yanıt vermedi.",
        IOException or UnauthorizedAccessException => "Kaynak dosyası okunamadı. Konumunu ve erişim iznini kontrol et.",
        _ => "Kaynak yüklenirken bir sorun oluştu. Bilgileri kontrol edip tekrar dene."
    };

    private async Task<LoadedCatalog> LoadM3uUrlAsync(Source source, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(HttpAddress(source.Location), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseM3u(source, content, response.RequestMessage?.RequestUri);
    }

    private static LoadedCatalog ParseM3u(Source source, string content, Uri? baseAddress)
    {
        const string userAgentOption = "#EXTVLCOPT:http-user-agent=";
        const string referrerOption = "#EXTVLCOPT:http-referrer=";
        var lines = content.TrimStart('\uFEFF').Split('\n');
        var hasPlaylistHeader = content.TrimStart('\uFEFF', ' ', '\r', '\n', '\t')
            .StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase);
        if (lines.Any(line => line.TrimStart().StartsWith("#EXT-X-", StringComparison.OrdinalIgnoreCase)))
        {
            var stream = baseAddress ?? new Uri(Path.GetFullPath(source.Location));
            return new LoadedCatalog([new Channel(Id(source, "hls"), source.Id, source.Name, "Genel", stream)], []);
        }

        var channels = new List<Channel>();
        var guides = new List<Uri>();
        string? name = null, group = null, userAgent = null, referrer = null, guideId = null, guideName = null, logo = null;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                var attributes = M3uAttribute().Matches(line);
                var playlistAddress = baseAddress ?? new Uri(Path.GetFullPath(source.Location));
                foreach (var key in new[] { "x-tvg-url", "url-tvg", "tvg-url" })
                {
                    foreach (var location in (Attribute(attributes, key) ?? "").Split(',',
                                 StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (Uri.TryCreate(playlistAddress, location, out var guide) &&
                            (guide.Scheme is "http" or "https" || baseAddress is null && guide.IsFile &&
                             (!guide.IsUnc || playlistAddress.IsUnc && playlistAddress.Host == guide.Host)))
                            guides.Add(guide);
                    }
                }
                continue;
            }

            if (line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var metadata = line[8..];
                var comma = MetadataSeparator(metadata);
                var attributes = M3uAttribute().Matches(comma < 0 ? metadata : metadata[..comma]);
                name = comma < 0 ? null : metadata[(comma + 1)..].Trim();
                guideName = Attribute(attributes, "tvg-name");
                if (string.IsNullOrWhiteSpace(name)) name = guideName;
                group = Attribute(attributes, "group-title");
                guideId = Attribute(attributes, "tvg-id");
                logo = Attribute(attributes, "tvg-logo");
                userAgent = referrer = null;
                continue;
            }

            if (line.StartsWith("#EXTGRP:", StringComparison.OrdinalIgnoreCase)) group = line[8..].Trim();
            if (line.StartsWith(userAgentOption, StringComparison.OrdinalIgnoreCase)) userAgent = line[userAgentOption.Length..].Trim();
            if (line.StartsWith(referrerOption, StringComparison.OrdinalIgnoreCase)) referrer = line[referrerOption.Length..].Trim();
            if (line[0] == '#') continue;

            Uri.TryCreate(line, UriKind.Absolute, out var stream);
            if (baseAddress is not null && (hasPlaylistHeader || name is not null) && (stream is null || stream.IsFile))
                Uri.TryCreate(baseAddress, line, out stream);

            if (stream is not null && StreamSchemes.Contains(stream.Scheme, StringComparer.OrdinalIgnoreCase))
            {
                var channelName = string.IsNullOrWhiteSpace(name) ? $"Kanal {channels.Count + 1}" : name;
                var channelGroup = string.IsNullOrWhiteSpace(group) ? "Genel" : group;
                channels.Add(new Channel(Id(source, $"{channelName}\n{channelGroup}\n{stream.AbsoluteUri}"),
                    source.Id, channelName, channelGroup, stream, userAgent, referrer, guideId, guideName,
                    Logo: string.IsNullOrWhiteSpace(logo) ? null : logo.Trim()));
            }

            name = group = userAgent = referrer = guideId = guideName = logo = null;
        }

        return new LoadedCatalog(channels.DistinctBy(channel => channel.Id).ToList(), guides.Distinct().ToList());
    }

    private async Task<LoadedCatalog> LoadXtreamAsync(Source source, CancellationToken cancellationToken)
    {
        var (root, username, password) = XtreamAddress(source);
        var api = $"{root}/player_api.php?username={username}&password={password}";

        using var account = await ReadJsonAsync(api, cancellationToken).ConfigureAwait(false);
        if (account.RootElement.ValueKind != JsonValueKind.Object ||
            !account.RootElement.TryGetProperty("user_info", out var userInfo) ||
            Value(userInfo, "auth") is not ("1" or "true"))
            throw new SourceException("Xtream girişi doğrulanamadı. Kullanıcı adını ve şifreyi kontrol et.");

        var status = Value(userInfo, "status");
        if (!string.IsNullOrEmpty(status) && !status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            throw new SourceException("Xtream hesabı etkin değil.");
        var accountInfo = AccountInfo.Parse(userInfo, DateTimeOffset.UtcNow);

        var groupsTask = LoadGroupsAsync($"{api}&action=get_live_categories", cancellationToken);
        var streamsTask = ReadJsonAsync($"{api}&action=get_live_streams", cancellationToken);
        try
        {
            await Task.WhenAll(groupsTask, streamsTask).ConfigureAwait(false);
            var groups = await groupsTask.ConfigureAwait(false);
            var streams = (await streamsTask.ConfigureAwait(false)).RootElement;
            if (streams.ValueKind != JsonValueKind.Array)
                throw new SourceException("Xtream sunucusu geçerli bir canlı kanal listesi göndermedi.");

            var extension = userInfo.TryGetProperty("allowed_output_formats", out var formats) &&
                formats.ValueKind == JsonValueKind.Array &&
                formats.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == "m3u8") &&
                !formats.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == "ts")
                ? "m3u8" : "ts";

            var channels = new List<Channel>();
            foreach (var item in streams.EnumerateArray())
            {
                var streamId = Value(item, "stream_id");
                if (!long.TryParse(streamId, NumberStyles.None, CultureInfo.InvariantCulture, out var numericId) || numericId <= 0) continue;

                var name = Value(item, "name");
                var categoryId = Value(item, "category_id") ?? "";
                var group = groups.GetValueOrDefault(categoryId) ?? "Genel";
                var stream = new Uri($"{root}/live/{username}/{password}/{numericId.ToString(CultureInfo.InvariantCulture)}.{extension}");
                var archive = Value(item, "tv_archive") is "1" or "true"
                    ? Math.Max(1, int.TryParse(Value(item, "tv_archive_duration"), NumberStyles.None, CultureInfo.InvariantCulture, out var days) ? days : 1)
                    : 0;
                channels.Add(new Channel(Id(source, numericId.ToString(CultureInfo.InvariantCulture)), source.Id,
                    string.IsNullOrWhiteSpace(name) ? $"Kanal {numericId}" : name, group, stream,
                    GuideId: Value(item, "epg_channel_id"), Logo: NullIfBlank(Value(item, "stream_icon")), CatchupDays: archive));
            }

            var guide = new Uri($"{root}/xmltv.php?username={username}&password={password}");
            var timeZone = account.RootElement.TryGetProperty("server_info", out var server) ? NullIfBlank(Value(server, "timezone")) : null;
            return new LoadedCatalog(channels.DistinctBy(channel => channel.Id).ToList(), [guide], timeZone, accountInfo);
        }
        finally
        {
            if (streamsTask.IsCompletedSuccessfully) streamsTask.Result.Dispose();
        }
    }

    /// <summary>One login request; used by the subscription card to refresh expiry and connection counts.</summary>
    public async Task<AccountInfo> LoadAccountAsync(Source source, CancellationToken cancellationToken)
    {
        var (root, username, password) = XtreamAddress(source);
        using var account = await ReadJsonAsync($"{root}/player_api.php?username={username}&password={password}", cancellationToken)
            .ConfigureAwait(false);
        if (account.RootElement.ValueKind != JsonValueKind.Object || !account.RootElement.TryGetProperty("user_info", out var userInfo) ||
            Value(userInfo, "auth") is not ("1" or "true"))
            throw new SourceException("Xtream girişi doğrulanamadı. Kullanıcı adını ve şifreyi kontrol et.");
        return AccountInfo.Parse(userInfo, DateTimeOffset.UtcNow);
    }

    private static (string Root, string Username, string Password) XtreamAddress(Source source)
    {
        const string apiPath = "/player_api.php";
        var server = new UriBuilder(HttpAddress(source.Location)) { Query = "", Fragment = "" }.Uri;
        var root = server.AbsoluteUri.TrimEnd('/');
        if (root.EndsWith(apiPath, StringComparison.OrdinalIgnoreCase)) root = root[..^apiPath.Length];
        return (root, Uri.EscapeDataString(source.Username), Uri.EscapeDataString(source.Password));
    }

    private async Task<Dictionary<string, string>> LoadGroupsAsync(string address, CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, string>();
        try
        {
            using var document = await ReadJsonAsync(address, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return groups;
            foreach (var category in document.RootElement.EnumerateArray())
            {
                var id = Value(category, "category_id");
                var name = Value(category, "category_name");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name)) groups[id] = name;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Channels remain usable when a provider omits the category endpoint.
        }

        return groups;
    }

    private async Task<JsonDocument> ReadJsonAsync(string address, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(address, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string? Value(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int MetadataSeparator(string metadata)
    {
        var quoted = false;
        for (var index = 0; index < metadata.Length; index++)
        {
            if (metadata[index] == '"') quoted = !quoted;
            if (!quoted && metadata[index] == ',') return index;
        }
        return -1;
    }

    private static string? Attribute(MatchCollection attributes, string name) => attributes
        .FirstOrDefault(attribute => attribute.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase))?.Groups[2].Value;

    private static string Id(Source source, string value) =>
        $"{source.Id:N}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";

    [GeneratedRegex("([\\w-]+)\\s*=\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex M3uAttribute();

    public void Dispose() => _http.Dispose();
}
