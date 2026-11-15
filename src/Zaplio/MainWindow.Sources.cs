using System.Windows.Controls;

namespace Zaplio;

public partial class MainWindow
{
    private sealed record SourceChoice(Guid? Id, string Name);
    private sealed record LiveSourceView(string Group, string Search, string? ChannelId);

    private readonly Dictionary<Guid, LiveSourceView> _liveSourceViews = new();
    private Guid? _activeSourceId;
    private CancellationTokenSource? _catalogRequest;
    private bool _syncSourcePicker;
    private bool _restoringSourceView;

    private Source? ActiveSource => _state.Sources.FirstOrDefault(source => source.Id == _activeSourceId);
    private readonly CatalogCache _catalogCache = new();

    private IReadOnlyList<Channel> ActiveChannels => _activeSourceId is Guid id ? CustomizedChannels(id) : [];

    private void SaveCatalogCache(Source source, LoadedCatalog catalog)
    {
        if (source.Kind == SourceKind.M3uFile) return;
        _ = Task.Run(() => _catalogCache.Save(source, catalog));
    }

    private static string CacheAgeLabel(TimeSpan age) => age.TotalMinutes < 1 ? "az önce"
        : age.TotalHours < 1 ? $"{(int)age.TotalMinutes} dk önce"
        : age.TotalDays < 1 ? $"{(int)age.TotalHours} sa önce"
        : $"{(int)age.TotalDays} gün önce";
    private bool _refreshing => _catalogRequest is not null;

    private void UpdateSourcePicker()
    {
        var choices = _state.Sources.Select(source => new SourceChoice(source.Id, source.Name)).ToArray();
        if (choices.Length == 0) choices = [new SourceChoice(null, "Kaynak yok")];
        _syncSourcePicker = true;
        ActiveSourcePicker.ItemsSource = choices;
        ActiveSourcePicker.SelectedItem = choices.FirstOrDefault(choice => choice.Id == _activeSourceId);
        ActiveSourcePicker.IsEnabled = _state.Sources.Count > 0;
        _syncSourcePicker = false;
    }

    private async void OnActiveSourceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady || _syncSourcePicker || ActiveSourcePicker.SelectedItem is not SourceChoice choice) return;
        await ActivateSourceAsync(choice.Id);
    }

    private async Task ActivateSourceAsync(Guid? id)
    {
        if (id == _activeSourceId) return;
        if (_activeSourceId is Guid oldId && _state.Sources.Any(source => source.Id == oldId))
            _liveSourceViews[oldId] = new LiveSourceView((GroupList.SelectedItem as GroupEntry)?.Key ?? "all",
                SearchBox.Text, (ChannelList.SelectedItem as Channel)?.Id);

        CancelCatalogRequest();
        CancelVodRequest();
        CancelVodPreviewRequest();
        _activeSourceId = id;
        _lastChannel = null;
        _finishedEpisode = null;
        var stop = StopPlaybackAsync("Hazır");
        SetFooter(ActiveSource?.Name ?? "");

        if (_state.LastSourceId != id)
        {
            var previous = _state.LastSourceId;
            _state.LastSourceId = id;
            try { SaveState(); }
            catch (SourceException exception)
            {
                _state.LastSourceId = previous;
                SetFooter(exception.Message, isError: true);
            }
        }

        var view = id is Guid sourceId ? _liveSourceViews.GetValueOrDefault(sourceId) : null;
        _restoringSourceView = true;
        ChannelList.SelectedItem = null;
        SearchBox.Text = view?.Search ?? "";
        _restoringSourceView = false;
        _selectedSeries = null;
        _seriesFilters = null;
        _vodItems = [];
        _vodError = null;
        VodSearchBox.Clear();
        ResetVodCategories();
        UpdateSourcePicker();
        UpdateCatalog(view?.Group ?? "all", view?.ChannelId);
        UpdateVodRows();

        await stop;
        if (_activeSourceId == id && !_closing) await LoadActiveSectionAsync();
    }

    private Task LoadActiveSectionAsync(bool refresh = false) => _librarySection == LibrarySection.Live
        ? LoadActiveCatalogAsync(refresh)
        : LoadVodViewAsync(refresh);

    private void CancelCatalogRequest()
    {
        var request = _catalogRequest;
        _catalogRequest = null;
        request?.Cancel();
    }

    private async Task LoadActiveCatalogAsync(bool refresh)
    {
        if (_catalogRequest is not null || ActiveSource is not Source source) return;
        if (!refresh && _catalog.ContainsKey(source.Id))
        {
            SetFooter($"{source.Name} · {ActiveChannels.Count} kanal");
            return;
        }

        if (!refresh && source.Kind != SourceKind.M3uFile)
        {
            var cached = await Task.Run(() => _catalogCache.Load(source));
            if (_closing || _catalogRequest is not null || ActiveSource?.Id != source.Id || _catalog.ContainsKey(source.Id)) return;
            if (cached is not null)
            {
                _catalog[source.Id] = cached.Value;
                InvalidateGuide(source.Id);
                var view = _liveSourceViews.GetValueOrDefault(source.Id);
                UpdateCatalog(view?.Group, view?.ChannelId);
                var age = DateTimeOffset.UtcNow - cached.SavedAt;
                SetFooter($"{source.Name} · {ActiveChannels.Count} kanal · {CacheAgeLabel(age)} kaydedilen liste");
                if (cached.IsFresh) return;
            }
        }

        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _catalogRequest = request;
        UpdateCatalog();
        SetFooter($"{source.Name} yükleniyor…");
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var catalog = await _loader.LoadAsync(source, request.Token);
            AppLog.Info($"Kanal listesi sunucudan yüklendi: {catalog.Channels.Count} kanal · {clock.ElapsedMilliseconds} ms");
            if (request != _catalogRequest) return;
            _catalog[source.Id] = catalog;
            InvalidateGuide(source.Id);
            SaveCatalogCache(source, catalog);
            SetFooter($"{source.Name} · {ActiveChannels.Count} kanal");
        }
        catch (Exception exception) when (!request.IsCancellationRequested)
        {
            AppLog.Warn($"Kaynak yüklenemedi: {source.Name}", exception);
            if (request != _catalogRequest) return;
            var message = CatalogLoader.UserMessage(exception);
            SetFooter(_catalog.ContainsKey(source.Id)
                ? $"{source.Name} güncellenemedi · kayıtlı liste gösteriliyor ({message})"
                : $"{source.Name} · {message}", isError: true);
        }
        catch (Exception) when (request.IsCancellationRequested)
        {
        }
        finally
        {
            if (_catalogRequest == request)
            {
                _catalogRequest = null;
                if (!_closing) UpdateCatalog();
            }
        }
    }
}
