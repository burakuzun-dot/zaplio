using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Zaplio;

/// <summary>The installer of a release, as GitHub lists it: where it is, how big, and its SHA-256 (GitHub publishes one per file).</summary>
internal sealed record UpdatePackage(Uri Url, long Size, string? Sha256);

/// <summary>
/// Updating from inside Zaplio: the release's installer is downloaded straight from GitHub, checked against the size and the
/// SHA-256 that GitHub lists for it, and run silently; it closes Zaplio, installs over it (per user, so no admin prompt) and opens
/// it again. Only copies installed by the installer update this way; the ZIP version keeps opening the release page.
/// </summary>
internal static class UpdateInstaller
{
    /// <summary>Inno Setup: a small progress window only, no questions, then Zaplio is started again ("--updated").</summary>
    internal const string InstallerArguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS";

    internal static string AssetName(Version version) => $"Zaplio-{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}-setup.exe";

    /// <summary>The setup asset of a release, only if it is served from this repository's release downloads over HTTPS.</summary>
    internal static UpdatePackage? FindInstaller(JsonElement release, Version version)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        var wanted = AssetName(version);
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var name) is false || !string.Equals(name.GetString(), wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (!asset.TryGetProperty("browser_download_url", out var link) || !Uri.TryCreate(link.GetString(), UriKind.Absolute, out var url) ||
                !IsTrusted(url)) return null;
            var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var bytes) ? bytes : 0;
            var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
            var sha = digest is { } text && text.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? text[7..].ToLowerInvariant() : null;
            return size > 0 ? new UpdatePackage(url, size, sha) : null;
        }
        return null;
    }

    /// <summary>HTTPS, github.com, and this repository's release downloads only.</summary>
    internal static bool IsTrusted(Uri url) =>
        url.Scheme == Uri.UriSchemeHttps && string.Equals(url.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
        url.AbsolutePath.StartsWith($"/{UpdateChecker.Repository}/releases/download/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this copy was installed by the installer (it leaves its uninstaller next to Zaplio.exe).</summary>
    internal static bool IsInstalledCopy(string programFolder) => File.Exists(Path.Combine(programFolder, "unins000.exe"));

    /// <summary>
    /// Downloads the installer into <paramref name="folder"/> and returns its path, or throws if the size or the SHA-256 does not
    /// match what GitHub listed (a broken or replaced download is deleted, never run).
    /// </summary>
    internal static async Task<string> DownloadAsync(HttpClient http, UpdatePackage package, string folder, IProgress<double>? progress,
        CancellationToken token)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Path.GetFileName(package.Url.AbsolutePath));
        try
        {
            using var response = await http.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long written = 0;
            await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var buffer = new byte[1 << 16];
                int read;
                while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    written += read;
                    if (written > package.Size) throw new InvalidDataException(Lang.T("İndirilen dosya beklenenden büyük."));
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    progress?.Report((double)written / package.Size);
                }
            }
            Verify(written, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), package);
            return path;
        }
        catch
        {
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    internal static void Verify(long size, string sha256, UpdatePackage package)
    {
        if (size != package.Size) throw new InvalidDataException(Lang.T("İndirilen dosyanın boyutu tutmuyor."));
        if (package.Sha256 is { } expected && !string.Equals(expected, sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(Lang.T("İndirilen dosyanın SHA-256 özeti GitHub'dakiyle tutmuyor."));
    }
}
