using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Zaplio;

/// <summary>
/// Videos and subtitles from this computer: what a drop, Ctrl + O or a file given on the command line contains, and the
/// playable entry for each video. Local files belong to no source, play with every player feature and are never sent anywhere.
/// </summary>
internal static class LocalMedia
{
    internal const string Group = "Bu bilgisayar";

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".mpg", ".mpeg", ".m2ts", ".mts", ".ts", ".3gp", ".ogv", ".vob", ".divx"
    };

    private static readonly HashSet<string> SubtitleExtensions = new(StringComparer.OrdinalIgnoreCase) { ".srt", ".ass", ".ssa", ".vtt", ".sub" };

    /// <summary>The Ctrl + O dialog filter.</summary>
    internal static string DialogFilter =>
        $"Video dosyaları|{string.Join(";", VideoExtensions.Select(extension => "*" + extension))}|Tüm dosyalar|*.*";

    internal static bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    internal static bool IsSubtitle(string path) => SubtitleExtensions.Contains(Path.GetExtension(path));

    /// <summary>Videos (in natural name order; a folder adds its own videos) and subtitles among the given paths.</summary>
    internal static (IReadOnlyList<string> Videos, IReadOnlyList<string> Subtitles) Sort(IEnumerable<string> paths)
    {
        var videos = new List<string>();
        var subtitles = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    videos.AddRange(Directory.EnumerateFiles(path).Where(IsVideo).Order(NaturalNameComparer.Instance));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
            else if (IsVideo(path)) videos.Add(path);
            else if (IsSubtitle(path)) subtitles.Add(path);
        }
        return (videos.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), subtitles);
    }

    /// <summary>A playable entry. The id is derived from the full path, so resume positions survive restarts.</summary>
    internal static Channel ToChannel(string path)
    {
        var full = Path.GetFullPath(path);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(full.ToUpperInvariant())))[..16].ToLowerInvariant();
        return new Channel($"file:{hash}", Guid.Empty, Title(full), Group, new Uri(full), IsOnDemand: true);
    }

    internal static bool IsLocal(Channel channel) => channel.Stream.IsFile;

    /// <summary>"Film.Adi.2021.1080p" → "Film Adi 2021 1080p": dots and underscores read as spaces when the name has none.</summary>
    internal static string Title(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).Trim();
        if (!name.Contains(' ')) name = name.Replace('.', ' ').Replace('_', ' ');
        return string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name.Trim();
    }

    /// <summary>"Bölüm 2" before "Bölüm 10".</summary>
    private sealed class NaturalNameComparer : IComparer<string>
    {
        internal static readonly NaturalNameComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            var a = Path.GetFileName(left) ?? "";
            var b = Path.GetFileName(right) ?? "";
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    var startA = i;
                    var startB = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    var numberA = a[startA..i].TrimStart('0');
                    var numberB = b[startB..j].TrimStart('0');
                    var byLength = numberA.Length.CompareTo(numberB.Length);
                    if (byLength != 0) return byLength;
                    var byDigits = string.CompareOrdinal(numberA, numberB);
                    if (byDigits != 0) return byDigits;
                    continue;
                }
                var byChar = string.Compare(a[i].ToString(), b[j].ToString(), StringComparison.CurrentCultureIgnoreCase);
                if (byChar != 0) return byChar;
                i++;
                j++;
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }
    }
}
