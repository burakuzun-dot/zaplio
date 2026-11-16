using System.IO;

namespace Zaplio;

/// <summary>
/// "--self-test &lt;report file&gt;": Zaplio walks through its own screens off screen with a small local demo list and writes what
/// went wrong. It refuses to run unless <see cref="AppPaths.OverrideVariable"/> redirects the data folder, so the user's
/// sources and settings are never read or changed, and it makes no network request (the demo list is a local file and
/// nothing is played). Run it with <c>tools\SelfTest.ps1</c>.
/// </summary>
internal static class SelfTest
{
    private static readonly List<string> Results = [];
    private static readonly List<string> Failures = [];

    /// <summary>True for the whole run; switches off the update check and the error dialog.</summary>
    internal static bool Active { get; private set; }

    internal static string? ReportPath { get; private set; }

    internal static int FailureCount => Failures.Count;

    /// <summary>Reads the command line. Returns false (with a reason) when a self test was asked for but must not run.</summary>
    internal static bool TryStart(IReadOnlyList<string> arguments, out string? refusal)
    {
        refusal = null;
        var index = arguments.ToList().IndexOf("--self-test");
        if (index < 0) return true;
        if (index + 1 >= arguments.Count)
        {
            refusal = "--self-test bir rapor dosyası yolu ister.";
            return false;
        }
        if (!AppPaths.IsOverridden)
        {
            refusal = $"Öz denetim yalnızca {AppPaths.OverrideVariable} ile ayrı bir veri klasörü verilince çalışır.";
            return false;
        }
        ReportPath = Path.GetFullPath(arguments[index + 1]);
        Active = true;
        return true;
    }

    internal static void Check(bool passed, string what)
    {
        Results.Add($"{(passed ? "TAMAM" : "HATA ")}  {what}");
        if (!passed) Failures.Add(what);
    }

    /// <summary>Optional folder for screenshots of each screen (set by <c>tools\SelfTest.ps1 -Shots</c>), for looking at the design.</summary>
    internal static string? ShotsFolder => Active ? Environment.GetEnvironmentVariable("ZAPLIO_SELFTEST_SHOTS") : null;

    /// <summary>Renders a window or element to "&lt;n&gt; &lt;name&gt;.png" in <see cref="ShotsFolder"/>. Does nothing without one.</summary>
    internal static void Snapshot(System.Windows.FrameworkElement element, string name)
    {
        if (ShotsFolder is not { Length: > 0 } folder || element.ActualWidth < 1 || element.ActualHeight < 1) return;
        try
        {
            Directory.CreateDirectory(folder);
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(element);
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
                (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            var file = Path.Combine(folder, $"{++_shot:00} {string.Concat(name.Split(Path.GetInvalidFileNameChars()))}.png");
            using var stream = File.Create(file);
            encoder.Save(stream);
        }
        catch (Exception exception)
        {
            Fail($"ekran görüntüsü alınamadı ({name})", exception);
        }
    }

    private static int _shot;

    internal static void Fail(string what, Exception? exception = null) =>
        Check(false, exception is null ? what : $"{what}: {exception.GetType().Name}: {exception.Message}");

    /// <summary>Twelve made-up channels in three groups, with quality variants; every address is example.com and is never opened.</summary>
    internal static string WriteDemoList(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "demo.m3u");
        var lines = new List<string> { "#EXTM3U" };
        string[] groups = ["Ulusal", "Haber", "Belgesel"];
        string[] names = ["Kanal A HD", "Kanal A FHD", "Kanal B", "Kanal C HD", "Haber 1", "Haber 2 HD", "Haber 2 FHD", "Haber 3",
            "Belgesel 1", "Belgesel 2 4K", "Belgesel 2 HD", "Belgesel 3"];
        for (var index = 0; index < names.Length; index++)
        {
            lines.Add($"#EXTINF:-1 tvg-id=\"demo{index}\" group-title=\"{groups[index / 4]}\",{names[index]}");
            lines.Add($"http://example.com/demo/{index}.m3u8");
        }
        File.WriteAllLines(path, lines);
        return path;
    }

    internal static void WriteReport()
    {
        if (ReportPath is null) return;
        var summary = Failures.Count == 0 ? $"SONUÇ: TAMAM ({Results.Count} denetim)" : $"SONUÇ: {Failures.Count} HATA ({Results.Count} denetim)";
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
        File.WriteAllLines(ReportPath, Results.Append(summary));
    }
}
