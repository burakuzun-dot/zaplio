using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

public partial class MainWindow : Window
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly StringComparer CategoryComparer = StringComparer.Create(Turkish, ignoreCase: true);
    private readonly CatalogLoader _loader = new();
    private readonly LocalStateStore _store = new();
    private readonly Dictionary<Guid, LoadedCatalog> _catalog = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _playbackLock = new(1);
    private readonly DispatcherTimer _playbackTimeout = new() { Interval = TimeSpan.FromSeconds(25) };
    private LocalState _state = new();
    private Channel? _playingChannel;
    private Channel? _lastChannel;
    private LibVLC? _engine;
    private VlcPlayer? _player;
    private int _playbackRevision;
    private Stopwatch? _playClock;
    private bool _isReady;
    private bool _updatingGroups;
    private bool _volumeDirty;
    private bool _muted;
    private bool _closing;
    private bool _canClose;
    private bool _fullscreen;
    private WindowState _previousWindowState;
    private Rect _previousBounds;
    private WindowChrome? _previousChrome;

    private sealed record GroupEntry(string Key, string Label, string Glyph, int Count);

    public MainWindow()
    {
        InitializeComponent();
        _playbackTimeout.Tick += OnPlaybackTimeout;
        _guideClock.Tick += (_, _) => UpdateGuide();
        _vodClock.Tick += (_, _) => TickVodPlayback();
        InitializeZapping();
        _searchDelay.Tick += (_, _) =>
        {
            _searchDelay.Stop();
            UpdateChannelList();
        };
        _vodSearchDelay.Tick += (_, _) =>
        {
            _vodSearchDelay.Stop();
            UpdateVodRows();
        };
    }

    private Channel? DisplayedChannel => (_playingChannel?.SourceId == _activeSourceId ? _playingChannel : null) ?? (_librarySection == LibrarySection.Live
        ? (ChannelList.SelectedItem as Channel) ?? (_lastChannel is { IsOnDemand: false } ? _lastChannel : null)
        : SelectedVod ?? (_lastChannel is { IsOnDemand: true } ? _lastChannel : null));

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isReady) return;
        FitWindowToWorkArea();
        try
        {
            _state = _store.Load();
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            SetFooter("Kayıtlı ayarlar okunamadı. Kaynaklarını yeniden ekleyebilirsin.", isError: true);
        }

        VolumeSlider.Value = Math.Clamp(_state.Volume, 0, 100);
        LogoLoader.Enabled = _state.Settings.ShowLogos;
        _peakDownload = _state.PeakDownloadBps;
        var preferredSource = _state.Settings.RememberSource ? _state.LastSourceId : null;
        _activeSourceId = _state.Sources.FirstOrDefault(source => source.Id == preferredSource)?.Id
            ?? _state.Sources.FirstOrDefault()?.Id;
        _librarySection = _state.Settings.StartupSection;
        var startupTab = _librarySection switch
        {
            LibrarySection.Movies => MoviesTab,
            LibrarySection.Series => SeriesTab,
            _ => LiveTab
        };
        startupTab.IsChecked = true;
        _isReady = true;
        _guideClock.Start();
        InitializeNetworkStatus();
        InitializeStateScreens();
        UpdateMuteIcon();
        UpdateSourcePicker();
        ApplyLibraryLayout();
        UpdateCatalog();
        _ = RunStartupMaintenanceAsync();
        _ = EnsureEngineAsync(); // Warm up VLC in the background so the first click plays at once.
        await LoadActiveSectionAsync();
    }

    private void FitWindowToWorkArea()
    {
        var area = SystemParameters.WorkArea;
        var width = Math.Min(Width, Math.Max(MinWidth, area.Width - 20));
        var height = Math.Min(Height, Math.Max(MinHeight, area.Height - 20));
        if (width == Width && height == Height) return;
        Width = width;
        Height = height;
        Left = area.Left + Math.Max(0, (area.Width - width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - height) / 2);
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadActiveSectionAsync(refresh: true);

    private void OnSources(object sender, RoutedEventArgs e)
    {
        if (!ConfirmLegalNotice()) return;
        var dialog = new SourcesWindow(_state.Sources, AddSourceAsync, RemoveSourceAsync, _librarySection != LibrarySection.Live) { Owner = this };
        dialog.ShowDialog();
    }

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_state.Settings, _state.Volume) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var previousSettings = _state.Settings;
        var previousVolume = _state.Volume;
        _state.Settings = dialog.Settings;
        _state.Volume = dialog.Volume;
        try { SaveState(); }
        catch (SourceException exception)
        {
            _state.Settings = previousSettings;
            _state.Volume = previousVolume;
            SetFooter(exception.Message, isError: true);
            return;
        }
        VolumeSlider.Value = _state.Volume;
        _volumeDirty = false;
        if (!_state.Settings.ResumeOnDemand) _pendingVodPosition = 0;
        if (previousSettings.MergeQualities != _state.Settings.MergeQualities || previousSettings.QualityMode != _state.Settings.QualityMode)
        {
            _customizationVersion++;
            UpdateCatalog(selectedChannel: RowId((ChannelList.SelectedItem as Channel)?.Id));
        }
        if (LogoLoader.Enabled != _state.Settings.ShowLogos)
        {
            LogoLoader.Enabled = _state.Settings.ShowLogos;
            ChannelList.ItemsSource = null; // re-create rows so logos appear or disappear at once
            UpdateCatalog();
        }
        UpdateVodRows();
        SetFooter("Ayarlar kaydedildi.");
    }

    private void OnLicenses(object sender, RoutedEventArgs e)
    {
        var notices = Path.Combine(AppContext.BaseDirectory, "Licenses", "ThirdPartyNotices.txt");
        try
        {
            Process.Start(new ProcessStartInfo(notices) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            SetFooter("Lisans belgesi açılamadı. Uygulama klasöründeki Licenses bölümüne bak.", isError: true);
        }
    }

    private async Task AddSourceAsync(Source source, CancellationToken cancellationToken)
    {
        var comparison = source.Kind == SourceKind.M3uFile ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (_state.Sources.Any(saved => saved.Id != source.Id && saved.Kind == source.Kind &&
            saved.Location.Equals(source.Location, comparison) && saved.Username == source.Username))
            throw new SourceException("Bu kaynak zaten kayıtlı.");

        var catalog = await _loader.LoadAsync(source, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var index = _state.Sources.FindIndex(saved => saved.Id == source.Id);
        if (index >= 0)
        {
            if (!_catalog.ContainsKey(source.Id) && source.Kind != SourceKind.Xtream &&
                _state.Favorites.Any(id => id.StartsWith($"{source.Id:N}:", StringComparison.Ordinal)))
            {
                try { _catalog[source.Id] = await _loader.LoadAsync(_state.Sources[index], cancellationToken); }
                catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            }
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateSavedSourceAsync(index, source, catalog);
            return;
        }
        _state.Sources.Add(source);
        try
        {
            SaveState();
        }
        catch
        {
            _state.Sources.Remove(source);
            throw;
        }

        _catalog[source.Id] = catalog;
        SaveCatalogCache(source, catalog);
        await ActivateSourceAsync(source.Id);
        SetFooter(catalog.Channels.Count == 0 ? $"{source.Name} eklendi." : $"{source.Name} eklendi · {catalog.Channels.Count} kanal");
    }

    private async Task UpdateSavedSourceAsync(int index, Source source, LoadedCatalog catalog)
    {
        var original = _state.Sources[index];
        var favorites = _state.Favorites.ToArray();
        var history = _state.WatchHistory.ToArray();
        var before = _catalog.GetValueOrDefault(source.Id)?.Channels;
        var candidates = catalog.Channels.ToLookup(channel => (channel.Name, channel.Group));
        Channel? Match(Channel previous)
        {
            if (previous.IsOnDemand) return CatalogLoader.RebindVod(source, previous);
            var matches = candidates[(previous.Name, previous.Group)];
            return matches.FirstOrDefault(channel => channel.Id == previous.Id)
                ?? (matches.Count() == 1 ? matches.First() : null);
        }

        if (before is not null)
        {
            foreach (var previous in before.Where(channel => _state.Favorites.Contains(channel.Id)))
            {
                if (Match(previous) is not { } replacement || replacement.Id == previous.Id) continue;
                _state.Favorites.Remove(previous.Id);
                _state.Favorites.Add(replacement.Id);
            }
        }
        for (var i = 0; i < history.Length; i++)
        {
            if (history[i].Channel.SourceId == source.Id && Match(history[i].Channel) is { } replacement)
                _state.WatchHistory[i] = history[i] with { Channel = replacement };
        }
        _state.Sources[index] = source;
        try { SaveState(); }
        catch
        {
            _state.Sources[index] = original;
            _state.Favorites.Clear();
            _state.Favorites.UnionWith(favorites);
            _state.WatchHistory.Clear();
            _state.WatchHistory.AddRange(history);
            throw;
        }
        _catalog[source.Id] = catalog;
        SaveCatalogCache(source, catalog);
        _customizationVersion++;
        InvalidateGuide(source.Id);
        foreach (var key in _vodCatalogs.Keys.Where(key => key.Source == source.Id).ToArray()) _vodCatalogs.Remove(key);
        if (_activeSourceId == source.Id)
        {
            CancelCatalogRequest();
            CancelVodRequest();
            _lastChannel = null;
            await StopPlaybackAsync("Hazır");
            _lastChannel = null;
            _selectedSeries = null;
            _seriesFilters = null;
            UpdateCatalog();
            await LoadActiveSectionAsync();
        }
        UpdateSourcePicker();
        SetFooter($"{source.Name} güncellendi.");
    }

    private async Task RemoveSourceAsync(Source source)
    {
        var index = _state.Sources.IndexOf(source);
        var favorites = _state.Favorites.Where(id => id.StartsWith($"{source.Id:N}:", StringComparison.Ordinal)).ToArray();
        var positions = _state.WatchPositions.Where(item => item.Key.StartsWith($"{source.Id:N}:", StringComparison.Ordinal)).ToArray();
        var history = _state.WatchHistory.ToArray();
        var previousLastSource = _state.LastSourceId;
        var prefix = $"{source.Id:N}:";
        var hiddenGroups = _state.HiddenGroups.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var channelNames = _state.ChannelNames.Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var favoriteOrder = _state.FavoriteOrder.ToArray();
        _state.Sources.RemoveAt(index);
        if (_state.LastSourceId == source.Id) _state.LastSourceId = _state.Sources.FirstOrDefault()?.Id;
        _state.Favorites.ExceptWith(favorites);
        foreach (var item in positions) _state.WatchPositions.Remove(item.Key);
        _state.WatchHistory.RemoveAll(entry => entry.Channel.SourceId == source.Id);
        _state.HiddenGroups.ExceptWith(hiddenGroups);
        foreach (var item in channelNames) _state.ChannelNames.Remove(item.Key);
        _state.FavoriteOrder.RemoveAll(id => id.StartsWith(prefix, StringComparison.Ordinal));
        try
        {
            SaveState();
        }
        catch
        {
            _state.Sources.Insert(index, source);
            _state.LastSourceId = previousLastSource;
            _state.Favorites.UnionWith(favorites);
            foreach (var item in positions) _state.WatchPositions[item.Key] = item.Value;
            _state.WatchHistory.Clear();
            _state.WatchHistory.AddRange(history);
            _state.HiddenGroups.UnionWith(hiddenGroups);
            foreach (var item in channelNames) _state.ChannelNames[item.Key] = item.Value;
            _state.FavoriteOrder.Clear();
            _state.FavoriteOrder.AddRange(favoriteOrder);
            throw;
        }

        _catalog.Remove(source.Id);
        _catalogCache.Delete(source.Id);
        _customizationVersion++;
        InvalidateGuide(source.Id);
        _liveSourceViews.Remove(source.Id);
        foreach (var key in _vodCatalogs.Keys.Where(key => key.Source == source.Id).ToArray()) _vodCatalogs.Remove(key);
        if (_activeSourceId == source.Id) await ActivateSourceAsync(_state.Sources.FirstOrDefault()?.Id);
        else UpdateSourcePicker();
        SetFooter($"{source.Name} kaldırıldı.");
    }

    private void UpdateCatalog(string? groupKey = null, string? selectedChannel = null)
    {
        var channels = ActiveChannels;
        var selectedGroup = groupKey ?? (GroupList.SelectedItem as GroupEntry)?.Key ?? "all";
        var groups = new List<GroupEntry>
        {
            new("all", "Tüm kanallar", IconGlyphs.Television, channels.Count),
            new("favorites", "Favoriler", IconGlyphs.Star, channels.Count(IsFavorite))
        };
        groups.AddRange(channels.SelectMany(channel => channel.Categories.Distinct(CategoryComparer))
            .Where(category => !IsGroupHidden(category))
            .GroupBy(category => category, CategoryComparer)
            .OrderBy(group => group.Key, CategoryComparer)
            .Select(group => new GroupEntry($"group:{group.Key}", group.Key, IconGlyphs.Folder, group.Count())));

        _updatingGroups = true;
        GroupList.ItemsSource = groups;
        GroupList.SelectedItem = groups.FirstOrDefault(group => group.Key == selectedGroup) ?? groups[0];
        _updatingGroups = false;

        SourceCount.Text = ActiveSource?.Name ?? "Kaynak yok";
        UpdateAccountCard();
        UpdateRefreshButton();
        UpdateChannelList(channels, selectedChannel);
    }

    private void UpdateChannelList(IEnumerable<Channel>? channels = null, string? selectedChannel = null)
    {
        if (!_isReady || GroupList.SelectedItem is not GroupEntry group) return;
        var visible = channels ?? ActiveChannels;
        if (group.Key == "favorites") visible = OrderFavorites(visible.Where(IsFavorite));
        if (group.Key.StartsWith("group:", StringComparison.Ordinal))
            visible = visible.Where(channel => channel.Categories.Contains(group.Label, CategoryComparer));

        var query = SearchKey(SearchBox.Text.Trim());
        if (query.Length > 0)
            visible = visible.Where(channel => Matches(channel.Name, query) || Matches(channel.Group, query));

        var previous = selectedChannel ?? (ChannelList.SelectedItem as Channel)?.Id ?? RowId(_playingChannel?.Id);
        var results = visible.ToList();
        ChannelList.ItemsSource = results;
        ChannelList.SelectedItem = results.FirstOrDefault(channel => channel.Id == previous);
        ChannelHeading.Text = group.Label;
        ChannelCount.Text = results.Count.ToString(Turkish);
        NoChannels.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoChannels.Text = _refreshing ? "Kanallar yükleniyor…" : query.Length > 0 ? "Aramana uyan kanal yok." :
            group.Key == "favorites" ? "Favori kanalın burada görünür." : "Henüz kanal yok.";
        UpdateSelection();
        UpdatePlaceholder();
    }

    private static bool Matches(string value, string query) =>
        SearchKey(value).Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string SearchKey(string value) =>
        string.Concat(value.Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .Replace('ı', 'i');

    private void OnGroupChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingGroups) UpdateChannelList();
    }

    // Filtering 10k+ channels on every key press makes typing stutter; wait until the user pauses.
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(200) };

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_restoringSourceView) return;
        _searchDelay.Stop();
        if (SearchBox.Text.Length == 0) UpdateChannelList(); // clearing is instant
        else _searchDelay.Start();
    }

    private void OnClearSearch(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
    }

    private void OnChannelSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_isReady) UpdateSelection();
    }

    private async void OnChannelClick(object sender, MouseButtonEventArgs e)
    {
        if (_guideOpen || !_state.Settings.AutoPlayChannel) return;
        if (e.OriginalSource is DependencyObject target &&
            ItemsControl.ContainerFromElement(ChannelList, target) is ListBoxItem { DataContext: Channel channel })
            await PlayChannelAsync(channel);
    }

    private async void OnChannelKeyDown(object sender, KeyEventArgs e)
    {
        if (_guideOpen || e.Key != Key.Enter || ChannelList.SelectedItem is not Channel channel) return;
        e.Handled = true;
        await PlayChannelAsync(channel);
    }

    private void UpdateSelection()
    {
        var channel = DisplayedChannel;
        var sectionTitle = _librarySection switch
        {
            LibrarySection.Movies => "Filmler",
            LibrarySection.Series => "Diziler",
            _ => "Canlı TV"
        };
        PlayerTitle.Text = channel?.Name ?? sectionTitle;
        PlayerSubtitle.Visibility = channel is null ? Visibility.Collapsed : Visibility.Visible;
        PlayerSubtitle.Text = channel is null ? "" : string.Join(" · ",
            new[] { channel.GroupLabel, _state.Sources.FirstOrDefault(source => source.Id == channel.SourceId)?.Name }
                .Where(part => !string.IsNullOrEmpty(part)));

        FavoriteButton.IsEnabled = PlayButton.IsEnabled = channel is not null;
        GuideToggle.Visibility = _librarySection == LibrarySection.Live ? Visibility.Visible : Visibility.Collapsed;
        FavoriteButton.Visibility = _librarySection == LibrarySection.Live && channel?.IsOnDemand != true
            ? Visibility.Visible : Visibility.Collapsed;
        var favoriteChannel = _guideOpen ? GuideChannel : channel;
        FavoriteButton.IsEnabled = favoriteChannel is not null;
        var favorite = favoriteChannel is not null && IsFavorite(favoriteChannel);
        FavoriteIcon.Foreground = (Brush)FindResource(favorite ? "Accent" : "Muted");
        FavoriteButton.Background = (Brush)FindResource(favorite ? "Active" : "Canvas");
        FavoriteButton.BorderBrush = (Brush)FindResource(favorite ? "ActiveLine" : "Canvas");
        var favoriteLabel = favorite ? "Favorilerden çıkar" : "Favorilere ekle";
        FavoriteButton.ToolTip = favoriteLabel;
        System.Windows.Automation.AutomationProperties.SetName(FavoriteButton, favoriteLabel);
        var paused = CanPausePlayback && _player?.State == VLCState.Paused;
        PlayIcon.Text = _playingChannel is null || paused ? IconGlyphs.Play : CanPausePlayback ? IconGlyphs.Pause : IconGlyphs.Stop;
        var playLabel = _playingChannel is null ? "Oynat" : paused ? "Devam et" : CanPausePlayback ? "Duraklat" : "Durdur";
        PlayText.Text = playLabel;
        PlayButton.ToolTip = playLabel;
        System.Windows.Automation.AutomationProperties.SetName(PlayButton, playLabel);
        UpdateGuide();
        UpdateVodPlaybackVisibility();
        UpdateExtraControls();
        UpdateVodPreview();
    }

    private void UpdatePlaceholder()
    {
        EmptyLibrary.Visibility = _state.Sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (IsSecondScreenActive && _playingChannel is not null)
        {
            FirstSourceButton.Visibility = Visibility.Collapsed;
            PlaceholderTitle.Text = "İkinci ekranda oynatılıyor";
            PlaceholderDescription.Text = "Kanal listesini ve rehberi buradan kullanabilirsin. Esc ya da \"Geri al\" ile videoyu bu pencereye taşı.";
            return;
        }
        if (_librarySection != LibrarySection.Live)
        {
            var hasXtream = _state.Sources.Any(source => source.Kind == SourceKind.Xtream);
            FirstSourceButton.Visibility = hasXtream ? Visibility.Collapsed : Visibility.Visible;
            PlaceholderTitle.Text = ActiveSource?.Kind != SourceKind.Xtream
                ? (hasXtream ? "Xtream kaynağı seç" : "Xtream kaynağı ekle") :
                _librarySection == LibrarySection.Movies ? "Bir film seç" : "Bir bölüm seç";
            PlaceholderDescription.Text = ActiveSource?.Kind == SourceKind.Xtream ? "Listeden bir içerik seçerek oynat."
                : hasXtream ? "Üst menüden bir Xtream kaynağı seç." : "Film ve diziler Xtream hesabından yüklenir.";
            return;
        }
        var noSources = _state.Sources.Count == 0;
        FirstSourceButton.Visibility = noSources ? Visibility.Visible : Visibility.Collapsed;
        if (_refreshing && ActiveChannels.Count == 0)
        {
            PlaceholderTitle.Text = "Kanallar yükleniyor";
            PlaceholderDescription.Text = "Yayın kaynaklarına bağlanılıyor…";
        }
        else if (noSources)
        {
            PlaceholderTitle.Text = "İlk kaynağını ekle";
            PlaceholderDescription.Text = "M3U listeni veya Xtream hesabını bağla.";
        }
        else if (ActiveChannels.Count == 0)
        {
            PlaceholderTitle.Text = "Canlı kanal bulunamadı";
            PlaceholderDescription.Text = "Aktif kaynağı kontrol et veya üst menüden başka bir kaynak seç.";
        }
        else
        {
            PlaceholderTitle.Text = "Bir kanal seç";
            PlaceholderDescription.Text = "Kanal listesinden canlı yayını aç.";
        }
    }

    private void UpdatePlaceholderVisibility()
    {
        PlayerPlaceholder.Visibility = VodPreviewPanel.Visibility == Visibility.Visible ||
            (!IsSecondScreenActive && VideoView.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;
        UpdateAmbientLightVisibility();
    }

    private void OnFavorite(object sender, RoutedEventArgs e)
    {
        if ((_guideOpen ? GuideChannel : DisplayedChannel) is not { IsOnDemand: false } channel ||
            channel.SourceId != _activeSourceId) return;
        var undo = ToggleFavorite(channel);
        try
        {
            SaveState();
            UpdateCatalog();
        }
        catch (SourceException exception)
        {
            undo();
            SetFooter(exception.Message, isError: true);
        }
    }

    private async void OnPlayToggle(object sender, RoutedEventArgs e)
    {
        if (CanPausePlayback && _player is { } player) player.SetPause(player.State != VLCState.Paused);
        else if (_playingChannel is not null) await StopPlaybackAsync("Durduruldu");
        else if (DisplayedChannel is Channel channel) await PlayChannelAsync(channel);
    }

    private async Task PlayChannelAsync(Channel channel, bool reconnect = false)
    {
        if (_closing || channel.SourceId != _activeSourceId || (!reconnect && _playingChannel?.Id == channel.Id)) return;
        if (_sleepingChannel is { } sleeping && sleeping.Id != channel.Id) HideSleeping();
        var showStatic = TelevisionEffect.IsChannelChange(_playingChannel, channel, reconnect);
        _vodPreviewError = null;
        UpdateVodPreview();
        if (!reconnect)
        {
            RememberPreviousChannel(channel);
            _reconnectAttempts = 0;
            _streamWasPlaying = false;
            _failoverTried.Clear();
        }
        ResetStreamHealth(keepHistory: reconnect);
        ResetLiveDelay();
        if (!reconnect) _liveReturnChannel = null;
        var revision = ++_playbackRevision;
        _playClock = Stopwatch.StartNew();
        await _playbackLock.WaitAsync();
        try
        {
            if (revision != _playbackRevision || _closing) return;
            if (showStatic && SystemParameters.ClientAreaAnimation)
            {
                if (!IsSecondScreenActive) ChannelStatic.ShowStatic();
                VideoStatic.ShowStatic();
            }
            var lockWait = _playClock.ElapsedMilliseconds;
            await ReleasePlayerAsync();
            if (revision != _playbackRevision || channel.SourceId != _activeSourceId) return;

            SetPlaybackStatus("Oynatıcı hazırlanıyor…");
            _engine ??= await EnsureEngineAsync();
            if (revision != _playbackRevision || _closing) return;
            var engineReady = _playClock.ElapsedMilliseconds;

            var engine = _engine;
            var player = await Task.Run(() => new VlcPlayer(engine));
            if (revision != _playbackRevision || _closing)
            {
                await Task.Run(player.Dispose);
                return;
            }
            _player = player;
            _finishedEpisode = null;
            _lastChannel = _playingChannel = channel;
            PrepareVodPlayback(channel);
            player.Playing += (_, _) => Dispatcher.BeginInvoke(() => PlaybackStarted(player, revision));
            player.EndReached += (_, _) => Dispatcher.BeginInvoke(() => PlaybackEnded(player, revision, false));
            player.EncounteredError += (_, _) => Dispatcher.BeginInvoke(() => PlaybackEnded(player, revision, true));
            player.Buffering += (_, args) => Dispatcher.BeginInvoke(() => OnPlayerBuffering(player, revision, args.Cache));
            if (channel.IsOnDemand)
            {
                player.Paused += (_, _) => Dispatcher.BeginInvoke(() => OnVodPaused(player, revision));
                player.PausableChanged += (_, _) => Dispatcher.BeginInvoke(() =>
                {
                    if (player == _player && revision == _playbackRevision && !_closing) UpdateSelection();
                });
            }
            else
            {
                player.Paused += (_, _) => Dispatcher.BeginInvoke(() => OnLivePaused(player, revision));
                player.PausableChanged += (_, _) => Dispatcher.BeginInvoke(() =>
                {
                    if (player == _player && revision == _playbackRevision && !_closing) UpdateSelection();
                });
            }

            VideoView.Visibility = Visibility.Visible;
            UpdatePlaceholderVisibility();
            VideoView.MediaPlayer = player;
            UpdateSelection();
            SetPlaybackStatus("Bağlanıyor…");

            _playbackTimeout.Start();
            var started = await Task.Run(() =>
            {
                using var media = new Media(engine, channel.Stream);
                if (!string.IsNullOrEmpty(channel.UserAgent)) media.AddOption($":http-user-agent={channel.UserAgent}");
                if (!string.IsNullOrEmpty(channel.Referrer)) media.AddOption($":http-referrer={channel.Referrer}");
                return player.Play(media);
            });
            if (!started) throw new InvalidOperationException("LibVLC Play() returned false.");
            AppLog.Info($"Oynatma istendi: {channel.Name} · kilit {lockWait} ms · motor {engineReady} ms · Play() {_playClock.ElapsedMilliseconds} ms");
        }
        catch (Exception exception)
        {
            AppLog.Error($"Oynatma başlatılamadı: {channel.Name} ({channel.Stream})", exception);
            await ReleasePlayerAsync();
            if (revision == _playbackRevision) ShowPlaybackStopped("Yayın açılamadı", isError: true);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private void PlaybackStarted(VlcPlayer player, int revision)
    {
        if (player != _player || revision != _playbackRevision || _closing) return;
        _playbackTimeout.Stop();
        HideSleeping();
        _playStartedAt = Environment.TickCount64;
        _streamWasPlaying = true;
        AppLog.Info($"Yayın başladı: {_playingChannel?.Name} · {_playClock?.ElapsedMilliseconds} ms");
        ApplyVolume();
        ApplyVideoOptions(player);
        if (_playingChannel is { } channel) RecordWatch(channel);
        if (_playingChannel?.IsOnDemand == true)
        {
            _vodStarted = true;
            _vodClock.Start();
            TickVodPlayback();
            UpdateSelection();
            SetPlaybackStatus("Oynatılıyor", live: true);
        }
        else
        {
            OnLiveResumed();
            if (IsLiveDelayed) UpdateLiveStatus();
            else SetPlaybackStatus("Canlı", live: true);
            UpdateSelection();
        }
    }

    private async void PlaybackEnded(VlcPlayer player, int revision, bool error)
    {
        if (player != _player || revision != _playbackRevision || _closing) return;
        var vod = _playingChannel?.IsOnDemand == true;
        if (_playingChannel is { IsOnDemand: false } live &&
            await TryReconnectAsync(live, revision, error ? "oynatıcı hatası" : "yayın sona erdi")) return;
        if (_playingChannel is { IsOnDemand: false } failed && await TryFailoverAsync(failed)) return;
        if (error) AppLog.Warn($"Yayın hatayla sona erdi: {_playingChannel?.Name} ({_playingChannel?.Stream})");
        if (!error && _playingChannel?.SeriesId > 0) _finishedEpisode = _playingChannel;
        var message = error ? (vod ? "İçerik açılamadı" : "Yayın açılamadı") : (vod ? "İçerik tamamlandı" : "Yayın sona erdi");
        await StopPlaybackAsync(message, error, completed: vod && !error);
    }

    private async void OnPlaybackTimeout(object? sender, EventArgs e)
    {
        _playbackTimeout.Stop();
        if (_playingChannel is { IsOnDemand: false } live && await TryReconnectAsync(live, _playbackRevision, "zaman aşımı")) return;
        if (_playingChannel is { IsOnDemand: false } failed && await TryFailoverAsync(failed)) return;
        AppLog.Warn($"Yayın zaman aşımına uğradı: {_playingChannel?.Name} ({_playingChannel?.Stream})");
        await StopPlaybackAsync("Yayın zaman aşımına uğradı", isError: true);
    }

    private async Task StopPlaybackAsync(string status, bool isError = false, bool completed = false)
    {
        var revision = ++_playbackRevision;
        await _playbackLock.WaitAsync();
        try
        {
            if (revision != _playbackRevision) return;
            await ReleasePlayerAsync(completed);
            if (revision == _playbackRevision && !_closing) ShowPlaybackStopped(status, isError);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private async Task ReleasePlayerAsync(bool completed = false)
    {
        _playbackTimeout.Stop();
        _vodClock.Stop();
        if (_player is not VlcPlayer player) return;
        SaveVodPosition(player, completed);
        _player = null;
        _recordedPlayback = null;
        _playingChannel = null;
        VideoView.Visibility = Visibility.Hidden;
        UpdatePlaceholderVisibility();
        try
        {
            await Task.Run(player.Stop);
        }
        finally
        {
            VideoView.MediaPlayer = null;
            player.Dispose();
        }
    }

    private void ShowPlaybackStopped(string status, bool isError)
    {
        if (_fullscreen) ToggleFullscreen();
        if (_pip) TogglePip();
        HideSecondScreen();
        ChannelOsd.Visibility = Visibility.Collapsed;
        UpdateSelection();
        UpdatePlaceholder();
        if (_librarySection != LibrarySection.Live) UpdateVodRows();
        if (isError)
        {
            if (SelectedVodPreview is not null)
            {
                _vodPreviewError = status;
                UpdateVodPreview();
            }
            else
            {
                PlaceholderTitle.Text = status;
                PlaceholderDescription.Text = "Tekrar oynatmayı dene veya başka bir içerik seç.";
            }
        }
        SetPlaybackStatus(status, isError: isError);
        if (isError && _lastChannel is { IsOnDemand: false } failed && _librarySection == LibrarySection.Live) ShowSleeping(failed);
        else HideSleeping();
    }

    private void OnMute(object sender, RoutedEventArgs e)
    {
        if (VolumeSlider.Value == 0)
        {
            _muted = false;
            VolumeSlider.Value = 50;
        }
        else
        {
            _muted = !_muted;
            ApplyVolume();
            UpdateMuteIcon();
        }
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isReady) return;
        _state.Volume = (int)Math.Round(e.NewValue);
        _volumeDirty = true;
        _muted = false;
        ApplyVolume();
        UpdateMuteIcon();
    }

    private void ApplyVolume()
    {
        if (_player is not null) _player.Volume = _muted ? 0 : _state.Volume;
    }

    private void UpdateMuteIcon() => MuteIcon.Text = _muted || VolumeSlider.Value == 0 ? IconGlyphs.Mute : IconGlyphs.Volume;

    private void SetPlaybackStatus(string message, bool live = false, bool isError = false)
    {
        var brush = (Brush)FindResource(isError ? "Error" : live ? "LiveColor" : "Muted");
        PlaybackLight.Fill = PlaybackStatus.Foreground = brush;
        PlaybackStatus.Text = message;
    }

    private void SetFooter(string message, bool isError = false)
    {
        FooterStatus.Text = message;
        FooterStatus.ToolTip = message;
        FooterStatus.Foreground = (Brush)FindResource(isError ? "Error" : "Muted");
    }

    private void SaveState()
    {
        try
        {
            _store.Save(_state);
            _volumeDirty = false;
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            throw new SourceException("Ayarlar bu bilgisayara kaydedilemedi.");
        }
    }

    private static bool IsStorageError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or CryptographicException or JsonException;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key == Key.F11 && Keyboard.Modifiers == ModifierKeys.None) || (_fullscreen && e.Key == Key.Escape))
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (!OwnedWindows.OfType<ShortcutsWindow>().Any()) new ShortcutsWindow { Owner = this }.ShowDialog();
            e.Handled = true;
        }
        else if (IsSecondScreenActive && e.Key == Key.Escape)
        {
            ReturnVideoToMainWindow();
            e.Handled = true;
        }
        else if (e.Key == Key.F11 && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            ToggleSecondScreen();
            e.Handled = true;
        }
        else if (_pip && e.Key == Key.Escape)
        {
            TogglePip();
            e.Handled = true;
        }
        else if (HandleZappingKey(e) || HandlePlayerKey(e))
        {
            e.Handled = true;
        }
        else if (e.Key is Key.Space or Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.None && _player is { } player &&
            _playingChannel is { } playing &&
            Keyboard.FocusedElement is not (System.Windows.Controls.Primitives.TextBoxBase or PasswordBox or ComboBox or Slider))
        {
            if (e.Key == Key.Space && CanPausePlayback && Keyboard.FocusedElement is not (Button or RadioButton))
            {
                player.SetPause(player.State != VLCState.Paused);
                e.Handled = true;
            }
            else if (!playing.IsOnDemand && e.Key == Key.Left)
            {
                _ = LiveRewindAsync();
                e.Handled = true;
            }
            else if (e.Key == Key.Right && CanReturnToLive && (!playing.IsOnDemand || !player.IsSeekable))
            {
                _ = ReturnToLiveAsync();
                e.Handled = true;
            }
            else if (playing.IsOnDemand && e.Key is Key.Left or Key.Right && player.IsSeekable && player.Length > 0)
            {
                _pendingVodPosition = 0;
                MarkSeek();
                player.Time = Math.Clamp(player.Time + (e.Key == Key.Left ? -10_000 : 10_000), 0, player.Length);
                e.Handled = true;
            }
        }
        else if (!_fullscreen && e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var search = _librarySection == LibrarySection.Live ? SearchBox : VodSearchBox;
            search.Focus();
            search.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin && SearchBox.Text.Length > 0)
        {
            SearchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && VodSearchBox.IsKeyboardFocusWithin && VodSearchBox.Text.Length > 0)
        {
            VodSearchBox.Clear();
            e.Handled = true;
        }
    }

    private void OnFullscreen(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (!_fullscreen && (_player is null || IsSecondScreenActive)) return;
        if (!_fullscreen && _pip) TogglePip();
        if (!_fullscreen && _guideOpen) SetGuideOpen(false);
        _fullscreen = !_fullscreen;
        ApplyImmersiveLayout();

        if (_fullscreen)
        {
            var handle = new WindowInteropHelper(this).Handle;
            var monitor = MonitorFromWindow(handle, 2);
            var info = new MonitorInformation { Size = Marshal.SizeOf<MonitorInformation>() };
            _previousWindowState = WindowState;
            _previousBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
            _previousChrome = WindowChrome.GetWindowChrome(this);
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;
            WindowChrome.SetWindowChrome(this, null);
            if (GetMonitorInfo(monitor, ref info))
            {
                var area = info.Monitor;
                SetWindowPos(handle, IntPtr.Zero, area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top, 0x14);
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            Focus();
        }
        else
        {
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;
            WindowChrome.SetWindowChrome(this, _previousChrome);
            if (!_previousBounds.IsEmpty)
            {
                Left = _previousBounds.Left;
                Top = _previousBounds.Top;
                Width = _previousBounds.Width;
                Height = _previousBounds.Height;
            }
            WindowState = _previousWindowState;
        }
    }

    private void OnTitleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) OnMaximize(sender, e);
        else DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        ReleaseStateScreens();
        _playbackTimeout.Stop();
        _guideClock.Stop();
        _networkClock.Stop();
        CancelCatalogRequest();
        CancelVodRequest();
        _lifetime.Cancel();
        if (SystemParameters.ClientAreaAnimation && (WindowState != WindowState.Minimized || IsSecondScreenActive))
        {
            if (WindowState != WindowState.Minimized) WindowPowerOff.ShowPowerOff();
            if (VideoOverlay.IsVisible) VideoPowerOff.ShowPowerOff();
            await Task.Delay(TelevisionEffect.PowerOffDuration);
        }
        if (_peakDownload > _state.PeakDownloadBps)
        {
            _state.PeakDownloadBps = _peakDownload;
            _volumeDirty = true; // reuse the "save on close" path
        }
        if (_volumeDirty)
        {
            try { SaveState(); }
            catch (SourceException exception) { SetFooter(exception.Message, isError: true); }
        }

        await StopPlaybackAsync("Durduruldu");
        _secondScreen?.CloseForShutdown();
        (_engine ?? (_engineTask is { IsCompletedSuccessfully: true } warmed ? warmed.Result : null))?.Dispose();
        _loader.Dispose();
        _guideLoader.Dispose();
        _canClose = true;
        // WPF disallows a second Close call during the original Closing event.
        _ = Dispatcher.BeginInvoke(Close);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInformation
    {
        public int Size;
        public NativeRectangle Monitor, Work;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInformation info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
