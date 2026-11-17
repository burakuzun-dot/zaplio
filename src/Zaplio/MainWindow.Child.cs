using System.Windows;

namespace Zaplio;

/// <summary>The kids profile: allowed categories only, and a PIN in front of Settings, sources and leaving the profile.</summary>
public partial class MainWindow
{
    private readonly PinGate _pinGate = new();

    private bool ChildMode => _state.Child.Active;

    /// <summary>The movies or series a child may see (episodes inside an allowed series are never filtered).</summary>
    private IReadOnlyList<VodItem> ForChild(IReadOnlyList<VodItem> items, Guid source, LibrarySection section) =>
        ChildMode ? items.Where(item => _state.Child.Allows(source, section, item.Category)).ToList() : items;

    /// <summary>Whether something from the watch history may be offered: an episode counts as its series.</summary>
    private bool ChildMayWatch(Channel channel)
    {
        if (!ChildMode) return true;
        if (channel.SeriesId == 0) return _state.Child.AllowsChannel(channel);
        var series = (_vodCatalogs.GetValueOrDefault((channel.SourceId, LibrarySection.Series)) ?? [])
            .FirstOrDefault(item => item.ProviderId == channel.SeriesId);
        return series is not null && _state.Child.Allows(channel.SourceId, LibrarySection.Series, series.Category);
    }

    /// <summary>Asks for the PIN while the kids profile is on; true when the way is open.</summary>
    private bool UnlockChild(Window owner, string purpose)
    {
        if (!ChildMode) return true;
        var hint = $"{purpose} için çocuk profilinin PIN'ini gir.";
        while (true)
        {
            var wait = _pinGate.Wait(DateTimeOffset.UtcNow);
            if (wait > TimeSpan.Zero)
            {
                SetFooter($"Çok fazla yanlış PIN denendi. {Math.Ceiling(wait.TotalSeconds):0} saniye sonra tekrar dene.", isError: true);
                return false;
            }
            var prompt = new PasswordPromptWindow("Ebeveyn PIN'i", hint, "Aç", confirm: false, minimum: ChildProfile.PinMinimum)
                { Owner = owner };
            prompt.FirstLabel.Text = "PIN";
            prompt.First.MaxLength = ChildProfile.PinMaximum;
            if (prompt.ShowDialog() != true) return false;
            if (_state.Child.CheckPin(prompt.Value))
            {
                _pinGate.Succeeded();
                return true;
            }
            _pinGate.Failed(DateTimeOffset.UtcNow);
            AppLog.Info("Çocuk profili: yanlış PIN.");
            hint = "PIN yanlış. Tekrar dene.";
        }
    }

    private void OnChildBadge(object sender, RoutedEventArgs e)
    {
        if (!ChildMode || !UnlockChild(this, "Çocuk profilinden çıkmak")) return;
        SetChildActive(false);
    }

    /// <summary>Settings › Genel › Çocuk profili (the settings window is already behind the PIN).</summary>
    private async Task EditChildProfileAsync(Window owner)
    {
        if (ActiveSource is not { } source)
        {
            SetFooter("Çocuk profili için önce bir kaynak seç.", isError: true);
            return;
        }

        var categories = new Dictionary<LibrarySection, IReadOnlyList<string>?>();
        if (_catalog.GetValueOrDefault(source.Id) is { } catalog)
        {
            var live = catalog.Channels.SelectMany(channel => channel.Categories).Distinct(CategoryComparer).ToList();
            if (catalog.Channels.Any(channel => channel.Categories.Length == 0)) live.Add("");
            categories[LibrarySection.Live] = live;
        }
        else categories[LibrarySection.Live] = null;

        if (source.Kind == SourceKind.Xtream)
            foreach (var section in new[] { LibrarySection.Movies, LibrarySection.Series })
            {
                // Only lists already here or saved on disk: setting up the profile must not send requests to the provider.
                var items = _vodCatalogs.GetValueOrDefault((source.Id, section));
                if (items is null && await Task.Run(() => _catalogCache.LoadVod(source, section)) is { } stored)
                    items = stored.Value;
                categories[section] = items?.Select(item => item.Category).Distinct(CategoryComparer).ToList();
            }

        var dialog = new ChildProfileWindow(source.Name, _state.Child, source.Id, categories) { Owner = owner };
        if (dialog.ShowDialog() != true) return;

        var before = System.Text.Json.JsonSerializer.Serialize(_state.Child);
        foreach (var (section, allowed) in dialog.Allowed) _state.Child.SetAllowed(source.Id, section, allowed);
        if (dialog.NewPin is { } pin) _state.Child.SetPin(pin);
        if (dialog.Result != ChildProfileWindow.Outcome.Save) _state.Child.Active = dialog.Result == ChildProfileWindow.Outcome.Start;
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            _state.Child = System.Text.Json.JsonSerializer.Deserialize<ChildProfile>(before) ?? new ChildProfile();
            SetFooter(exception.Message, isError: true);
            return;
        }
        AppLog.Info($"Çocuk profili {(dialog.Result switch { ChildProfileWindow.Outcome.Start => "açıldı", ChildProfileWindow.Outcome.Stop => "kapatıldı", _ => "güncellendi" })}.");
        if (dialog.Result == ChildProfileWindow.Outcome.Start) owner.Close(); // nothing in Settings is for the child
        ApplyChildProfile();
        SetFooter(dialog.Result switch
        {
            ChildProfileWindow.Outcome.Start => "Çocuk profili açık · yalnızca izin verdiğin kategoriler görünüyor.",
            ChildProfileWindow.Outcome.Stop => "Çocuk profili kapatıldı.",
            _ => "Çocuk profili kaydedildi."
        });
    }

    private void SetChildActive(bool active)
    {
        _state.Child.Active = active;
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            _state.Child.Active = !active;
            SetFooter(exception.Message, isError: true);
            return;
        }
        AppLog.Info(active ? "Çocuk profili açıldı." : "Çocuk profili kapatıldı.");
        ApplyChildProfile();
        SetFooter(active ? "Çocuk profili açık." : "Çocuk profili kapatıldı.");
    }

    /// <summary>Shows the lists as the profile allows them now.</summary>
    private void ApplyChildProfile()
    {
        ChildBadge.Visibility = ChildMode ? Visibility.Visible : Visibility.Collapsed;
        _sourceSettings?.NotifyChanged();
        _customizationVersion++;
        if (_librarySection == LibrarySection.Live) UpdateCatalog(selectedChannel: RowId((ChannelList.SelectedItem as Channel)?.Id));
        else _ = LoadVodViewAsync();
        if (ChildMode && _playingChannel is { } playing && !ChildMayWatch(playing) && !LocalMedia.IsLocal(playing))
            _ = StopPlaybackAsync("Çocuk profilinde bu içerik açık değil.");
    }

    private string ChildStatus() => !_state.Child.HasPin ? "Kapalı · henüz ayarlanmadı."
        : ChildMode ? "Açık · yalnızca izin verilen kategoriler görünüyor."
        : "Kapalı · ayarlar hazır.";
}
