using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Zaplio;

/// <summary>
/// Keeps the last channel list of each source on disk so the app opens instantly and still shows channels
/// when the provider is unreachable. Files are compressed and protected with DPAPI because Xtream stream
/// addresses contain the account's user name and password.
/// </summary>
internal sealed class CatalogCache
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    public static string Folder { get; internal set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zaplio", "cache");

    private const string CatalogKind = "catalog2"; // v2 adds channel logos

    private sealed record Entry<T>(string Fingerprint, DateTimeOffset SavedAt, T Data);
    private sealed record CatalogData(List<Channel> Channels, List<Uri> GuideLocations, string? TimeZone = null,
        AccountInfo? Account = null);

    public sealed record Hit<T>(T Value, DateTimeOffset SavedAt)
    {
        public bool IsFresh => DateTimeOffset.UtcNow - SavedAt < MaxAge;
    }

    public Hit<LoadedCatalog>? Load(Source source) => Read<CatalogData>(source, CatalogKind) is { } hit
        ? new Hit<LoadedCatalog>(new LoadedCatalog(hit.Value.Channels, hit.Value.GuideLocations, hit.Value.TimeZone, hit.Value.Account), hit.SavedAt)
        : null;

    public void Save(Source source, LoadedCatalog catalog) =>
        Write(source, CatalogKind, new CatalogData(catalog.Channels.ToList(), catalog.GuideLocations.ToList(), catalog.TimeZone, catalog.Account));

    public Hit<List<VodItem>>? LoadVod(Source source, LibrarySection section) => Read<List<VodItem>>(source, VodKind(section));

    public void SaveVod(Source source, LibrarySection section, IReadOnlyList<VodItem> items) =>
        Write(source, VodKind(section), items.ToList());

    private static string VodKind(LibrarySection section) => section == LibrarySection.Series ? "series" : "movies";

    private static Hit<T>? Read<T>(Source source, string kind) where T : class
    {
        var path = PathFor(source.Id, kind);
        try
        {
            if (!File.Exists(path)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            using var gzip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
            var entry = JsonSerializer.Deserialize<Entry<T>>(gzip);
            if (entry is null || entry.Data is null || entry.Fingerprint != Fingerprint(source)) return null;
            return new Hit<T>(entry.Data, entry.SavedAt);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or
            JsonException or InvalidDataException or NotSupportedException)
        {
            AppLog.Warn($"Önbellek okunamadı ({source.Name}, {kind}); siliniyor.", exception);
            TryDelete(path);
            return null;
        }
    }

    private static void Write<T>(Source source, string kind, T data)
    {
        var path = PathFor(source.Id, kind);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            var entry = new Entry<T>(Fingerprint(source), DateTimeOffset.UtcNow, data);
            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
                JsonSerializer.Serialize(gzip, entry);
            var encrypted = ProtectedData.Protect(buffer.ToArray(), null, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or
            NotSupportedException)
        {
            AppLog.Warn($"Önbellek yazılamadı ({source.Name}, {kind}).", exception);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public void Delete(Guid sourceId)
    {
        foreach (var kind in new[] { "catalog", CatalogKind, "movies", "series" }) TryDelete(PathFor(sourceId, kind));
    }

    /// <summary>Removes cached channel lists and guides. Returns false when some files could not be deleted.</summary>
    public static bool Clear()
    {
        if (!Directory.Exists(Folder)) return true;
        var complete = true;
        foreach (var file in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
            complete &= TryDelete(file);
        return complete;
    }

    /// <summary>Deletes cache files nobody has touched for a long time (removed sources, old guide URLs).</summary>
    public static void Prune(TimeSpan olderThan)
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            var limit = DateTime.Now - olderThan;
            foreach (var file in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
                if (File.GetLastWriteTime(file) < limit) TryDelete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Warn("Önbellek temizliği yapılamadı.", exception);
        }
    }

    private static string PathFor(Guid sourceId, string kind) => Path.Combine(Folder, $"{sourceId:N}.{kind}");

    private static string Fingerprint(Source source) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{source.Kind}\n{source.Location}\n{source.Username}\n{source.Password}")));

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
