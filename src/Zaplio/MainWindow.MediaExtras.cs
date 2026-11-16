using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;

namespace Zaplio;

public partial class MainWindow
{
    private static readonly (string? Value, string Label)[] AspectRatios =
        [(null, "Otomatik"), ("16:9", "16:9"), ("4:3", "4:3"), ("21:9", "21:9"), ("16:10", "16:10"), ("1:1", "1:1")];
    private static readonly (string? Value, string Label)[] CropModes =
        [(null, "Kırpma yok"), ("16:9", "16:9"), ("4:3", "4:3"), ("21:9", "21:9"), ("2.35:1", "2.35:1")];
    private const long DelayStep = 100_000; // LibVLC delays are in microseconds.

    private Channel? _finishedEpisode;
    private string? _aspectRatio;
    private string? _cropGeometry;

    private void UpdateExtraControls()
    {
        TracksButton.IsEnabled = _player is not null;
        PipButton.IsEnabled = _player is not null && !IsSecondScreenActive;
        RecordButton.IsEnabled = _player is not null || IsRecording;
        CinemaButton.IsEnabled = _player is not null || _cinema;
        SecondScreenButton.Visibility = HasSecondMonitor || IsSecondScreenActive ? Visibility.Visible : Visibility.Collapsed;
        SecondScreenButton.IsEnabled = _player is not null || IsSecondScreenActive;
        SecondScreenText.Text = IsSecondScreenActive ? "Geri al" : "2. ekran";
        NextEpisodeButton.Visibility = (_playingChannel ?? _finishedEpisode) is { SeriesId: > 0 } episode &&
            episode.SourceId == _activeSourceId ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Re-applies the session-wide picture options to a freshly started player.</summary>
    private void ApplyVideoOptions(MediaPlayer player)
    {
        player.AspectRatio = _aspectRatio;
        player.CropGeometry = _cropGeometry;
        if (_playingChannel?.IsOnDemand == true && Math.Abs(_playbackRate - 1f) > 0.005f) player.SetRate(_playbackRate);
    }

    /// <summary>Player shortcuts that work for both live and on-demand playback.</summary>
    private bool HandlePlayerKey(KeyEventArgs e)
    {
        if (_player is not { } player || IsTextInputFocused() || Keyboard.Modifiers != ModifierKeys.None) return false;
        switch (e.Key)
        {
            case Key.A:
                var index = Array.FindIndex(AspectRatios, item => item.Value == _aspectRatio);
                var next = AspectRatios[(index + 1) % AspectRatios.Length];
                SetAspectRatio(player, next.Value);
                ShowOsd("Görüntü oranı", next.Label);
                return true;
            case Key.P:
                TogglePip();
                return true;
            case Key.R:
                _ = ToggleRecordingAsync();
                return true;
            case Key.OemComma or Key.OemPeriod when _playingChannel?.IsOnDemand == true:
                ChangeSpeed(faster: e.Key == Key.OemPeriod);
                return true;
            case Key.E when _playingChannel?.IsOnDemand == true:
                StepFrame();
                return true;
            case Key.C:
                SetCinema(!_cinema);
                return true;
            case Key.M:
                OnMute(this, new RoutedEventArgs());
                ShowOsd(_muted || VolumeSlider.Value == 0 ? "Ses kapalı" : "Ses açık", "");
                return true;
            case Key.S:
                _ = TakeSnapshotAsync(player);
                return true;
            default:
                return false;
        }
    }

    private void OnTracks(object sender, RoutedEventArgs e)
    {
        if (_player is not { } player) return;
        var button = (Button)sender;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Top };
        menu.Items.Add(BuildTracks("Ses", player.AudioTrackDescription, player.AudioTrack, id => player.SetAudioTrack(id)));
        menu.Items.Add(BuildTracks("Altyazı", player.SpuDescription, player.Spu, id => player.SetSpu(id), subtitles: true));
        if (_playingChannel is { IsOnDemand: false } live &&
            ActiveChannels.FirstOrDefault(channel => channel.Id == RowId(live.Id)) is { } row && BuildQualityMenu(row) is { } quality)
            menu.Items.Add(quality);
        menu.Items.Add(new Separator());
        menu.Items.Add(BuildChoice("Görüntü oranı  (A)", AspectRatios, _aspectRatio, value => SetAspectRatio(player, value)));
        menu.Items.Add(BuildChoice("Kırpma", CropModes, _cropGeometry, value =>
        {
            _cropGeometry = value;
            if (ReferenceEquals(player, _player)) player.CropGeometry = value;
        }));
        menu.Items.Add(BuildDelay("Ses gecikmesi", player.AudioDelay, delay => player.SetAudioDelay(delay)));
        menu.Items.Add(BuildDelay("Altyazı gecikmesi", player.SpuDelay, delay => player.SetSpuDelay(delay)));
        if (_playingChannel?.IsOnDemand == true)
        {
            var rates = PlaybackSpeed.Ladder.Select(rate => ((string?)rate.ToString(CultureInfo.InvariantCulture),
                rate == 1f ? "Normal" : $"{rate.ToString("0.##", Turkish)}×")).ToArray();
            menu.Items.Add(BuildChoice("Oynatma hızı  ( , / . )", rates, _playbackRate.ToString(CultureInfo.InvariantCulture), value =>
            {
                if (ReferenceEquals(player, _player) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate))
                    SetSpeed(rate);
            }));
        }
        menu.Items.Add(new Separator());
        var ambientLight = new MenuItem { Header = "Ortam ışığı", IsCheckable = true, IsChecked = _state.Settings.AmbientLight };
        ambientLight.Click += (_, _) => SetAmbientLightEnabled(ambientLight.IsChecked);
        menu.Items.Add(ambientLight);
        var record = new MenuItem { Header = IsRecording ? "Kaydı durdur  (R)" : "Kaydet  (R)" };
        record.Click += async (_, _) => await ToggleRecordingAsync();
        menu.Items.Add(record);
        var snapshot = new MenuItem { Header = "Ekran görüntüsü al  (S)" };
        snapshot.Click += async (_, _) => await TakeSnapshotAsync(player);
        menu.Items.Add(snapshot);
        menu.IsOpen = true;

        MenuItem BuildTracks(string label, TrackDescription[]? tracks, int current, Func<int, bool> select, bool subtitles = false)
        {
            var group = new MenuItem { Header = label };
            if (subtitles && !(tracks ?? []).Any(track => track.Id == -1)) Add(-1, "Kapalı");
            foreach (var track in tracks ?? []) Add(track.Id, track.Id == -1 ? "Kapalı" : track.Name);
            if (group.Items.Count == 0) group.IsEnabled = false;
            return group;

            void Add(int id, string name)
            {
                var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = id == current };
                item.Click += (_, _) =>
                {
                    if (ReferenceEquals(player, _player) && !select(id))
                        SetFooter("Parça değiştirilemedi.", isError: true);
                };
                group.Items.Add(item);
            }
        }

        MenuItem BuildChoice(string label, IEnumerable<(string? Value, string Label)> choices, string? current, Action<string?> apply)
        {
            var group = new MenuItem { Header = label };
            foreach (var (value, text) in choices)
            {
                var item = new MenuItem { Header = text, IsCheckable = true, IsChecked = value == current };
                item.Click += (_, _) => apply(value);
                group.Items.Add(item);
            }
            return group;
        }

        MenuItem BuildDelay(string label, long current, Func<long, bool> apply)
        {
            var group = new MenuItem { Header = $"{label}: {DelayLabel(current)}" };
            Add("+100 ms", current + DelayStep);
            Add("−100 ms", current - DelayStep);
            Add("+500 ms", current + 5 * DelayStep);
            Add("−500 ms", current - 5 * DelayStep);
            Add("Sıfırla", 0);
            return group;

            void Add(string text, long value)
            {
                var item = new MenuItem { Header = text };
                item.Click += (_, _) =>
                {
                    if (!ReferenceEquals(player, _player)) return;
                    if (apply(value)) ShowOsd(label, DelayLabel(value));
                    else SetFooter($"{label} değiştirilemedi.", isError: true);
                };
                group.Items.Add(item);
            }
        }
    }

    private static string DelayLabel(long microseconds) =>
        $"{(microseconds > 0 ? "+" : "")}{(microseconds / 1000).ToString("N0", Turkish)} ms";

    private void SetAspectRatio(MediaPlayer player, string? value)
    {
        _aspectRatio = value;
        if (ReferenceEquals(player, _player)) player.AspectRatio = value;
    }

    private async Task TakeSnapshotAsync(MediaPlayer player)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Zaplio");
        var file = Path.Combine(folder, $"Zaplio_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        bool saved;
        await _playbackLock.WaitAsync();
        try
        {
            if (_closing || player != _player) return;
            Directory.CreateDirectory(folder);
            saved = await Task.Run(() => player.TakeSnapshot(0, file, 0, 0));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Error("Ekran görüntüsü klasörü oluşturulamadı.", exception);
            saved = false;
        }
        finally
        {
            _playbackLock.Release();
        }
        if (_closing) return;
        if (saved)
        {
            ShowOsd("Ekran görüntüsü kaydedildi", Path.GetFileName(file));
            SetFooter($"Ekran görüntüsü kaydedildi: {file}");
        }
        else SetFooter("Ekran görüntüsü alınamadı.", isError: true);
    }

    private async void OnNextEpisode(object sender, RoutedEventArgs e)
    {
        if ((_playingChannel ?? _finishedEpisode) is not { SeriesId: > 0 } episode || ActiveSource is not { } source) return;
        NextEpisodeButton.IsEnabled = false;
        try
        {
            var title = episode.Name.Split(" · ", 2)[0];
            var episodes = await _loader.LoadEpisodesAsync(source, new VodItem(episode.SeriesId, title, "", "", null), _lifetime.Token);
            if (_closing || _activeSourceId != source.Id || (_playingChannel ?? _finishedEpisode)?.Id != episode.Id) return;
            var next = episodes.SkipWhile(item => item.ProviderId != episode.EpisodeId).Skip(1).FirstOrDefault()?.Playable;
            if (next is null) SetFooter("Bu dizide sonraki bölüm bulunamadı.");
            else await PlayChannelAsync(next);
        }
        catch (Exception exception)
        {
            if (!_closing) SetFooter(CatalogLoader.UserMessage(exception), isError: true);
        }
        finally { NextEpisodeButton.IsEnabled = true; }
    }

    private async void OnVodDetails(object sender, RoutedEventArgs e)
    {
        if (VodList.SelectedItem is not VodRow row || ActiveSource is not { } source) return;
        var selection = row.Item;
        var target = _selectedSeries ?? selection;
        var seasonCount = _selectedSeries is null ? 0 : _vodItems.Select(item => item.Season).Distinct().Count();
        var details = new MediaDetailsWindow(target, seasonCount) { Owner = this };
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        details.Closed += (_, _) => request.Cancel();
        details.Loaded += async (_, _) =>
        {
            try { details.UpdateDetails(await _loader.LoadVodDetailsAsync(source, target, request.Token), seasonCount); }
            catch (Exception) { /* The catalog metadata remains available if the detail endpoint fails. */ }
        };
        if (details.ShowDialog() == true && source.Id == _activeSourceId) await ActivateVodAsync(selection);
    }
}
