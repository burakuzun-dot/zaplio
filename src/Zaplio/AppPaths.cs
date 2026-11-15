using System.IO;

namespace Zaplio;

/// <summary>
/// Where Zaplio keeps its data (settings, caches, logs). Normally <c>%LocalAppData%\Zaplio</c>; the environment variable
/// <c>ZAPLIO_DATA_DIR</c> points it somewhere else, which the self test uses so it can never touch the user's real data.
/// </summary>
internal static class AppPaths
{
    internal const string OverrideVariable = "ZAPLIO_DATA_DIR";

    public static string Root { get; } = Resolve(Environment.GetEnvironmentVariable(OverrideVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>True when the data folder was redirected with <see cref="OverrideVariable"/>.</summary>
    public static bool IsOverridden { get; } = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OverrideVariable));

    internal static string Resolve(string? overrideFolder, string localAppData) =>
        string.IsNullOrWhiteSpace(overrideFolder) ? Path.Combine(localAppData, "Zaplio") : Path.GetFullPath(overrideFolder.Trim());
}
