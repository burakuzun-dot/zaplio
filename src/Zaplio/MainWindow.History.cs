using System.Windows;

namespace Zaplio;

public partial class MainWindow
{
    private Channel? _recordedPlayback;

    private void RecordWatch(Channel channel)
    {
        if (ReferenceEquals(_recordedPlayback, channel)) return;
        _recordedPlayback = channel;
        var history = _state.WatchHistory;
        var previous = history.ToArray();
        // Archive (catch-up) programmes of one channel share a single history row: the latest one watched.
        var archiveOf = WatchHistory.ArchiveChannelId(channel.Id);
        history.RemoveAll(entry => entry.Channel.Id == channel.Id ||
            (archiveOf is not null && WatchHistory.ArchiveChannelId(entry.Channel.Id) == archiveOf));
        history.Insert(0, new WatchEntry(channel, DateTimeOffset.UtcNow));
        var older = history.Where(entry => entry.Channel.SourceId == channel.SourceId).Skip(200).ToArray();
        foreach (var entry in older) history.Remove(entry);
        try { SaveState(); }
        catch (SourceException exception)
        {
            history.Clear();
            history.AddRange(previous);
            SetFooter(exception.Message, isError: true);
        }
    }

    private async void OnWatchHistory(object sender, RoutedEventArgs e)
    {
        var source = ActiveSource;
        var entries = _state.WatchHistory.Where(entry => entry.Channel.SourceId == source?.Id && ChildMayWatch(entry.Channel));
        var dialog = new WatchHistoryWindow(source?.Name ?? "Aktif kaynak yok", entries, _state.WatchPositions,
            _state.Settings.ResumeOnDemand) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedChannel is not { } selected) return;
        if (!selected.IsOnDemand)
        {
            await LoadActiveCatalogAsync(refresh: false);
            // Look in the provider list itself: a quality variant may not be the one the merged row shows.
            var current = _activeSourceId is Guid id
                ? _catalog.GetValueOrDefault(id)?.Channels.FirstOrDefault(channel => channel.Id == selected.Id) : null;
            if (current is null)
            {
                SetFooter("Bu kanal aktif kaynakta artık bulunmuyor.", isError: true);
                return;
            }
            selected = current;
        }
        await PlayChannelAsync(selected);
    }
}
