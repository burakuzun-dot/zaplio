using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace Zaplio;

internal sealed record GuideProgramme(DateTimeOffset Start, DateTimeOffset? End, string Title, string Description)
{
    public bool IsOnAir(DateTimeOffset time) => Start <= time && End > time;
}

internal sealed class XmlTvGuide
{
    private readonly Dictionary<string, string?> _channelAliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<GuideProgramme>> _programmes = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GuideProgramme> ForChannel(Channel channel)
    {
        var id = channel.GuideId?.Trim();
        if (!string.IsNullOrEmpty(id) && _programmes.TryGetValue(id, out var byId)) return byId;

        var guideName = channel.GuideName?.Trim();
        if (!string.IsNullOrEmpty(guideName) && FindByName(guideName) is { } byGuideName) return byGuideName;
        return FindByName(channel.Name.Trim()) ?? [];
    }

    private IReadOnlyList<GuideProgramme>? FindByName(string name) =>
        _programmes.GetValueOrDefault(_channelAliases.GetValueOrDefault(name) ?? name);

    public void AddAlias(string name, string id)
    {
        if (_channelAliases.TryGetValue(name, out var existing) && !StringComparer.OrdinalIgnoreCase.Equals(existing, id))
            _channelAliases[name] = null;
        else
            _channelAliases[name] = id;
    }

    public void AddProgramme(string id, GuideProgramme programme)
    {
        if (!_programmes.TryGetValue(id, out var entries)) _programmes[id] = entries = [];
        entries.Add(programme);
    }

    public void Merge(XmlTvGuide other)
    {
        foreach (var (alias, id) in other._channelAliases)
        {
            if (id is null) _channelAliases[alias] = null;
            else AddAlias(alias, id);
        }

        foreach (var (id, entries) in other._programmes)
        {
            if (_programmes.TryGetValue(id, out var existing)) existing.AddRange(entries);
            else _programmes.Add(id, entries);
        }
    }

    public void SortProgrammes()
    {
        foreach (var id in _programmes.Keys)
        {
            var entries = _programmes[id].OrderBy(entry => entry.Start).ThenByDescending(entry => entry.End.HasValue)
                .DistinctBy(entry => (entry.Start, entry.Title)).ToList();
            for (var index = 0; index + 1 < entries.Count; index++)
            {
                if (entries[index].End is null && entries[index + 1].Start > entries[index].Start)
                    entries[index] = entries[index] with { End = entries[index + 1].Start };
            }
            _programmes[id] = entries;
        }
    }
}

internal sealed class XmlTvLoader : IDisposable
{
    private static readonly string[] DateFormats = ["yyyy", "yyyyMM", "yyyyMMdd", "yyyyMMddHH", "yyyyMMddHHmm", "yyyyMMddHHmmss"];
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(35),
        MaxResponseContentBufferSize = 96 * 1024 * 1024
    };

    public static Uri Address(string input)
    {
        if (Uri.TryCreate(input.Trim(), UriKind.Absolute, out var address) &&
            (address.IsFile || (address.Scheme is "http" or "https" && address.Host.Length > 0)))
            return address;
        throw new SourceException(Lang.T("Bir XMLTV dosyası seç veya http:// ya da https:// ile başlayan bağlantı gir."));
    }

    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(6);
    private static string CacheFolder => Path.Combine(CatalogCache.Folder, "guides");

    public async Task<XmlTvGuide> LoadAsync(IReadOnlyList<Uri> locations, IReadOnlyList<Channel> channels, CancellationToken token,
        bool refresh = false)
    {
        var names = channels.Select(channel => channel.Name.Trim())
            .Concat(channels.Select(channel => channel.GuideName?.Trim()).OfType<string>().Where(name => name.Length > 0))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ids = channels.Select(channel => channel.GuideId?.Trim()).OfType<string>()
            .Where(id => id.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new XmlTvGuide?[locations.Count];
        var failures = new string?[locations.Count];

        await Parallel.ForEachAsync(Enumerable.Range(0, locations.Count),
            new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 3 },
            async (index, cancellationToken) =>
            {
                try
                {
                    results[index] = await LoadFeedAsync(locations[index], names, ids, refresh, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested &&
                    exception is SourceException or IOException or UnauthorizedAccessException or HttpRequestException or XmlException or OperationCanceledException)
                {
                    failures[index] = exception is XmlException or InvalidDataException
                        ? Lang.T("Geçerli bir XMLTV rehberi okunamadı.")
                        : CatalogLoader.UserMessage(exception);
                }
            }).ConfigureAwait(false);

        var guide = new XmlTvGuide();
        foreach (var result in results.OfType<XmlTvGuide>()) guide.Merge(result);
        if (results.All(result => result is null))
            throw new SourceException(failures.OfType<string>().FirstOrDefault() ?? Lang.T("Yayın rehberi yüklenemedi."));

        guide.SortProgrammes();
        return guide;
    }

    private async Task<XmlTvGuide> LoadFeedAsync(Uri location, HashSet<string> names, HashSet<string> ids, bool refresh,
        CancellationToken token)
    {
        if (location.IsFile)
        {
            using var file = File.OpenRead(location.LocalPath);
            return ReadFeed(file, names, ids, token);
        }

        // Guides are cached under a hash of the address because provider URLs carry account details.
        var cacheFile = Path.Combine(CacheFolder,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(location.AbsoluteUri)))[..32] + ".xml.gz");
        var cacheAge = File.Exists(cacheFile) ? DateTime.Now - File.GetLastWriteTime(cacheFile) : (TimeSpan?)null;
        if (!refresh && cacheAge < CacheMaxAge && TryReadCache(cacheFile, names, ids, token) is { } fresh) return fresh;

        byte[] content;
        try
        {
            using var response = await _http.GetAsync(location, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            content = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
        }
        catch (Exception exception) when (!token.IsCancellationRequested && cacheAge is not null &&
            exception is HttpRequestException or OperationCanceledException or IOException)
        {
            AppLog.Warn($"Yayın rehberi indirilemedi, kayıtlı kopya kullanılıyor: {location}", exception);
            if (TryReadCache(cacheFile, names, ids, token) is { } stale) return stale;
            throw;
        }

        var guide = ReadFeed(new MemoryStream(content, writable: false), names, ids, token);
        WriteCache(cacheFile, content);
        return guide;
    }

    private static XmlTvGuide? TryReadCache(string path, HashSet<string> names, HashSet<string> ids, CancellationToken token)
    {
        try
        {
            using var file = File.OpenRead(path);
            return ReadFeed(file, names, ids, token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or InvalidDataException)
        {
            AppLog.Warn("Kayıtlı yayın rehberi okunamadı.", exception);
            return null;
        }
    }

    private static void WriteCache(string path, byte[] content)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var file = File.Create(temporary))
            {
                if (content.Length > 1 && content[0] == 0x1F && content[1] == 0x8B) file.Write(content);
                else
                {
                    using var gzip = new GZipStream(file, CompressionLevel.Fastest);
                    gzip.Write(content);
                }
            }
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Yayın rehberi önbelleğe yazılamadı.", exception);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static XmlTvGuide ReadFeed(Stream stream, HashSet<string> names, HashSet<string> ids, CancellationToken token)
    {
        var compressed = stream.ReadByte() == 0x1F && stream.ReadByte() == 0x8B;
        stream.Position = 0;
        using var gzip = compressed ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true) : null;
        using var reader = XmlReader.Create(gzip ?? stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = 256L * 1024 * 1024
        });
        if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "tv")
            throw new SourceException(Lang.T("Bu dosya geçerli bir XMLTV rehberi değil."));

        var guide = new XmlTvGuide();
        var matches = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != 1) continue;

            if (reader.LocalName == "channel" && reader.GetAttribute("id")?.Trim() is { Length: > 0 } channelId)
            {
                using var subtree = reader.ReadSubtree();
                foreach (var (_, _, value) in TextElements(subtree, "display-name"))
                {
                    if (!names.Contains(value)) continue;
                    guide.AddAlias(value, channelId);
                    matches.Add(channelId);
                }
            }
            else if (reader.LocalName == "programme" &&
                reader.GetAttribute("channel")?.Trim() is { Length: > 0 } id &&
                (matches.Contains(id) || names.Contains(id)) &&
                ParseDate(reader.GetAttribute("start")) is DateTimeOffset start)
            {
                var end = ParseDate(reader.GetAttribute("stop"));
                using var subtree = reader.ReadSubtree();
                string title = "", description = "";
                foreach (var (element, language, value) in TextElements(subtree, "title", "desc"))
                {
                    var preferred = language is "tr" or "tr-tr";
                    if (element == "title" && (title.Length == 0 || preferred)) title = value;
                    if (element == "desc" && (description.Length == 0 || preferred)) description = value;
                }
                if (title.Length > 0) guide.AddProgramme(id, new GuideProgramme(start, end > start ? end : null, title, description));
            }
        }

        return guide;
    }

    private static IEnumerable<(string Element, string Language, string Value)> TextElements(XmlReader reader, params string[] elements)
    {
        reader.Read();
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == 1 && elements.Contains(reader.LocalName))
            {
                var name = reader.LocalName;
                var language = reader.GetAttribute("lang")?.ToLowerInvariant() ?? "";
                var value = reader.ReadElementContentAsString().Trim();
                if (value.Length > 0) yield return (name, language, value);
                continue;
            }
            reader.Read();
        }
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (!DateTime.TryParseExact(parts[0], DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return null;
        var offset = TimeSpan.Zero;
        if (parts.Length > 1 && parts[1] is not ("UTC" or "GMT" or "Z"))
        {
            var zone = parts[1].Replace(":", "", StringComparison.Ordinal);
            if (zone.Length != 5 || zone[0] is not ('+' or '-') ||
                !int.TryParse(zone.AsSpan(1, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
                !int.TryParse(zone.AsSpan(3, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
                minutes > 59 || hours * 60 + minutes > 14 * 60)
                return null;
            offset = TimeSpan.FromMinutes((hours * 60 + minutes) * (zone[0] == '-' ? -1 : 1));
        }

        try { return new DateTimeOffset(date, offset); }
        catch (ArgumentException) { return null; }
    }

    public void Dispose() => _http.Dispose();
}
