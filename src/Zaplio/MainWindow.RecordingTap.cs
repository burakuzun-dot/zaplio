using System.IO;
using System.Net.Http;
using System.Windows;
using LibVLCSharp.Shared;

namespace Zaplio;

/// <summary>
/// "Kayda hazır": a provider's stream is played with a copy of itself served on this computer only (127.0.0.1, an address
/// with a random secret path). Zaplio reads that copy all the time and drops it, so pressing record just starts writing it to a
/// file at the next keyframe, and stopping just stops: the stream never restarts, nothing freezes or reloads, and the provider
/// sees no extra connection. Without the setting (or if the copy cannot be read) recording falls back to restarting the stream.
/// </summary>
public partial class MainWindow
{
    private readonly TsRecorder _tsRecorder = new();
    private CancellationTokenSource? _tapReader;
    private volatile bool _tapFlowing;
    private string? _tapRecording; // the .ts being written from the copy, converted to .mkv when the recording ends

    /// <summary>Whether recording can start and stop without touching the stream right now.</summary>
    private bool CanRecordFromTap => _tapFlowing && _tapReader is not null;

    /// <summary>The copy's option for a stream about to start, and the address to read it from; nothing for local files.</summary>
    private (string? Option, string? Address) PrepareTap(Channel channel)
    {
        if (!RecordingPlan.UsesTap(channel, _state.Settings.RecordReady)) return (null, null);
        var port = RecordingPlan.FreeLoopbackPort();
        var secret = Guid.NewGuid().ToString("N");
        return (RecordingPlan.TapOutput(port, secret), $"http://127.0.0.1:{port}/{secret}.ts");
    }

    /// <summary>Starts reading the copy of the stream that just started. Called once per player.</summary>
    private void StartTapReader(string address)
    {
        StopTapReader();
        var cancel = new CancellationTokenSource();
        _tapReader = cancel;
        _tapFlowing = false;
        _tsRecorder.NewStream();
        _ = Task.Run(() => ReadTapAsync(address, cancel.Token));
    }

    private void StopTapReader()
    {
        _tapReader?.Cancel();
        _tapReader?.Dispose();
        _tapReader = null;
        _tapFlowing = false;
    }

    private async Task ReadTapAsync(string address, CancellationToken token)
    {
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        // VLC opens the address a moment after playback starts.
        for (var attempt = 0; attempt < 60 && !token.IsCancellationRequested; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(150, token);
                    continue;
                }
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[64 * 1024];
                int read;
                while ((read = await stream.ReadAsync(buffer, token)) > 0)
                {
                    _tapFlowing = true;
                    _tsRecorder.Feed(buffer.AsSpan(0, read));
                }
                return; // the stream ended with its player
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                if (_tapFlowing) return; // it was flowing and then closed: the player is gone
                try { await Task.Delay(150, token); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Begins writing the flowing copy into a file. Nothing happens to the picture.</summary>
    private bool StartTapRecording(string finalPath)
    {
        var working = Path.ChangeExtension(finalPath, ".ts");
        try
        {
            _tsRecorder.Start(new FileStream(working, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Kayıt dosyası açılamadı.", exception);
            return false;
        }
        _tapRecording = working;
        return true;
    }

    /// <summary>Stops writing and turns the recorded stream into the final .mkv in the background (no re-encoding).</summary>
    private async Task FinishTapRecordingAsync(string finalPath)
    {
        var working = _tapRecording;
        _tapRecording = null;
        var written = _tsRecorder.Stop();
        if (working is null) return;
        if (written == 0)
        {
            TryDelete(working);
            _recordParts.Clear();
            AnnounceRecording(finalPath);
            return;
        }
        SetFooter("Kayıt hazırlanıyor…");
        var converted = _engine is { } engine &&
            await CopyWithoutPictureAsync(engine, new Uri(working), [RecordingPlan.MkvOutput(finalPath)], expectedMs: 0);
        if (_closing) return;
        _recordParts.Clear();
        if (converted && File.Exists(finalPath) && new FileInfo(finalPath).Length > 0)
        {
            TryDelete(working);
            _recordParts.Add(finalPath);
        }
        else _recordParts.Add(working); // the stream itself is a playable recording too
        AnnounceRecording(finalPath);
    }

    /// <summary>Runs a stream-output copy with a player that has no picture and no sound, and waits for it to finish.</summary>
    private static Task<bool> CopyWithoutPictureAsync(LibVLC engine, Uri source, IEnumerable<string> options, long expectedMs) => Task.Run(async () =>
    {
        try
        {
            using var media = new Media(engine, source);
            foreach (var option in options) media.AddOption(option);
            using var copier = new MediaPlayer(engine); // never attached to a video view
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            copier.EndReached += (_, _) => finished.TrySetResult(true);
            copier.EncounteredError += (_, _) => finished.TrySetResult(false);
            if (!copier.Play(media)) return false;
            var limit = TimeSpan.FromMilliseconds(Math.Clamp(expectedMs / 5 + 60_000, 60_000, 15 * 60_000));
            var done = await Task.WhenAny(finished.Task, Task.Delay(limit)) == finished.Task && finished.Task.Result;
            copier.Stop();
            return done;
        }
        catch (Exception exception)
        {
            AppLog.Error("Kayıt kopyalanamadı.", exception);
            return false;
        }
    });

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
}
