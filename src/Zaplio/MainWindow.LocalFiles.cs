using System.IO;
using System.Windows;
using LibVLCSharp.Shared;
using Microsoft.Win32;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

/// <summary>
/// Videos from this computer: drop files or a folder on the window, press Ctrl + O, or open a file with Zaplio from Windows.
/// They play in the same player (cinema mode, mini window, second screen, resume); a folder or several files play one after
/// another, and a subtitle file dropped on a playing video is added to it.
/// </summary>
public partial class MainWindow
{
    private readonly List<Channel> _localQueue = [];
    private string? _pendingSubtitle;

    internal int LocalQueueCount => _localQueue.Count;

    /// <summary>Plays the first video among <paramref name="paths"/> and queues the rest. Returns the first video's entry.</summary>
    internal async Task<Channel?> OpenLocalAsync(IEnumerable<string> paths, bool play = true)
    {
        var (videos, subtitles) = LocalMedia.Sort(paths);
        if (videos.Count == 0)
        {
            if (subtitles.Count > 0 && _player is { } player && _playingChannel is not null)
            {
                AddSubtitle(player, subtitles[0]);
                return null;
            }
            SetFooter(subtitles.Count > 0 ? Lang.T("Altyazı eklemek için önce bir video oynat.") : Lang.T("Bu dosya oynatılamıyor; bir video dosyası bırak."),
                isError: subtitles.Count == 0);
            return null;
        }

        var entries = videos.Select(LocalMedia.ToChannel).ToList();
        _localQueue.Clear();
        _localQueue.AddRange(entries.Skip(1));
        _pendingSubtitle = subtitles.FirstOrDefault();
        SetFooter(entries.Count > 1 ? Lang.F($"{entries[0].Name} · sırada {entries.Count - 1} video daha") : entries[0].Name);
        AppLog.Info($"Yerel dosya açıldı ({entries.Count} video).");
        if (play) await PlayChannelAsync(entries[0]);
        return entries[0];
    }

    /// <summary>Plays the next queued local video when one finishes. Returns false when there is none.</summary>
    private async Task<bool> PlayNextLocalAsync()
    {
        if (_localQueue.Count == 0) return false;
        var next = _localQueue[0];
        _localQueue.RemoveAt(0);
        await PlayChannelAsync(next);
        return true;
    }

    private void AddSubtitle(VlcPlayer player, string path)
    {
        var added = player.AddSlave(MediaSlaveType.Subtitle, new Uri(Path.GetFullPath(path)).AbsoluteUri, true);
        ShowOsd(added ? Lang.T("Altyazı eklendi") : Lang.T("Altyazı eklenemedi"), Path.GetFileName(path));
    }

    /// <summary>A subtitle dropped together with the video is added once the video is playing.</summary>
    private void ApplyPendingSubtitle(VlcPlayer player)
    {
        if (_pendingSubtitle is not { } subtitle || _playingChannel is not { } channel || !LocalMedia.IsLocal(channel)) return;
        _pendingSubtitle = null;
        AddSubtitle(player, subtitle);
    }

    private async void OnOpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = Lang.T("Video aç"), Filter = LocalMedia.DialogFilter, Multiselect = true, CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) await OpenLocalAsync(dialog.FileNames);
    }

    /// <summary>
    /// Zaplio was started again (for example "Birlikte aç" on a video) while this window is open: come forward, and play the
    /// files it was given, if any.
    /// </summary>
    internal void OpenFromAnotherLaunch(IReadOnlyList<string> paths)
    {
        if (_closing) return;
        if (!IsLoaded || !IsVisible)
        {
            OpenWhenShown(paths); // still behind the intro animation
            return;
        }
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true; // a short nudge so Windows lets the window come to the front
        Topmost = false;
        Focus();
        if (paths.Count > 0) _ = OpenLocalAsync(paths);
    }

    /// <summary>Files given on the command line ("Birlikte aç" in Windows) open once the window is on screen.</summary>
    internal void OpenWhenShown(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;
        EventHandler? shown = null;
        shown = async (_, _) =>
        {
            ContentRendered -= shown;
            await OpenLocalAsync(paths);
        };
        ContentRendered += shown;
    }

    // ── drag and drop ──────────────────────────────────────────────────────────────────────────────────

    private static string[] DroppedPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths ? paths : [];

    private void OnFileDragOver(object sender, DragEventArgs e)
    {
        var paths = DroppedPaths(e);
        var usable = paths.Any(path => Directory.Exists(path) || LocalMedia.IsVideo(path) || LocalMedia.IsSubtitle(path));
        e.Effects = usable ? DragDropEffects.Copy : DragDropEffects.None;
        DropHint.Visibility = usable ? Visibility.Visible : Visibility.Collapsed;
        if (usable)
            DropHintText.Text = paths.All(LocalMedia.IsSubtitle) ? Lang.T("Altyazıyı bırak, videoya eklensin") : Lang.T("Bırak, hemen oynasın");
        e.Handled = true;
    }

    private void OnFileDragLeave(object sender, DragEventArgs e) => DropHint.Visibility = Visibility.Collapsed;

    private async void OnFileDrop(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        var paths = DroppedPaths(e);
        if (paths.Length == 0) return;
        e.Handled = true;
        Activate();
        await OpenLocalAsync(paths);
    }
}
