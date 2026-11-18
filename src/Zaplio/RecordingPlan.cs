using System.Globalization;
using System.IO;

namespace Zaplio;

/// <summary>
/// Recording what plays without touching its quality: VLC's stream output copies the incoming audio and video as they are
/// (no re-encoding) into a file. Matroska takes every codec a provider or a file may use; MPEG-TS was tried first and, with
/// LibVLC 3.0.24, silently dropped VP9/AV1/Theora video (a recording of such a video held only its sound).
/// </summary>
internal static class RecordingPlan
{
    internal const string Extension = ".mkv";

    internal static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Zaplio");

    /// <summary>"Zaplio_Kanal A_20261117_103000.ts": the title made safe for a file name, at most 60 characters.</summary>
    internal static string FileName(string title, DateTime now)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(title.Select(character => invalid.Contains(character) || character == '\'' ? ' ' : character).ToArray());
        safe = string.Join(' ', safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (safe.Length > 60) safe = safe[..60].TrimEnd();
        return string.IsNullOrEmpty(safe)
            ? $"Zaplio_{now:yyyyMMdd_HHmmss}{Extension}"
            : $"Zaplio_{safe}_{now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}{Extension}";
    }

    /// <summary>The media option that shows the stream and copies it into <paramref name="path"/> at the same time.</summary>
    internal static string StreamOutput(string path)
    {
        // Forward slashes need no escaping in VLC's option chain; double quotes keep an apostrophe in a folder name working
        // (checked with LibVLC 3.0.24: single quotes fail on it, and "append" is ignored, hence PartPath).
        var target = path.Replace('\\', '/').Replace("\"", "\\\"");
        return $":sout=#duplicate{{dst=display,dst=std{{access=file,mux=mkv,dst=\"{target}\"}}}}";
    }

    /// <summary>
    /// When a recorded stream starts again by itself (a live reconnect), the copy goes on in "…_2.ts", "…_3.ts" so nothing
    /// already recorded is overwritten.
    /// </summary>
    internal static string PartPath(string path, int part) => part <= 1 ? path
        : Path.Combine(Path.GetDirectoryName(path) ?? "", $"{Path.GetFileNameWithoutExtension(path)}_{part}{Path.GetExtension(path)}");

    /// <summary>
    /// Options that copy <paramref name="from"/>–<paramref name="to"/> (milliseconds) of a local file into <paramref name="path"/>
    /// in the background: no display, no sound, as fast as the disk allows. A lossless copy begins at the keyframe at or before
    /// <paramref name="from"/>.
    /// </summary>
    internal static string[] ClipOptions(string path, long from, long to) =>
    [
        $":sout=#std{{access=file,mux=mkv,dst=\"{path.Replace('\\', '/').Replace("\"", "\\\"")}\"}}",
        StartTime(from),
        ":stop-time=" + (to / 1000.0).ToString("0.###", CultureInfo.InvariantCulture)
    ];

    /// <summary>
    /// Whether a stream gets the "Kayda hazır" copy: live provider streams only. Movies, episodes and archive programmes can
    /// be paused and sought, and with a stream-output copy attached VLC has to stop and realign that copy on every pause and
    /// jump, which makes both noticeably slow. Local files are never copied either.
    /// </summary>
    internal static bool UsesTap(Channel channel, bool recordReady) =>
        recordReady && !channel.IsOnDemand && !channel.Stream.IsFile;

    /// <summary>
    /// "Kayda hazır": shows the stream and serves an untouched MPEG-TS copy of it on 127.0.0.1 only, under a random secret
    /// path, for Zaplio to read (see TsRecorder). Nothing leaves this computer; when nobody reads, VLC drops the copy.
    /// </summary>
    internal static string TapOutput(int port, string secret) =>
        $":sout=#duplicate{{dst=display,dst=std{{access=http,mux=ts,dst=127.0.0.1:{port}/{secret}.ts}}}}";

    /// <summary>A free TCP port on this computer's loopback address.</summary>
    internal static int FreeLoopbackPort()
    {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>Copies a recorded stream into Matroska unchanged (the background conversion of a recording to .mkv).</summary>
    internal static string MkvOutput(string path) =>
        $":sout=#std{{access=file,mux=mkv,dst=\"{path.Replace('\\', '/').Replace("\"", "\\\"")}\"}}";

    /// <summary>Seeks a restarted on-demand stream before it starts, so the recording begins where you pressed record.</summary>
    internal static string StartTime(long milliseconds) =>
        ":start-time=" + (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>Playback speeds, finer around normal and in slow motion, and the steps between them.</summary>
internal static class PlaybackSpeed
{
    internal static readonly float[] Ladder = [0.25f, 0.33f, 0.5f, 0.6f, 0.75f, 0.85f, 1f, 1.1f, 1.25f, 1.5f, 1.75f, 2f, 3f];

    /// <summary>The next speed down (<paramref name="faster"/> false) or up from <paramref name="current"/>.</summary>
    internal static float Step(float current, bool faster)
    {
        if (faster)
        {
            foreach (var rate in Ladder) if (rate > current + 0.005f) return rate;
            return Ladder[^1];
        }
        for (var index = Ladder.Length - 1; index >= 0; index--) if (Ladder[index] < current - 0.005f) return Ladder[index];
        return Ladder[0];
    }

    internal static string Label(float rate) =>
        Math.Abs(rate - 1f) < 0.005f ? Lang.T("Normal hız") : $"{rate.ToString("0.##", Lang.Culture)}×";

    /// <summary>Intermediate speeds for a gentle change (instead of a jump), ending exactly on <paramref name="target"/>.</summary>
    internal static float[] Ramp(float from, float target, int steps = 5)
    {
        if (steps < 1 || Math.Abs(target - from) < 0.005f) return [target];
        return Enumerable.Range(1, steps).Select(step => step == steps ? target : from + (target - from) * step / steps).ToArray();
    }
}
