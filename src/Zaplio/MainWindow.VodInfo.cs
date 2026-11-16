using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Zaplio;

/// <summary>
/// The panel under the player while browsing movies and series: facts and credits of the selection, the next episodes of a
/// series and "continue watching". It uses the room the 16:9 preview leaves below itself and disappears during playback.
/// </summary>
public partial class MainWindow
{
    private const double VodInfoGap = 14;
    private const double VodInfoMinimum = 110;
    private const double VodInfoWithContinue = 235;

    /// <summary>Detail responses already received, so selecting the same item again does not ask the provider twice.</summary>
    private readonly Dictionary<(Guid Source, LibrarySection Section, long Id), VodItem> _vodDetailsCache = new();

    private void RememberVodDetails(Guid source, VodItem details)
    {
        if (_vodDetailsCache.Count >= 500) _vodDetailsCache.Clear();
        _vodDetailsCache[(source, _librarySection, details.ProviderId)] = details;
    }

    private VodItem? CachedVodDetails(VodItem item) =>
        _activeSourceId is { } source && _vodDetailsCache.TryGetValue((source, _librarySection, item.ProviderId), out var details) ? details : null;

    /// <param name="playerHeight">The capped player height, or NaN when the player fills the column.</param>
    /// <param name="browsingHeight">The player height with its title and controls, whether or not a preview hides them now.</param>
    private void PlaceVodInfoPanel(double playerHeight, double columnHeight, double browsingHeight)
    {
        var top = PlayerPane.Margin.Top + playerHeight + VodInfoGap;
        var room = columnHeight - top - VodInfoPanel.Margin.Bottom;
        var show = _librarySection != LibrarySection.Live && !double.IsNaN(playerHeight) && double.IsFinite(room) && room >= VodInfoMinimum;
        VodInfoPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            var margin = new Thickness(PlayerPane.Margin.Left, top, PlayerPane.Margin.Right, VodInfoPanel.Margin.Bottom);
            if (VodInfoPanel.Margin != margin) VodInfoPanel.Margin = margin;
            if (!VodInfoPanel.Height.Equals(room)) VodInfoPanel.Height = room;
        }

        // "Continue watching" moves under the player when there is room for it beside the facts; otherwise it stays above the list.
        // Decided from the browsing layout, so selecting a title (which hides the player's title and controls) does not move it.
        var browsingRoom = columnHeight - PlayerPane.Margin.Top - browsingHeight - VodInfoGap - VodInfoPanel.Margin.Bottom;
        var underPlayer = show && browsingRoom >= VodInfoWithContinue;
        var host = underPlayer ? ContinueHostInfo : ContinueHostList;
        if (!ReferenceEquals(ContinuePanel.Parent, host))
        {
            if (ContinuePanel.Parent is Border previous) previous.Child = null;
            ContinuePanel.Margin = underPlayer ? new Thickness(0, 12, 0, 0) : new Thickness(0, 0, 0, 8);
            host.Child = ContinuePanel;
        }
        UpdateVodInfo();
    }

    private void UpdateVodInfo()
    {
        if (VodInfoPanel.Visibility != Visibility.Visible)
        {
            if (SelectedVodPreview is { Summary: { Length: > 0 } }) VodPreviewSummary.Visibility = Visibility.Visible;
            return;
        }

        var row = VodList.SelectedItem as VodRow;
        var item = row is null ? null
            : _vodPreviewDetails is { } loaded && loaded.ProviderId == row.Item.ProviderId ? loaded : CachedVodDetails(row.Item) ?? row.Item;
        if (item is null && _selectedSeries is { } series) item = CachedVodDetails(series) ?? series;

        var facts = item?.Facts;
        var chips = new List<string>();
        if (item?.Detail is { } year && Regex.IsMatch(year, @"^\d{4}$")) chips.Add(year);
        if (facts is not null) chips.AddRange(facts.Chips);
        var plot = string.IsNullOrWhiteSpace(item?.Plot) ? null : item!.Plot!.Trim();

        VodInfoHeading.Text = item is null ? "Hakkında" : row is null && _selectedSeries is not null ? $"{item.Title} hakkında" : "Hakkında";
        VodInfoChips.ItemsSource = chips;
        VodInfoChips.Visibility = chips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        VodInfoPlot.Text = plot;
        VodInfoPlot.Visibility = plot is null ? Visibility.Collapsed : Visibility.Visible;
        VodInfoDirector.Text = facts?.Director;
        VodInfoCast.Text = facts?.Cast;
        VodInfoDirectorLabel.Visibility = VodInfoDirector.Visibility = facts?.Director is null ? Visibility.Collapsed : Visibility.Visible;
        VodInfoCastLabel.Visibility = VodInfoCast.Visibility = facts?.Cast is null ? Visibility.Collapsed : Visibility.Visible;
        VodInfoCredits.Visibility = facts?.Director is null && facts?.Cast is null ? Visibility.Collapsed : Visibility.Visible;

        var empty = chips.Count == 0 && plot is null && VodInfoCredits.Visibility != Visibility.Visible;
        VodInfoHint.Text = item is null
            ? _librarySection == LibrarySection.Series ? "Bir dizi seç; konusu, türü ve oyuncuları burada görünür."
                : "Bir film seç; konusu, türü, süresi ve oyuncuları burada görünür."
            : _vodPreviewRequest is not null ? "Ayrıntılar yükleniyor…" : "Bu içerik için ayrıntı bilgisi yok.";
        VodInfoHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;

        // The plot is shown in full here, so the preview card does not repeat it.
        if (plot is not null) VodPreviewSummary.Visibility = Visibility.Collapsed;

        var next = NextEpisodes(row);
        VodNextEpisodes.ItemsSource = next;
        VodNextEpisodesPanel.Visibility = next.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Up to three episodes after the selected one, in list order.</summary>
    private List<VodRow> NextEpisodes(VodRow? selected)
    {
        if (_selectedSeries is null || selected is null) return [];
        var index = VodList.Items.IndexOf(selected);
        if (index < 0) return [];
        return VodList.Items.OfType<VodRow>().Skip(index + 1).Where(row => row.Item.Playable is not null).Take(3).ToList();
    }

    private void OnNextEpisodeRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VodRow row) return;
        VodList.SelectedItem = row;
        VodList.ScrollIntoView(row);
    }
}
