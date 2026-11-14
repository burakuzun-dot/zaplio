using System.Diagnostics;
using System.IO;
using LibVLCSharp.Shared;

namespace Zaplio;

public partial class MainWindow
{
    private Task<LibVLC>? _engineTask;

    /// <summary>
    /// Creates the shared LibVLC instance once, off the UI thread. Without VLC's plugin cache (plugins.dat) LibVLC loads
    /// all ~320 plugin DLLs at start-up, and with Windows code-integrity checks on freshly copied files that took ~35 s.
    /// The cache makes VLC load only the plugins a stream needs, so it is created here when missing.
    /// </summary>
    /// <summary>Starts preparing VLC before the window is shown (during the intro animation).</summary>
    internal void WarmUp() => _ = EnsureEngineAsync();

    private Task<LibVLC> EnsureEngineAsync()
    {
        if (_engineTask is { IsFaulted: true } or { IsCanceled: true }) _engineTask = null;
        return _engineTask ??= Task.Run(CreateEngine);
    }

    private static LibVLC CreateEngine()
    {
        var clock = Stopwatch.StartNew();
        Core.Initialize();
        var cache = Path.Combine(AppContext.BaseDirectory, "libvlc", Environment.Is64BitProcess ? "win-x64" : "win-x86",
            "plugins", "plugins.dat");
        var hadCache = File.Exists(cache);
        var engine = hadCache
            ? new LibVLC("--no-video-title-show")
            : new LibVLC("--no-video-title-show", "--reset-plugins-cache");
        var status = hadCache ? "kullanıldı" : File.Exists(cache) ? "oluşturuldu" : "oluşturulamadı";
        AppLog.Info($"VLC motoru hazır: {clock.ElapsedMilliseconds} ms · eklenti önbelleği {status}");
        return engine;
    }
}
