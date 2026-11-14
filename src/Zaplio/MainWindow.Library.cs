using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zaplio;

public partial class MainWindow
{
    private sealed record VodFilter(string? Category, string Label);

    private sealed record VodRow(VodItem Item, string Caption, string Tile)
    {
        public string Title => Item.Title;
        public string Action => Item.Playable is null ? IconGlyphs.ChevronRight : IconGlyphs.Play;
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

    private Channel? SelectedVod => (VodList.SelectedItem as VodRow)?.Item.Playable;

    private async void OnLibraryChanged(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return;
        _librarySection = sender == MoviesTab ? LibrarySection.Movies : sender == SeriesTab ? LibrarySection.Series : LibrarySection.Live;
        if (_guideOpen) SetGuideOpen(false);
        ApplyLibraryLayout();
        if (_librarySection == LibrarySection.Live)
        {
            CancelVodRequest();
            UpdateRefreshButton();
        }
        else
        {
            _selectedSeries = null;
            _seriesFilters = null;
            VodSearchBox.Clear();
        }
        await LoadActiveSectionAsync();
    }

    private void ApplyLibraryLayout()
    {
        var live = _librarySection == LibrarySection.Live;
        NavigationPane.Visibility = ChannelPane.Visibility = live && !Immersive ? Visibility.Visible : Visibility.Collapsed;
        VodPane.Visibility = !live && !Immersive ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelection();
        UpdatePlaceholder();
    }

    private void CancelVodRequest()
    {
        var request = _vodRequest;
        _vodRequest = null;
        request?.Cancel();
    }

    private async Task LoadVodViewAsync(bool refresh = false)
    {
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
        if (!_changingVodFilters) UpdateVodRows();
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
        var query = SearchKey(VodSearchBox.Text.Trim());
        var selectedId = (VodList.SelectedItem as VodRow)?.Item.ProviderId;
        var rows = _vodItems.Where(item => (category is null || CategoryComparer.Equals(item.Category, category)) &&
                (query.Length == 0 || Matches(item.Title, query) || Matches(item.Category, query) || Matches(item.Detail, query)))
            .Select(item =>
            {
                var resume = item.Playable is not null && _state.Settings.ResumeOnDemand ? _state.WatchPositions.GetValueOrDefault(item.Playable.Id) : 0;
                var completed = item.Playable is not null && _state.CompletedItems.Contains(item.Playable.Id);
                var parts = new[] { item.Category, item.Detail, resume > 0 ? $"Devam et · {VodTime(resume)}" : completed ? "İzlendi" : "" };
                return new VodRow(item, string.Join(" · ", parts.Where(part => part.Length > 0).Distinct()),
                    _librarySection == LibrarySection.Movies ? IconGlyphs.Movie : IconGlyphs.Series);
            }).ToArray();
        VodList.ItemsSource = rows;
        VodList.SelectedItem = rows.FirstOrDefault(row => row.Item.ProviderId == selectedId);
        VodBack.Visibility = _selectedSeries is null ? Visibility.Collapsed : Visibility.Visible;
        VodHeading.Text = _selectedSeries?.Title ?? (_librarySection == LibrarySection.Movies ? "Filmler" : "Diziler");
        VodCount.Text = rows.Length.ToString("N0", Turkish);
        var needsXtream = ActiveSource?.Kind != SourceKind.Xtream;
        var canSwitch = _state.Sources.Any(source => source.Kind == SourceKind.Xtream);
        VodEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        VodConnect.Visibility = needsXtream && !canSwitch ? Visibility.Visible : Visibility.Collapsed;
        VodRetry.Visibility = _vodError is null ? Visibility.Collapsed : Visibility.Visible;
        VodMessage.Text = needsXtream ? (canSwitch ? "Üst menüdeki Aktif Kaynak listesinden bir Xtream kaynağı seç."
                : "Film ve dizileri görmek için bir Xtream kaynağı ekle.")
            : _vodRequest is not null ? "İçerikler yükleniyor…"
            : _vodError ?? (query.Length > 0 ? "Aramana uyan içerik bulunamadı." : "Bu listede içerik bulunamadı.");
        VodCategoryPicker.IsEnabled = _vodItems.Count > 0;
        UpdateRefreshButton();
        UpdateSelection();
    }

    private void OnVodSelected(object sender, SelectionChangedEventArgs e)
    {
        if (VodDetailsButton is not null) VodDetailsButton.IsEnabled = VodList.SelectedItem is VodRow;
        if (_isReady) UpdateSelection();
    }

    private async void OnVodClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject target &&
            ItemsControl.ContainerFromElement(VodList, target) is ListBoxItem { DataContext: VodRow row })
            await ActivateVodAsync(row.Item);
    }

    private async void OnVodKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || VodList.SelectedItem is not VodRow row) return;
        e.Handled = true;
        await ActivateVodAsync(row.Item);
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

    private void UpdateRefreshButton() => RefreshButton.IsEnabled = _librarySection == LibrarySection.Live
        ? !_refreshing && ActiveSource is not null
        : _vodRequest is null && ActiveSource?.Kind == SourceKind.Xtream;
}
