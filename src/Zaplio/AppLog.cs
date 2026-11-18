using System.IO;
using System.Text.RegularExpressions;

namespace Zaplio;

/// <summary>
/// Small daily-rotated text log in %LocalAppData%\Zaplio\logs. Account details that stream providers put in
/// stream and API addresses are masked before anything is written.
/// </summary>
internal static partial class AppLog
{
    private const int KeepDays = 14;
    private static readonly object Gate = new();
    private static bool _pruned;

    public static string Folder { get; } = Path.Combine(AppPaths.Root, "logs");

    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    public static string Redact(string text)
    {
        text = CredentialQuery().Replace(text, "$1=***");
        text = CredentialPath().Replace(text, "/$1/***/***/");
        text = UserInfo().Replace(text, "://***@");
        // The provider's server address is private too; this computer (the cast relay, the recording copy) stays readable.
        return Host().Replace(text, "***");
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            line = Redact(line) + Environment.NewLine;
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                if (!_pruned) Prune();
                File.AppendAllText(Path.Combine(Folder, $"zaplio-{DateTime.Now:yyyyMMdd}.log"), line);
            }
        }
        catch (Exception)
        {
            // Logging must never take the player down.
        }
    }

    private static void Prune()
    {
        _pruned = true;
        var limit = DateTime.Now.AddDays(-KeepDays);
        foreach (var file in Directory.EnumerateFiles(Folder, "zaplio-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < limit) File.Delete(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [GeneratedRegex(@"\b(username|password|user|pass|token)=[^&\s""')]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialQuery();

    [GeneratedRegex(@"/(live|movie|series|timeshift)/[^/\s]+/[^/\s]+/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialPath();

    [GeneratedRegex(@"://[^/@\s]+@", RegexOptions.CultureInvariant)]
    private static partial Regex UserInfo();

    /// <summary>Host and port of any web address except this computer.</summary>
    [GeneratedRegex(@"(?<=\b(?:https?|rtsp|rtmps?|udp|rtp)://(?:\*\*\*@)?)(?!(?:127\.0\.0\.1|localhost)(?:[:/\s]|$))[^/\s:@""')]+(?::\d+)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Host();
}
