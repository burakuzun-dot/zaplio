using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Zaplio.Tests;

/// <summary>Every Turkish text on screen has an English version, and switching the language does what it should.</summary>
[Collection(WpfCollection.Name)]
public sealed partial class LangTests
{
    private static readonly string Source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Zaplio"));

    /// <summary>Text that reads the same in both languages needs no entry.</summary>
    private static readonly HashSet<string> SameInBoth = ["Zaplio", "Ctrl+F", "PIN", "Xtream API", "Dil / Language"];

    private static IEnumerable<string> Files(string pattern) =>
        Directory.EnumerateFiles(Source, pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string Unescape(string text) => Regex.Unescape(text);

    /// <summary>The composite format FormattableString gives for "…{expr,align:format}…" (holes without nested strings).</summary>
    private static string FormatKey(string body)
    {
        var result = new StringBuilder();
        var index = 0;
        for (var i = 0; i < body.Length; i++)
        {
            if (body[i] == '{' && i + 1 < body.Length && body[i + 1] == '{') { result.Append("{{"); i++; continue; }
            if (body[i] == '}' && i + 1 < body.Length && body[i + 1] == '}') { result.Append("}}"); i++; continue; }
            if (body[i] != '{') { result.Append(body[i]); continue; }
            var depth = 0;
            var j = i + 1;
            int? align = null, format = null;
            for (; j < body.Length; j++)
            {
                var c = body[j];
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']') depth--;
                else if (c == '}') { if (depth == 0) break; depth--; }
                else if (c == ',' && depth == 0 && align is null && format is null) align = j;
                else if (c == ':' && depth == 0 && format is null) format = j;
            }
            var spec = (align is { } a ? body[a..(format ?? j)] : "") + (format is { } f ? body[f..j] : ""); // unescaped with the rest below
            result.Append('{').Append(index++).Append(spec).Append('}');
            i = j;
        }
        return Unescape(result.ToString().Replace("{{", "\u0001").Replace("}}", "\u0002")).Replace("\u0001", "{{").Replace("\u0002", "}}");
    }

    [GeneratedRegex(@"Lang\.T\(""((?:[^""\\\r\n]|\\.)*)""\)")]
    private static partial Regex PlainCall();

    /// <summary>Interpolated calls whose holes hold no string literals (the few that do are covered by the generator).</summary>
    [GeneratedRegex(@"Lang\.F\(\$""((?:[^""\\\r\n]|\\.)*)""\)")]
    private static partial Regex FormatCall();

    [Fact]
    public void Every_wrapped_text_in_the_code_has_an_english_version()
    {
        var missing = new List<string>();
        foreach (var file in Files("*.cs"))
        {
            var code = File.ReadAllText(file);
            foreach (Match match in PlainCall().Matches(code))
            {
                var key = Unescape(match.Groups[1].Value);
                if (!Lang.English.ContainsKey(key) && !SameInBoth.Contains(key)) missing.Add($"{Path.GetFileName(file)}: {key}");
            }
            foreach (Match match in FormatCall().Matches(code))
            {
                var key = FormatKey(match.Groups[1].Value);
                if (!Lang.English.ContainsKey(key) && !SameInBoth.Contains(key)) missing.Add($"{Path.GetFileName(file)}: {key}");
            }
        }
        Assert.True(missing.Count == 0, "İngilizcesi olmayan metinler:\n" + string.Join("\n", missing.Distinct()));
    }

    [Fact]
    public void Every_text_written_in_xaml_has_an_english_version()
    {
        string[] attributes = ["Text", "Content", "ToolTip", "Header", "Title"];
        var missing = new List<string>();
        foreach (var file in Files("*.xaml"))
        {
            foreach (var element in XDocument.Load(file).Descendants())
            {
                var texts = element.Attributes()
                    .Where(attribute => attributes.Contains(attribute.Name.LocalName) || attribute.Name.LocalName == "AutomationProperties.Name")
                    .Select(attribute => attribute.Value)
                    .Where(value => !value.StartsWith('{') && Regex.IsMatch(value, @"\p{L}{2}"));
                foreach (var text in texts)
                    if (!Lang.English.ContainsKey(text) && !SameInBoth.Contains(text)) missing.Add($"{Path.GetFileName(file)}: {text}");
            }
        }
        Assert.True(missing.Count == 0, "İngilizcesi olmayan XAML metinleri:\n" + string.Join("\n", missing.Distinct()));
    }

    [Fact]
    public void Formats_keep_their_values_in_english()
    {
        Lang.UseEnglish(true);
        try
        {
            Assert.Equal("3 favorite channels", Lang.F($"{3} kanal favorisi"));
            Assert.Equal("The Liste subscription ends in 5 days.", Lang.F($"{"Liste"} aboneliğinin bitmesine {5} gün kaldı."));
            Assert.Equal("Search channels", Lang.T("Kanal ara"));
            Assert.Equal("Bilinmeyen metin", Lang.T("Bilinmeyen metin")); // no entry: stays Turkish, never empty
        }
        finally
        {
            Lang.UseEnglish(false);
        }
        Assert.Equal("Kanal ara", Lang.T("Kanal ara"));
        Assert.Equal("3 kanal favorisi", Lang.F($"{3} kanal favorisi"));
    }

    [Theory]
    [InlineData(1, false)] // Turkish
    [InlineData(2, true)]  // English
    public void A_chosen_language_wins_over_windows(int language, bool english) =>
        Assert.Equal(english, Lang.Resolve((AppLanguage)language));

    [Fact]
    public void Older_settings_follow_windows()
    {
        var restored = System.Text.Json.JsonSerializer.Deserialize<LocalState>("{\"Settings\":{}}")!;
        Assert.Equal(AppLanguage.System, restored.Settings.Language);
    }
}
