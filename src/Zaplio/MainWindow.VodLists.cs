using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zaplio;

/// <summary>Favourites and "watch later" for movies and series.</summary>
public partial class MainWindow
{
    private enum VodListKind { All, Favorites, Watchlist }

    private VodListKind _vodListKind;

    /// <summary>The movie or series a favourite / watch-later action applies to (the series while browsing its episodes).</summary>
    private VodItem? VodListTarget => _selectedSeries ?? (VodList.SelectedItem as VodRow)?.Item;

    private string VodKey(VodItem item) => VodLists.Key(_activeSourceId ?? Guid.Empty, _librarySection, item.ProviderId);

    private bool IsVodFavorite(VodItem item) => _state.VodFavorites.Contains(VodKey(item));

    private bool IsOnVodWatchlist(VodItem item) => _state.VodWatchlist.Contains(VodKey(item));

    private void OnVodListKindChanged(object sender, RoutedEventArgs e)
    {
        _vodListKind = sender == VodFavoritesTab ? VodListKind.Favorites : sender == VodWatchlistTab ? VodListKind.Watchlist : VodListKind.All;
        UpdateVodRows();
        if (_isReady) Motion.StaggerRows(VodList);
    }

    /// <param name="keepRowsForAnimation">Wait for the bolt animation in the row before the list is rebuilt.</param>
    private async Task ToggleVodListAsync(HashSet<string> list, VodItem item, string added, string removed, bool keepRowsForAnimation = false)
    {
        var key = VodKey(item);
        var onList = VodLists.Toggle(list, key);
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            VodLists.Toggle(list, key);
            SetFooter(exception.Message, isError: true);
            UpdateVodRows();
            UpdateVodPreview();
            return;
        }
        SetFooter($"{item.Title} {(onList ? added : removed)}");
        UpdateVodListButtons();
        if (keepRowsForAnimation && onList && SystemParameters.ClientAreaAnimation) await Task.Delay(700);
        if (_closing) return;
        UpdateVodRows();
        UpdateVodPreview();
    }

    private Task ToggleVodFavoriteAsync(VodItem item, bool keepRowsForAnimation = false) =>
        ToggleVodListAsync(_state.VodFavorites, item, "favorilere eklendi.", Lang.T("favorilerden çıkarıldı."),
            // In the favourites tab the row has to go at once when it is removed; adding cannot happen there.
            keepRowsForAnimation && _vodListKind != VodListKind.Favorites);

    private Task ToggleVodWatchlistAsync(VodItem item) =>
        ToggleVodListAsync(_state.VodWatchlist, item, "izleme listene eklendi.", Lang.T("izleme listenden çıkarıldı."));

    private async void OnVodRowFavoriteToggled(object? sender, bool added)
    {
        if ((sender as FrameworkElement)?.DataContext is VodRow { CanFavorite: true } row)
            await ToggleVodFavoriteAsync(row.Item, keepRowsForAnimation: true);
    }

    private async void OnVodPreviewFavoriteToggled(object? sender, bool added)
    {
        if (VodListTarget is { } item) await ToggleVodFavoriteAsync(item);
    }

    private async void OnVodWatchlist(object sender, RoutedEventArgs e)
    {
        if (VodListTarget is { } item) await ToggleVodWatchlistAsync(item);
    }

    /// <summary>Keeps the bolt and "Sonra izle" next to "Oynat" in step with the selected movie or series.</summary>
    private void UpdateVodListButtons()
    {
        var item = VodListTarget;
        var whole = _selectedSeries is null ? "" : Lang.T("Diziyi ");
        VodFavoriteToggle.ChannelName = item?.Title ?? "";
        VodFavoriteToggle.IsFavorite = item is not null && IsVodFavorite(item);

        var later = item is not null && IsOnVodWatchlist(item);
        var tooltip = later ? Lang.F($"{whole}izleme listemden çıkar") : Lang.F($"{whole}sonra izlemek için ekle");
        VodWatchlistText.Text = later ? Lang.T("Listemde") : Lang.T("Sonra izle");
        VodWatchlistButton.SetResourceReference(ForegroundProperty, later ? "Accent" : "Muted");
        VodWatchlistButton.SetResourceReference(BackgroundProperty, later ? "Active" : "Surface");
        VodWatchlistButton.SetResourceReference(BorderBrushProperty, later ? "ActiveLine" : "Line");
        VodWatchlistButton.ToolTip = tooltip;
        AutomationProperties.SetName(VodWatchlistButton, tooltip);
    }

    private void OnVodMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = VodList.ContextMenu!;
        menu.Items.Clear();
        if (e.OriginalSource is not DependencyObject target ||
            ItemsControl.ContainerFromElement(VodList, target) is not ListBoxItem { DataContext: VodRow row })
        {
            e.Handled = true;
            return;
        }
        VodList.SelectedItem = row;
        var item = VodListTarget ?? row.Item;
        var whole = _selectedSeries is null ? "" : Lang.T("Diziyi ");
        Add(menu.Items, row.Item.Playable is null ? Lang.T("Bölümleri aç") : Lang.T("Oynat"), () => _ = ActivateVodAsync(row.Item));
        menu.Items.Add(new Separator());
        Add(menu.Items, IsVodFavorite(item) ? Lang.F($"{whole}favorilerden çıkar") : $"{whole}favorilere ekle", () => _ = ToggleVodFavoriteAsync(item));
        Add(menu.Items, IsOnVodWatchlist(item) ? Lang.F($"{whole}izleme listemden çıkar") : Lang.F($"{whole}sonra izlemek için ekle"),
            () => _ = ToggleVodWatchlistAsync(item));
    }
}
