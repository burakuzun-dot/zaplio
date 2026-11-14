using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Zaplio.Tests;

public sealed class LegalTests
{
    [Fact]
    public void Notice_says_zaplio_provides_no_content_and_the_user_is_responsible()
    {
        var text = string.Join(" ", LegalNotice.Paragraphs);
        Assert.Contains("içerik sağlamaz", text);
        Assert.Contains("sen sorumlusun", text);
        Assert.Contains("erişim hakkına sahip", text);
    }

    [Fact]
    public void Notice_acceptance_defaults_to_false_and_survives_other_setting_changes()
    {
        Assert.False(new PlaybackSettings().LegalNoticeAccepted);

        var accepted = new PlaybackSettings { ShowIntro = false } with { LegalNoticeAccepted = true };
        var changed = accepted with { ShowLogos = false };

        Assert.True(changed.LegalNoticeAccepted);
        Assert.False(changed.ShowIntro);
    }

    [Fact]
    public void Notice_acceptance_is_saved_with_the_state()
    {
        var state = new LocalState { Settings = new PlaybackSettings { LegalNoticeAccepted = true } };
        var restored = JsonSerializer.Deserialize<LocalState>(JsonSerializer.SerializeToUtf8Bytes(state));
        Assert.True(restored!.Settings.LegalNoticeAccepted);
    }

    /// <summary>Guards the project's naming rule: examples must not use real broadcasters, channels or brands.</summary>
    [Fact]
    public void Repository_text_uses_no_real_broadcaster_or_brand_names()
    {
        var root = FindRepositoryRoot();
        var forbidden = new Regex(@"\b(TRT|beIN|Digiturk|D-Smart|Tivibu|Exxen|BluTV|Netflix|HBO|LaLiga|Premier League|Kanal D)\b|⚽",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var skipped = new[] { ".git", ".plans", "bin", "obj", "artifacts", "TestResults", "Licenses", "Zaplio-Gorseller", "node_modules" };
        var extensions = new[] { ".cs", ".xaml", ".md", ".yml", ".yaml", ".csproj", ".ps1", ".iss", ".json" };

        var offenders = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar).Any(part => skipped.Contains(part)))
            .Where(file => Path.GetFileName(file) != nameof(LegalTests) + ".cs")
            .Where(file => forbidden.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Gerçek yayıncı/marka adı geçen dosyalar: " + string.Join(", ", offenders));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Zaplio.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Zaplio.slnx bulunamadı.");
    }
}
