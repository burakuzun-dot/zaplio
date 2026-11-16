using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zaplio;

public partial class MainWindow
{
    private sealed record VodFilter(string? Category, string Label);

    private sealed record VodRow(VodItem Item, string Caption, string Tile, bool IsFavorite = false, bool CanFavorite = false) : IVodListItem
    {
        /// <summary>Movies and series are poster tiles; the episodes of a series are rows.</summary>
        public bool IsTile => CanFavorite;
        public string Title => Item.Title;
        public override string ToString() => Title;
    }

    private readonly Dictionary<(Guid Source, LibrarySection Section), IReadOnlyList<VodItem>> _vodCatalogs = new();
    private LibrarySection _librarySection;
    private IReadOnlyList<VodItem> _vodItems = [];
    private VodItem? _selectedSeries;
    private (string Search, string? Category)? _seriesFilters;
    private CancellationTokenSource? _vodRequest;
    private string? _vodError;
    private bool _changingVodFilters;
    private string? _vodPreviewError;
    private VodItem? _vodPreviewDetails;
    private CancellationTokenSource? _vodPreviewRequest;

    private Channel? SelectedVod => (VodList.SelectedItem as VodRow)?.Item.Playable;

    private VodPreview? SelectedVodPreview
    {
        get
        {
            if (_librarySection == LibrarySection.Live || Immersive || VodList.SelectedItem is not VodRow row ||
                (_playingChannel is not null && _playingChannel.Id == row.Item.Playable?.Id)) return null;
            return VodPreview.Create(_vodPreviewDetails ?? row.Item, _selectedSeries, _activeSourceId, _state);
        }
    }

    private VirtualizingTilePanel? _vodPanel;

    private void OnVodPanelLoaded(object sender, RoutedEventArgs e)
    {
        _vodPanel = (VirtualizingTilePanel)sender;
        ApplyVodPanelMode();
    }

    /// <summary>Poster grid for movies and series, a one-column list for the episodes of a series.</summary>
    private void ApplyVodPanelMode()
    {
        if (_vodPanel is not { } panel) return;
        var tiles = _selectedSeries is null;
        panel.MinTileWidth = tiles ? 136 : 100_000;
        panel.TileAspect = tiles ? 1.5 : 0;
        panel.TileExtraHeight = tiles ? 56 : 70;
    }

    /// <summary>
    /// The catalog and player share the window while browsing. Playback and episode lists give more room to the player.
    /// </summary>
    private void ApplyVodPaneWidth()
    {
        if (_librarySection == LibrarySection.Live || Immersive) return;
        var wide = _playingChannel is null && _selectedSeries is null;
        var maximum = wide ? double.PositiveInfinity : 410;
        var width = new GridLength(wide ? 2 : 1, GridUnitType.Star);
        if (!ChannelWidth.MaxWidth.Equals(maximum)) ChannelWidth.MaxWidth = maximum;
        if (ChannelWidth.Width != width) ChannelWidth.Width = width;
    }

    private bool _watchingPlayerColumn;

    private void UpdateVodPlayerHeight()
    {
        if (!_isReady) return;

        // Cap the browsing panel as a group so the controls stay beside the 16:9 preview when there is extra height.
        // A short window can still shrink the star-sized video row, and active playback can use the whole pane.
        var browsing = _librarySection != LibrarySection.Live && _playingChannel is null && !Immersive;
        var maximum = browsing && PlayerPane.ActualWidth > 0
            ? PlayerPane.ActualWidth * 9 / 16 + OccupiedHeight(PlayerHeader) + OccupiedHeight(PlayerControls)
            : double.PositiveInfinity;
        if (PlayerPane.MaxHeight != maximum) PlayerPane.MaxHeight = maximum;

        // The capped panel sits at the top, level with the library's title, instead of floating in the middle of the column.
        // A fixed height (not just Top alignment) keeps the star-sized video row from collapsing to its content.
        if (PlayerPane.Parent is FrameworkElement column && !_watchingPlayerColumn)
        {
            _watchingPlayerColumn = true;
            column.SizeChanged += (_, e) => { if (e.HeightChanged) UpdateVodPlayerHeight(); };
        }
        var room = PlayerPane.Parent is FrameworkElement { ActualHeight: > 0 } parent
            ? parent.ActualHeight - PlayerPane.Margin.Top - PlayerPane.Margin.Bottom : double.PositiveInfinity;
        var height = double.IsFinite(maximum) && double.IsFinite(room) ? Math.Max(0, Math.Min(maximum, room)) : double.NaN;
        if (!PlayerPane.Height.Equals(height)) PlayerPane.Height = height;
        var alignment = double.IsNaN(height) ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        if (PlayerPane.VerticalAlignment != alignment) PlayerPane.VerticalAlignment = alignment;
        PlaceVodInfoPanel(height, PlayerPane.Parent is FrameworkElement { ActualHeight: > 0 } host ? host.ActualHeight : double.NaN);

        static double OccupiedHeight(FrameworkElement element) => element.Visibility == Visibility.Collapsed
            ? 0 : element.ActualHeight + element.Margin.Top + element.Margin.Bottom;
    }

    private void OnPlayerChromeSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.HeightChanged) UpdateVodPlayerHeight();
    }

    private void UpdateVodPreview()
    {
        ApplyVodPaneWidth();
        var preview = SelectedVodPreview;
        VodPreviewPanel.Visibility = preview is null ? Visibility.Collapsed : Visibility.Visible;
        MainVideoSlot.Visibility = preview is null ? Visibility.Visible : Visibility.Hidden;
        VideoOverlay.Visibility = preview is null || IsSecondScreenActive ? Visibility.Visible : Visibility.Hidden;
        UpdatePlaceholderVisibility();
        PlayerHeader.Visibility = Immersive || preview is not null ? Visibility.Collapsed : Visibility.Visible;
        PlayButton.Visibility = preview is null || _playingChannel is not null ? Visibility.Visible : Visibility.Collapsed;
        PlayerControls.Visibility = !Immersive && (preview is null || _playingChannel is not null) ? Visibility.Visible : Visibility.Collapsed;
        UpdateVodPlayerHeight();
        LogoLoader.SetUrl(VodPreviewPoster, preview?.Poster);
        if (preview is null)
        {
            UpdateVodInfo();
            return;
        }

        VodPreviewSeries.Text = _selectedSeries?.Title;
        VodPreviewSeries.Visibility = _selectedSeries is null ? Visibility.Collapsed : Visibility.Visible;
        VodPreviewIcon.Text = _librarySection == LibrarySection.Movies ? IconGlyphs.Movie : IconGlyphs.Series;
        VodPreviewTitle.Text = preview.Title;
        VodPreviewMetadata.Text = preview.Metadata;
        VodPreviewSummary.Text = preview.Summary;
        VodPreviewSummary.Visibility = string.IsNullOrWhiteSpace(preview.Summary) ? Visibility.Collapsed : Visibility.Visible;
        VodPreviewAction.Text = preview.Action;
        System.Windows.Automation.AutomationProperties.SetName(VodPreviewPlayButton, preview.Action);
        VodPreviewError.Text = _vodPreviewError;
        VodPreviewError.Visibility = _vodPreviewError is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateVodListButtons();
        UpdateVodInfo();
    }

    private async void OnVodPreviewPlay(object sender, RoutedEventArgs e)
    {
        if (SelectedVodPreview is not null && VodList.SelectedItem is VodRow row) await ActivateVodAsync(row.Item);
    }

    private void CancelVodPreviewRequest()
    {
        _vodPreviewRequest?.Cancel();
        _vodPreviewDetails = null;
    }

    private async void OnLibraryChanged(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return;
        CancelVodPreviewRequest();
        _librarySection = sender == MoviesTab ? LibrarySection.Movies : sender == SeriesTab ? LibrarySection.Series : LibrarySection.Live;
        _selectedSeries = null;
        _seriesFilters = null;
        _vodItems = [];
        VodList.SelectedItem = null;
        if (_guideOpen) SetGuideOpen(false);
        ApplyLibraryLayout();
        Motion.Reveal(_librarySection == LibrarySection.Live ? ChannelPane : VodPane, offset: 12);
        if (_librarySection == LibrarySection.Live)
        {
            CancelVodRequest();
            UpdateRefreshButton();
        }
        else
        {
            VodSearchBox.Clear();
            _vodListKind = VodListKind.All;
            VodAllTab.IsChecked = true;
        }
        await LoadActiveSectionAsync();
    }

    private void ApplyLibraryLayout()
    {
        var live = _librarySection == LibrarySection.Live;
        NavigationWidth.Width = new GridLength(live && !Immersive ? 218 : 0);
        ChannelWidth.MinWidth = !live && !Immersive ? 280 : 0;
        if (live || Immersive)
        {
            ChannelWidth.MaxWidth = double.PositiveInfinity;
            ChannelWidth.Width = Immersive ? new GridLength(0) : new GridLength(318);
        }
        else ApplyVodPaneWidth();
        NavigationPane.Visibility = ChannelPane.Visibility = live && !Immersive ? Visibility.Visible : Visibility.Collapsed;
        VodPane.Visibility = !live && !Immersive ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelection();
    }

    private void CancelVodRequest()
    {
        var request = _vodRequest;
        _vodRequest = null;
        request?.Cancel();
    }

    private async Task LoadVodViewAsync(bool refresh = false)
    {
        CancelVodPreviewRequest();
        CancelVodRequest();
        _vodError = null;
        _vodItems = [];
        if (ActiveSource is not { Kind: SourceKind.Xtream } source || _librarySection == LibrarySection.Live)
        {
            ResetVodCategories();
            UpdateVodRows();
            return;
        }

        var key = (source.Id, _librarySection);
        var series = _selectedSeries;
        if (!refresh && series is null && _vodCatalogs.TryGetValue(key, out var cached))
        {
            _vodItems = cached;
            ResetVodCategories();
            UpdateVodRows();
            return;
        }

        // Show the list saved on disk at once; refresh it from the provider only when it is old.
        var showingCache = false;
        if (!refresh && series is null)
        {
            var stored = await Task.Run(() => _catalogCache.LoadVod(source, key.Item2));
            if (_closing || _vodRequest is not null || ActiveSource?.Id != source.Id || _librarySection != key.Item2 ||
                _selectedSeries is not null || _vodCatalogs.ContainsKey(key)) return;
            if (stored is not null)
            {
                _vodCatalogs[key] = _vodItems = stored.Value;
                ResetVodCategories();
                UpdateVodRows();
                SetFooter($"{source.Name} · {stored.Value.Count.ToString("N0", Turkish)} içerik · " +
                    $"{CacheAgeLabel(DateTimeOffset.UtcNow - stored.SavedAt)} kaydedilen liste");
                if (stored.IsFresh) return;
                showingCache = true;
            }
        }

        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _vodRequest = request;
        if (!showingCache)
        {
            ResetVodCategories();
            UpdateVodRows();
        }
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var items = series is null
                ? await _loader.LoadVodAsync(source, key.Item2, request.Token)
                : await _loader.LoadEpisodesAsync(source, series, request.Token);
            if (request != _vodRequest) return;
            AppLog.Info($"İçerik listesi yüklendi ({key.Item2}): {items.Count} öğe · {clock.ElapsedMilliseconds} ms");
            if (series is null)
            {
                _vodCatalogs[key] = items;
                _ = Task.Run(() => _catalogCache.SaveVod(source, key.Item2, items));
            }
            _vodItems = items;
        }
        catch (Exception exception) when (!request.IsCancellationRequested)
        {
            AppLog.Warn($"İçerik listesi yüklenemedi ({key.Item2})", exception);
            if (request != _vodRequest) { }
            else if (showingCache) SetFooter($"{source.Name} güncellenemedi · kayıtlı liste gösteriliyor ({CatalogLoader.UserMessage(exception)})", isError: true);
            else _vodError = CatalogLoader.UserMessage(exception);
        }
        catch (Exception) when (request.IsCancellationRequested)
        {
        }
        finally
        {
            if (_vodRequest == request)
            {
                _vodRequest = null;
                if (!_closing)
                {
                    ResetVodCategories();
                    UpdateVodRows();
                }
            }
        }
    }

    private void ResetVodCategories()
    {
        var groups = _selectedSeries is null
            ? _vodItems.Select(item => item.Category).Distinct(CategoryComparer).OrderBy(name => name, CategoryComparer)
            : _vodItems.OrderBy(item => item.Season).Select(item => item.Category).Distinct(CategoryComparer);
        var categories = new[] { new VodFilter(null, _selectedSeries is null ? "Tüm kategoriler" : "Tüm sezonlar") }
            .Concat(groups.Select(name => new VodFilter(name, name))).ToArray();
        var previous = (VodCategoryPicker.SelectedItem as VodFilter)?.Category;
        _changingVodFilters = true;
        VodCategoryPicker.ItemsSource = categories;
        VodCategoryPicker.SelectedItem = categories.FirstOrDefault(item => item.Category == previous) ?? categories[0];
        _changingVodFilters = false;
    }

    private void OnVodFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_changingVodFilters) return;
        UpdateVodRows();
        Motion.StaggerRows(VodList);
    }

    private readonly System.Windows.Threading.DispatcherTimer _vodSearchDelay = new() { Interval = TimeSpan.FromMilliseconds(200) };

    private void OnVodSearchChanged(object sender, TextChangedEventArgs e)
    {
        _vodSearchDelay.Stop();
        if (VodSearchBox.Text.Length == 0) UpdateVodRows();
        else _vodSearchDelay.Start();
    }

    private void UpdateVodRows()
    {
        if (!_isReady || _librarySection == LibrarySection.Live || _closing) return;
        var category = (VodCategoryPicker.SelectedItem as VodFilter)?.Category;
        var listKind = _selectedSeries is null ? _vodListKind : VodListKind.All;
        var query = SearchKey(VodSearchBox.Text.Trim());
        var selectedId = (VodList.SelectedItem as VodRow)?.Item.ProviderId;
        var rows = _vodItems.Where(item => (category is null || CategoryComparer.Equals(item.Category, category)) &&
                (listKind != VodListKind.Favorites || IsVodFavorite(item)) && (listKind != VodListKind.Watchlist || IsOnVodWatchlist(item)) &&
                (query.Length == 0 || Matches(item.Title, query) || Matches(item.Category, query) || Matches(item.Detail, query)))
            .Select(item =>
            {
                var resume = item.Playable is not null && _state.Settings.ResumeOnDemand ? _state.WatchPositions.GetValueOrDefault(item.Playable.Id) : 0;
                var completed = item.Playable is not null && _state.CompletedItems.Contains(item.Playable.Id);
                var parts = new[]
                {
                    item.Category, item.Detail, resume > 0 ? $"Devam et · {PlaybackTime.Format(resume)}" : completed ? "İzlendi" : "",
                    _selectedSeries is null && IsOnVodWatchlist(item) ? "İzleme listemde" : ""
                };
                return new VodRow(item, string.Join(" · ", parts.Where(part => part.Length > 0).Distinct()),
                    _librarySection == LibrarySection.Movies ? IconGlyphs.Movie : IconGlyphs.Series,
                    _selectedSeries is null && IsVodFavorite(item), _selectedSeries is null);
            }).ToArray();
        ApplyVodPanelMode();
        VodList.ItemsSource = rows;
        VodList.SelectedItem = rows.FirstOrDefault(row => row.Item.ProviderId == selectedId);
        VodBack.Visibility = _selectedSeries is null ? Visibility.Collapsed : Visibility.Visible;
        VodListTabs.Visibility = _selectedSeries is null ? Visibility.Visible : Visibility.Collapsed;
        UpdateContinueWatching(showRow: _selectedSeries is null && listKind == VodListKind.All && category is null && query.Length == 0);
        VodHeading.Text = _selectedSeries?.Title ?? (_librarySection == LibrarySection.Movies ? "Filmler" : "Diziler");
        VodCount.Text = rows.Length.ToString("N0", Turkish);
        var needsXtream = ActiveSource?.Kind != SourceKind.Xtream;
        var canSwitch = _state.Sources.Any(source => source.Kind == SourceKind.Xtream);
        var loadingVod = rows.Length == 0 && _vodRequest is not null && !needsXtream;
        VodSkeleton.Visibility = loadingVod ? Visibility.Visible : Visibility.Collapsed;
        VodEmpty.Visibility = rows.Length == 0 && !loadingVod ? Visibility.Visible : Visibility.Collapsed;
        VodConnect.Visibility = needsXtream && !canSwitch ? Visibility.Visible : Visibility.Collapsed;
        VodRetry.Visibility = _vodError is null ? Visibility.Collapsed : Visibility.Visible;
        VodMessage.Text = needsXtream ? (canSwitch ? "Aşağıdaki Kaynak kartından bir Xtream kaynağı seç."
                : "Film ve dizileri görmek için bir Xtream kaynağı ekle.")
            : _vodRequest is not null ? "İçerikler yükleniyor…"
            : _vodError ?? (query.Length > 0 ? "Aramana uyan içerik bulunamadı."
                : listKind == VodListKind.Favorites ? "Henüz favorin yok. Bir içeriğin yanındaki şimşeğe bas."
                : listKind == VodListKind.Watchlist ? "İzleme listen boş. Bir içerik seçip “Sonra izle”ye bas."
                : "Bu listede içerik bulunamadı.");
        VodCategoryPicker.IsEnabled = _vodItems.Count > 0;
        UpdateRefreshButton();
        UpdateSelection();
    }

    private async void OnVodSelected(object sender, SelectionChangedEventArgs e)
    {
        CancelVodPreviewRequest();
        _vodPreviewError = null;
        if (VodDetailsButton is not null) VodDetailsButton.IsEnabled = VodList.SelectedItem is VodRow;
        if (!_isReady || _closing) return;
        UpdateSelection();
        if (SelectedVodPreview is not { } preview || _selectedSeries is not null || ActiveSource is not { Kind: SourceKind.Xtream } source ||
            VodList.SelectedItem is not VodRow row ||
            (row.Item.Facts is not null && !string.IsNullOrWhiteSpace(preview.Summary) && !string.IsNullOrWhiteSpace(preview.Poster))) return;
        if (CachedVodDetails(row.Item) is { } cached)
        {
            _vodPreviewDetails = cached;
            UpdateVodPreview();
            return;
        }

        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _vodPreviewRequest = request;
        UpdateVodInfo();
        try
        {
            await Task.Delay(350, request.Token);
            var details = await _loader.LoadVodDetailsAsync(source, row.Item, request.Token);
            if (request.IsCancellationRequested || !ReferenceEquals(VodList.SelectedItem, row) || _activeSourceId != source.Id) return;
            _vodPreviewDetails = details;
            RememberVodDetails(source.Id, details);
            UpdateVodPreview();
        }
        catch (Exception exception) when (request.IsCancellationRequested ||
            exception is SourceException or HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or System.IO.IOException)
        {
            // Keep the catalog preview if the detail request was cancelled or the provider could not supply it.
        }
        finally
        {
            if (_vodPreviewRequest == request)
            {
                _vodPreviewRequest = null;
                if (!_closing) UpdateVodInfo();
            }
        }
    }

    private void OnVodKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || SelectedVodPreview is null) return;
        e.Handled = true;
        VodPreviewPlayButton.Focus();
    }

    private async Task ActivateVodAsync(VodItem item)
    {
        if (item.Playable is Channel channel)
        {
            await PlayChannelAsync(channel);
            return;
        }
        _seriesFilters = (VodSearchBox.Text, (VodCategoryPicker.SelectedItem as VodFilter)?.Category);
        _selectedSeries = item;
        VodSearchBox.Clear();
        await LoadVodViewAsync();
    }

    private async void OnVodBack(object sender, RoutedEventArgs e)
    {
        var filters = _seriesFilters;
        var sourceId = _activeSourceId;
        _seriesFilters = null;
        _selectedSeries = null;
        VodSearchBox.Text = filters?.Search ?? "";
        await LoadVodViewAsync();
        if (_activeSourceId != sourceId || _librarySection != LibrarySection.Series || _selectedSeries is not null) return;
        VodCategoryPicker.SelectedItem = VodCategoryPicker.Items.OfType<VodFilter>()
            .FirstOrDefault(filter => filter.Category == filters?.Category);
    }

    /// <summary>Whether "Listeleri yenile" (Settings › Kaynaklar, F5) can run for the current section now.</summary>
    private bool CanRefreshLists() => _librarySection == LibrarySection.Live
        ? !_refreshing && ActiveSource is not null
        : _vodRequest is null && ActiveSource?.Kind == SourceKind.Xtream;

    private void UpdateRefreshButton() => _sourceSettings?.NotifyChanged();
}
