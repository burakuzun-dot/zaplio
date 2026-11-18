using System.Windows;

namespace Zaplio;

/// <summary>"İzlemeye devam et": half-watched movies and episodes above the movie and series lists.</summary>
public partial class MainWindow
{
    /// <param name="Subtitle">The episode name under a series title; null for a movie.</param>
    private sealed record ContinueRow(Channel Channel, string Title, string? Subtitle, string Caption, string? Poster, string Tile, string Tooltip);

    /// <param name="showRow">Only on the plain list (all items, no category, no search, not inside a series).</param>
    private void UpdateContinueWatching(bool showRow)
    {
        if (!showRow || _activeSourceId is not { } source || !_state.Settings.ResumeOnDemand)
        {
            ContinuePanel.Visibility = Visibility.Collapsed;
            ContinueList.ItemsSource = null;
            UpdateVodInfo();
            return;
        }

        var series = _librarySection == LibrarySection.Series;
        var rows = ContinueWatching.Select(_state.WatchHistory, source, _librarySection, _state.WatchPositions, _state.CompletedItems)
            .Where(entry => ChildMayWatch(entry.Channel))
            .Select(entry =>
            {
                var channel = entry.Channel;
                var item = series
                    ? _vodItems.FirstOrDefault(candidate => candidate.ProviderId == channel.SeriesId)
                    : _vodItems.FirstOrDefault(candidate => candidate.Playable?.Id == channel.Id);
                var title = item?.Title ?? channel.Name;
                var time = PlaybackTime.Format(entry.Position);
                var episode = series && !string.Equals(channel.Name, title, StringComparison.CurrentCultureIgnoreCase) ? channel.Name : null;
                return new ContinueRow(channel, title, episode, Lang.F($"Devam et · {time}"), item?.Poster, series ? IconGlyphs.Series : IconGlyphs.Movie,
                    Lang.F($"{title}: kaldığın yerden devam ({time})"));
            }).ToArray();
        ContinueList.Tag ??= _continueShelf;
        ContinueList.ItemsSource = rows;
        ContinuePanel.Visibility = rows.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateVodInfo();
        FitContinueShelf();
    }

    /// <summary>
    /// Opens the item the way the list does: its poster preview with "Devam et · …" appears, nothing plays until the user presses it.
    /// For an episode the series is opened first and the episode is selected inside it.
    /// </summary>
    private async void OnContinueWatching(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ContinueRow row) return;
        var channel = row.Channel;
        if (channel.SeriesId > 0 && _selectedSeries?.ProviderId != channel.SeriesId)
        {
            if (_vodItems.FirstOrDefault(item => item.ProviderId == channel.SeriesId && item.Playable is null) is not { } series)
            {
                SetFooter(Lang.F($"{row.Title} listede bulunamadı; listeyi yenileyip tekrar dene."), isError: true);
                return;
            }
            await ActivateVodAsync(series); // loads the episodes
            if (_selectedSeries?.ProviderId != channel.SeriesId) return; // the user moved on meanwhile
        }

        var match = VodList.Items.OfType<VodRow>().FirstOrDefault(candidate => candidate.Item.Playable?.Id == channel.Id);
        if (match is null)
        {
            SetFooter(Lang.F($"{row.Title} listede bulunamadı; listeyi yenileyip tekrar dene."), isError: true);
            return;
        }
        VodList.SelectedItem = match;
        VodList.ScrollIntoView(match);
    }
}
