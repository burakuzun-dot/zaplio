using System.Diagnostics;
using System.IO;
using LibVLCSharp.Shared;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

public partial class MainWindow
{
    private Task<LibVLC>? _engineTask;
    private string? _engineKey;

    /// <summary>
    /// Engines replaced after a settings change. Not disposed until Zaplio closes: a recording being converted may still
    /// use one, and disposing LibVLC under a running player crashes.
    /// </summary>
    private readonly List<LibVLC> _retiredEngines = new();

    /// <summary>Starts preparing VLC before the window is shown (during the intro animation).</summary>
    internal void WarmUp() => _ = EnsureEngineAsync();

    /// <summary>
    /// The shared LibVLC instance, created off the UI thread. Without VLC's plugin cache (plugins.dat) LibVLC loads all
    /// ~320 plugin DLLs at start-up, and with Windows code-integrity checks on freshly copied files that took ~35 s; the
    /// cache makes VLC load only the plugins a stream needs, so it is created here when missing.
    /// When the subtitle or volume settings changed and nothing is playing, a new engine with the new options is started.
    /// </summary>
    private Task<LibVLC> EnsureEngineAsync()
    {
        if (_engineTask is { IsFaulted: true } or { IsCanceled: true }) _engineTask = null;
        var options = VlcOptions.For(_state.Settings);
        var key = VlcOptions.Key(options);
        if (_engineTask is { IsCompletedSuccessfully: true } current && _engineKey != key && _player is null)
        {
            _retiredEngines.Add(current.Result);
            _engineTask = null;
            _engine = null;
            AppLog.Info("VLC motoru yeni altyazı / ses ayarlarıyla yeniden hazırlanıyor.");
        }
        if (_engineTask is null)
        {
            _engineKey = key;
            _engineTask = Task.Run(() => CreateEngine(options));
        }
        return _engineTask;
    }

    private static LibVLC CreateEngine(string[] options)
    {
        var clock = Stopwatch.StartNew();
        Core.Initialize();
        var cache = Path.Combine(AppContext.BaseDirectory, "libvlc", Environment.Is64BitProcess ? "win-x64" : "win-x86",
            "plugins", "plugins.dat");
        var hadCache = File.Exists(cache);
        LibVLC engine;
        try
        {
            engine = new LibVLC(hadCache ? options : [.. options, "--reset-plugins-cache"]);
        }
        catch (VLCException exception) when (options.Length > VlcOptions.Base.Length)
        {
            // Never let a subtitle or sound option keep Zaplio from playing at all.
            AppLog.Warn("VLC ek seçeneklerle açılamadı; varsayılanlarla açılıyor.", exception);
            engine = new LibVLC(hadCache ? VlcOptions.Base : [.. VlcOptions.Base, "--reset-plugins-cache"]);
        }
        var status = hadCache ? Lang.T("kullanıldı") : File.Exists(cache) ? Lang.T("oluşturuldu") : Lang.T("oluşturulamadı");
        AppLog.Info($"VLC motoru hazır: {clock.ElapsedMilliseconds} ms · eklenti önbelleği {status}");
        return engine;
    }

    private void DisposeEngines()
    {
        (_engine ?? (_engineTask is { IsCompletedSuccessfully: true } warmed ? warmed.Result : null))?.Dispose();
        foreach (var retired in _retiredEngines) retired.Dispose();
        _retiredEngines.Clear();
    }

    /// <summary>
    /// Reopens what plays so a changed engine option (subtitle size, levelling) takes effect: a movie continues at the
    /// same second, a live channel simply reconnects.
    /// </summary>
    private async Task ReopenForEngineOptionsAsync()
    {
        if (_engineKey == VlcOptions.Key(VlcOptions.For(_state.Settings))) return;
        if (_playingChannel is not { } channel || _player is not { } player) return; // the next stream picks it up
        if (IsRecording)
        {
            SetFooter(Lang.T("Altyazı / ses ayarı kaydedildi; kayıt bitip yayın yeniden açılınca uygulanır."));
            return;
        }
        if (channel.IsOnDemand && player.IsSeekable && player.Time > 0) _forcedResume = player.Time;
        await PlayChannelAsync(channel, reconnect: true);
    }

    /// <summary>Brightness, contrast and colour on the current player (VLC's adjust filter; off when neutral).</summary>
    private void ApplyPicture(VlcPlayer player)
    {
        var picture = _state.Settings.Picture;
        if (picture.IsNeutral)
        {
            player.SetAdjustInt(VideoAdjustOption.Enable, 0);
            return;
        }
        var (brightness, contrast, saturation) = picture.Values;
        player.SetAdjustInt(VideoAdjustOption.Enable, 1);
        player.SetAdjustFloat(VideoAdjustOption.Brightness, brightness);
        player.SetAdjustFloat(VideoAdjustOption.Contrast, contrast);
        player.SetAdjustFloat(VideoAdjustOption.Saturation, saturation);
    }
}
