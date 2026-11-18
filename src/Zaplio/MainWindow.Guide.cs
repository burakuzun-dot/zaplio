using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Zaplio;

public partial class MainWindow
{
    private sealed class GuideRequest(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public bool IsLoading { get; set; } = true;
        public XmlTvGuide? Guide { get; set; }
        public string? Error { get; set; }
    }

    private sealed record GuideRow(DateTimeOffset Start, string Time, string Title, string Description, bool IsOnAir,
        DateTimeOffset? End = null, bool CanCatchUp = false)
    {
        public bool ShowArchive => CanCatchUp && !IsOnAir;
        public string StartTime => Start.LocalDateTime.ToString("HH:mm", Lang.Culture);
        public override string ToString() => $"{Time} {Title}";
    }

    private readonly XmlTvLoader _guideLoader = new();
    private readonly HashSet<Guid> _guideRefresh = new();
    private readonly Dictionary<Guid, GuideRequest> _guideRequests = new();
    private readonly DispatcherTimer _guideClock = new() { Interval = TimeSpan.FromSeconds(30) };
    private bool _guideOpen;
    private DateOnly _guideDay;
    private string? _guideListChannel;
    private DateOnly _guideListDay;

    private Channel? GuideChannel => ChannelList.SelectedItem as Channel ??
        ChannelList.Items.OfType<Channel>().FirstOrDefault() ?? (_lastChannel is { IsOnDemand: false } ? _lastChannel : null);

    private void OnToggleGuide(object sender, RoutedEventArgs e) => SetGuideOpen(!_guideOpen);

    private void SetGuideOpen(bool open)
    {
        _guideOpen = open;
        if (open) _guideDay = DateOnly.FromDateTime(DateTime.Today);
        GuideToggleText.Text = open ? Lang.T("Rehberi gizle") : Lang.T("Rehber");
        GuideToggle.ToolTip = open ? Lang.T("Yayın rehberini gizle") : Lang.T("Yayın rehberini göster");
        GuidePane.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open) _guideListChannel = null;
        UpdateSelection();
    }

    private void OnPlayerPaneSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (GuidePane is not null) GuidePane.Height = Math.Clamp(e.NewSize.Height * 0.38, 144, 315);
        if (e.WidthChanged)
        {
            ApplyCompactPlayerChrome(e.NewSize.Width);
            UpdateVodPlayerHeight();
        }
    }

    /// <summary>
    /// In a narrow player column the guide buttons keep only their icons (so the title is not cut short) and the volume
    /// slider and, in the narrowest column, the mini-window button step aside (so the full-screen button still fits); the mute
    /// button, the volume keys and P still work.
    /// </summary>
    private void ApplyCompactPlayerChrome(double width)
    {
        var labels = width < 560 ? Visibility.Collapsed : Visibility.Visible;
        GuideGridText.Visibility = labels;
        GuideToggleText.Visibility = labels;
        GuideGridButton.Padding = GuideToggle.Padding = new Thickness(labels == Visibility.Visible ? 12 : 10, 0, labels == Visibility.Visible ? 12 : 4, 0);
        VolumeSlider.Visibility = width < 470 ? Visibility.Collapsed : Visibility.Visible;
        PipButton.Visibility = width < 420 ? Visibility.Collapsed : Visibility.Visible; // P still opens the mini window
        RecordButton.Visibility = width < 420 && !IsRecording ? Visibility.Collapsed : Visibility.Visible; // R still records
    }

    private void OnGuideProgrammeChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGuidePlayButton();
        var row = GuideList.SelectedItem as GuideRow;
        GuideDetails.DataContext = row;
        GuideDescription.Text = string.IsNullOrWhiteSpace(row?.Description)
            ? Lang.T("Bu program için açıklama yok.") : row.Description;
    }

    private void UpdateGuide()
    {
        if (!_isReady || _closing) return;
        var channel = _guideOpen ? GuideChannel : PlayingLiveChannel;
        var source = _state.Sources.FirstOrDefault(item => item.Id == channel?.SourceId);
        IReadOnlyList<Uri> locations = source is null ? [] : GuideAddresses(source);
        var request = source is null ? null : GetGuide(source.Id, locations);

        var now = DateTimeOffset.Now;
        UpdateGuideSummary(now);
        if (!_guideOpen) return;

        var today = DateOnly.FromDateTime(now.LocalDateTime);
        // Past days are reachable as far back as the channel's archive goes (Xtream catch-up), at most a week.
        var archiveDays = source?.Kind == SourceKind.Xtream ? Math.Clamp(channel?.CatchupDays ?? 0, 0, 7) : 0;
        if (_guideDay < today.AddDays(-archiveDays) || _guideDay > today.AddDays(6)) _guideDay = today;
        var programmes = channel is not null ? request?.Guide?.ForChannel(channel) ?? [] : [];
        var current = programmes.LastOrDefault(programme => programme.IsOnAir(now));
        var dayStart = _guideDay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var dayEnd = _guideDay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local);
        var rows = programmes.Where(programme => programme.Start < dayEnd &&
                (programme.Start >= dayStart || programme.End > dayStart))
            .Select(programme => new GuideRow(programme.Start, ProgrammeTime(programme), programme.Title, programme.Description,
                programme == current, programme.End, CanCatchUp(source, channel, programme.Start, now)))
            .ToArray();
        var sameSchedule = _guideListChannel == channel?.Id && _guideListDay == _guideDay;
        if (!sameSchedule || GuideList.ItemsSource is not GuideRow[] previous || !previous.SequenceEqual(rows))
        {
            var selected = sameSchedule ? GuideList.SelectedItem as GuideRow : null;
            GuideList.ItemsSource = rows;
            GuideList.SelectedItem = rows.FirstOrDefault(row => row.Start == selected?.Start && row.Title == selected.Title)
                ?? rows.FirstOrDefault(row => row.IsOnAir)
                ?? rows.FirstOrDefault(row => row.Start >= now)
                ?? rows.FirstOrDefault();
            if (GuideList.SelectedItem is { } item) GuideList.ScrollIntoView(item);
        }
        _guideListChannel = channel?.Id;
        _guideListDay = _guideDay;
        GuideDetails.Visibility = rows.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        GuideDayLabel.Text = _guideDay == today
            ? Lang.F($"Bugün, {_guideDay.ToString("d MMM", Lang.Culture)}") : _guideDay.ToString("d MMM ddd", Lang.Culture);
        GuidePrevious.IsEnabled = programmes.Count > 0 && _guideDay > today.AddDays(-archiveDays);
        GuideNext.IsEnabled = programmes.Count > 0 && _guideDay < today.AddDays(6);
        GuideSource.IsEnabled = source is not null;
        GuideChannelName.Text = channel?.Name ?? "";
        GuideChannelName.ToolTip = channel?.Name;
        GuideReload.IsEnabled = locations.Count > 0 && request?.IsLoading != true;
        UpdateGuidePlayButton();
        GuideEmpty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        GuideConnect.Visibility = source is not null && request?.IsLoading != true &&
            (locations.Count == 0 || request?.Error is not null || programmes.Count == 0)
            ? Visibility.Visible : Visibility.Collapsed;
        GuideConnect.Content = locations.Count == 0 ? Lang.T("XMLTV kaynağı ekle") : Lang.T("XMLTV kaynağını ayarla");
        GuideMessage.Text = channel is null ? Lang.T("Rehberini görmek için bir kanal seç.")
            : request?.IsLoading == true ? Lang.T("Yayın rehberi yükleniyor…")
            : request?.Error ?? (locations.Count == 0 ? Lang.T("Bu listeye bir XMLTV rehberi bağlayabilirsin.")
                : programmes.Count == 0 ? Lang.T("Rehberde bu kanalla eşleşen program bulunamadı.")
                : Lang.T("Bu gün için program bulunamadı."));
    }

    private void UpdateGuideSummary(DateTimeOffset now)
    {
        var channel = PlayingLiveChannel;
        var programmes = channel is not null && !_guideOpen && !Immersive
            ? _guideRequests.GetValueOrDefault(channel.SourceId)?.Guide?.ForChannel(channel) ?? []
            : [];
        var current = programmes.LastOrDefault(programme => programme.IsOnAir(now));
        var next = programmes.FirstOrDefault(programme => programme.Start > now);
        GuideSummary.Visibility = current is not null || next is not null ? Visibility.Visible : Visibility.Collapsed;
        NowProgramme.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
        NextProgramme.Visibility = next is null ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(NextProgramme, current is null ? 0 : 1);
        Grid.SetColumnSpan(NextProgramme, current is null ? 2 : 1);
        Grid.SetColumnSpan(NowProgramme, next is null ? 2 : 1);
        NowProgrammeTitle.Text = current?.Title ?? "";
        var nextLocal = next?.Start.LocalDateTime;
        var nextFormat = nextLocal?.Date == now.LocalDateTime.Date ? "HH:mm" : "d MMM HH:mm";
        NextProgrammeTime.Text = nextLocal is null ? "" : Lang.F($"SIRADA · {nextLocal.Value.ToString(nextFormat, Turkish)}");
        NextProgrammeTitle.Text = next?.Title ?? "";
    }

    private Channel? PlayingLiveChannel => _playingChannel is { IsOnDemand: false } channel && channel.SourceId == _activeSourceId
        ? channel : null;

    private static string ProgrammeTime(GuideProgramme programme)
    {
        var start = programme.Start.LocalDateTime.ToString("HH:mm", Lang.Culture);
        return programme.End is DateTimeOffset end ? $"{start} – {end.LocalDateTime.ToString("HH:mm", Lang.Culture)}" : start;
    }

    private IReadOnlyList<Uri> GuideAddresses(Source source) =>
        source.GuideLocation is { Length: > 0 } custom
            ? [XmlTvLoader.Address(custom)]
            : _catalog.GetValueOrDefault(source.Id)?.GuideLocations ?? [];

    private GuideRequest? GetGuide(Guid sourceId, IReadOnlyList<Uri> locations)
    {
        if (_guideRequests.TryGetValue(sourceId, out var existing)) return existing;
        if (locations.Count == 0 || !_catalog.TryGetValue(sourceId, out var catalog)) return null;
        var request = new GuideRequest(CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token));
        _guideRequests[sourceId] = request;
        _ = LoadGuideAsync(sourceId, request, locations, catalog.Channels, _guideRefresh.Remove(sourceId));
        return request;
    }

    private async Task LoadGuideAsync(Guid sourceId, GuideRequest request, IReadOnlyList<Uri> locations, IReadOnlyList<Channel> channels,
        bool refresh)
    {
        try
        {
            request.Guide = await _guideLoader.LoadAsync(locations, channels, request.Cancellation.Token, refresh);
        }
        catch (OperationCanceledException) when (request.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLog.Warn("Yayın rehberi yüklenemedi.", exception);
            request.Error = exception is SourceException ? exception.Message : Lang.T("Yayın rehberi yüklenemedi.");
        }
        finally
        {
            request.IsLoading = false;
            request.Cancellation.Dispose();
            if (_guideRequests.GetValueOrDefault(sourceId) == request) UpdateGuide();
        }
    }

    private void InvalidateGuide(Guid sourceId)
    {
        if (_guideRequests.Remove(sourceId, out var request) && request.IsLoading)
            request.Cancellation.Cancel();
    }

    private void OnGuidePrevious(object sender, RoutedEventArgs e)
    {
        _guideDay = _guideDay.AddDays(-1);
        UpdateGuide();
    }

    private void OnGuideNext(object sender, RoutedEventArgs e)
    {
        _guideDay = _guideDay.AddDays(1);
        UpdateGuide();
    }

    private void OnGuideReload(object sender, RoutedEventArgs e)
    {
        if (GuideChannel is Channel channel)
        {
            InvalidateGuide(channel.SourceId);
            _guideRefresh.Add(channel.SourceId);
        }
        UpdateGuide();
    }

    private void OnGuideSource(object sender, RoutedEventArgs e)
    {
        if (GuideChannel is not Channel channel) return;
        var index = _state.Sources.FindIndex(source => source.Id == channel.SourceId);
        if (index < 0) return;
        var original = _state.Sources[index];
        var automatic = _catalog.GetValueOrDefault(original.Id)?.GuideLocations.Count > 0;
        var dialog = new GuideSourceWindow(original, automatic) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.GuideLocation == original.GuideLocation) return;

        _state.Sources[index] = original with { GuideLocation = dialog.GuideLocation };
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            _state.Sources[index] = original;
            SetFooter(exception.Message, isError: true);
            return;
        }

        InvalidateGuide(original.Id);
        UpdateGuide();
    }

    private static bool CanCatchUp(Source? source, Channel? channel, DateTimeOffset start, DateTimeOffset now) =>
        source?.Kind == SourceKind.Xtream && channel is { CatchupDays: > 0, IsOnDemand: false } &&
        start < now.AddMinutes(-1) && start >= now.AddDays(-channel.CatchupDays);

    private void UpdateGuidePlayButton()
    {
        var channel = GuideChannel;
        var archive = GuideList.SelectedItem is GuideRow { ShowArchive: true };
        GuidePlay.IsEnabled = channel is not null && (archive || channel.Id != PlayingLiveChannel?.Id);
        var label = archive ? Lang.T("Seçili programı arşivden izle") : Lang.T("Kanalı canlı oynat");
        GuidePlay.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(GuidePlay, label);
    }

    private async void OnGuideRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject target &&
            ItemsControl.ContainerFromElement(GuideList, target) is ListBoxItem { DataContext: GuideRow })
            await PlayFromGuideAsync();
    }

    private async void OnGuidePlay(object sender, RoutedEventArgs e) => await PlayFromGuideAsync();

    private async Task PlayFromGuideAsync()
    {
        if (GuideChannel is not Channel channel) return;
        if (GuideList.SelectedItem is GuideRow { ShowArchive: true } row &&
            _state.Sources.FirstOrDefault(item => item.Id == channel.SourceId) is { } source)
        {
            var archiveChannel = VariantsOf(channel).Prepend(channel).FirstOrDefault(variant => variant.CatchupDays > 0) ?? channel;
            var archive = CatalogLoader.Catchup(source, archiveChannel with { Name = channel.Name }, _catalog.GetValueOrDefault(source.Id)?.TimeZone,
                row.Start, row.End, row.Title);
            if (archive is not null)
            {
                SetGuideOpen(false); // the archive plays like a film, with a timeline under the video
                ShowOsd(Lang.T("Arşivden oynatılıyor"), $"{row.Title} · {row.Start.LocalDateTime.ToString("d MMM HH:mm", Lang.Culture)}");
                await PlayChannelAsync(archive);
                return;
            }
        }
        if (ChannelList.Items.Contains(channel)) ChannelList.SelectedItem = channel;
        await PlayChannelAsync(channel);
    }
}
