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

    private bool _continueFitsWithDetails;
    private readonly ContinueShelfLayout _continueShelf = new();

    /// <param name="playerHeight">The capped player height, or NaN when the player fills the column.</param>
    private void PlaceVodInfoPanel(double playerHeight, double columnHeight)
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

        // Whenever the panel is there, "continue watching" lives in it: alone while nothing is selected, under the facts of a
        // selection when both fit. The list on the left therefore never jumps when a title is selected.
        _continueFitsWithDetails = show && room >= VodInfoWithContinue;
        var host = show ? ContinueHostInfo : ContinueHostList;
        ContinueList.Tag ??= _continueShelf;
        if (!ReferenceEquals(ContinuePanel.Parent, host))
        {
            if (ContinuePanel.Parent is Border previous) previous.Child = null;
            if (!show)
            {
                ContinuePanel.Margin = new Thickness(0);
                _continueShelf.UseDefault();
            }
            // Above the list the row scrolls sideways (drag or wheel); under the player the wrapped rows scroll down.
            ContinueScroll.HorizontalScrollBarVisibility = show ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
            ContinueScroll.VerticalScrollBarVisibility = show ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            ContinueScroll.PanningMode = show ? PanningMode.VerticalOnly : PanningMode.HorizontalOnly;
            DragScroll.SetIsEnabled(ContinueScroll, !show);
            ContinueScroll.ScrollToHome();
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

        // Nothing selected: the panel shows what you can pick up again instead of the "select something" hint.
        var idle = item is null;
        var continueFirst = idle && ContinuePanel.Visibility == Visibility.Visible;
        VodInfoScroll.Visibility = continueFirst ? Visibility.Collapsed : Visibility.Visible;
        var continueShown = idle || _continueFitsWithDetails;
        ContinueHostInfo.Visibility = continueShown ? Visibility.Visible : Visibility.Collapsed;
        ContinuePanel.Margin = continueFirst ? new Thickness(0) : new Thickness(0, 12, 0, 0);
        // The facts take what they need; the cards below get the rest, but never less than their smallest size.
        var shelfMinimum = ContinuePanel.Visibility == Visibility.Visible && continueShown ? ContinueShelfLayout.MinimumCardHeight + 50 : 0;
        VodInfoScroll.MaxHeight = double.IsFinite(VodInfoPanel.Height) ? Math.Max(60, VodInfoPanel.Height - shelfMinimum) : double.PositiveInfinity;
        // The margin above the cards may have changed without the host resizing; fit again once layout has settled.
        Dispatcher.BeginInvoke(FitContinueShelf, System.Windows.Threading.DispatcherPriority.Loaded);

        // The plot is shown in full here, so the preview card does not repeat it.
        if (plot is not null) VodPreviewSummary.Visibility = Visibility.Collapsed;

        var next = NextEpisodes(row);
        VodNextEpisodes.ItemsSource = next;
        VodNextEpisodesPanel.Visibility = next.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnContinueHostInfoSizeChanged(object sender, SizeChangedEventArgs e) => FitContinueShelf();

    /// <summary>Fits the cards under the player to the panel's width and to the height left for them.</summary>
    private void FitContinueShelf()
    {
        if (!ReferenceEquals(ContinuePanel.Parent, ContinueHostInfo) || ContinueHostInfo.ActualWidth <= 0) return;
        var heading = ContinuePanel.Children[0] is FrameworkElement label ? label.ActualHeight + label.Margin.Top + label.Margin.Bottom : 28;
        var height = ContinueHostInfo.ActualHeight - ContinuePanel.Margin.Top - heading;
        var count = ContinueList.Items.Count;
        _continueShelf.Fit(ContinueHostInfo.ActualWidth, height, count);
        // A scroll bar only when some rows do not fit; otherwise it would narrow the row and push the last card down.
        var overflow = count > _continueShelf.Columns * _continueShelf.Rows;
        if (overflow) _continueShelf.Fit(ContinueHostInfo.ActualWidth - SystemParameters.VerticalScrollBarWidth - 1, height, count);
        var bar = overflow ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        if (ContinueScroll.VerticalScrollBarVisibility != bar) ContinueScroll.VerticalScrollBarVisibility = bar;
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
