using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace Zaplio;

internal sealed record UpdateInfo(Version Version, string Tag, Uri Page);

/// <summary>Looks up the latest GitHub release. Release tags are expected to look like "v0.8.0" or "0.8.0".</summary>
internal static class UpdateChecker
{
    /// <summary>Owner/name of the GitHub repository (update together with README links if the repository is renamed).</summary>
    public const string Repository = "burakuzun-dot/zaplio";
    public const string ProjectPage = "https://github.com/" + Repository;
    private const string LatestRelease = "https://api.github.com/repos/" + Repository + "/releases/latest";

    public static Version CurrentVersion { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0));

    public static async Task<UpdateInfo?> FindNewerAsync(CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Zaplio", CurrentVersion.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await http.GetAsync(LatestRelease, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!root.TryGetProperty("tag_name", out var tagElement) || tagElement.GetString() is not { Length: > 0 } tag ||
            !root.TryGetProperty("html_url", out var pageElement) ||
            !Uri.TryCreate(pageElement.GetString(), UriKind.Absolute, out var page) || page.Scheme != Uri.UriSchemeHttps ||
            root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True ||
            root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
            return null;

        var text = tag.TrimStart('v', 'V').Split('-', '+')[0];
        if (!Version.TryParse(text, out var version)) return null;
        version = Normalize(version);
        return version > CurrentVersion ? new UpdateInfo(version, tag, page) : null;
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build));
}
