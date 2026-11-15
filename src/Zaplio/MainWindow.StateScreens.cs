using System.Net;
using System.Net.NetworkInformation;
using System.Windows;

namespace Zaplio;

/// <summary>Favourite bolt on rows, "channel is sleeping", "connection lost" and "add your first list" screens.</summary>
public partial class MainWindow
{
    private Channel? _sleepingChannel;
    private int _sleepingFailures;
    private Channel? _offlineResume;
    private bool _offlineRetrying;

    private void InitializeStateScreens()
    {
        ChannelFavoriteConverter.Lookup = IsFavorite;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    private void ReleaseStateScreens() => NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

    // ── Favourite bolt on channel rows ──────────────────────────────────────────────────────────────

    private async void OnRowFavoriteToggled(object? sender, bool added)
    {
        if ((sender as FrameworkElement)?.DataContext is not Channel channel || channel.SourceId != _activeSourceId) return;
        var undo = ToggleFavorite(channel);
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            undo();
            SetFooter(exception.Message, isError: true);
            UpdateCatalog();
            return;
        }
        _customizationVersion++;
        SetFooter(IsFavorite(channel) ? $"{channel.Name} favorilere eklendi." : $"{channel.Name} favorilerden çıkarıldı.");
        // Rebuilding the list now would cut the bolt animation short; the favourites group has to drop the row at once.
        if (added && SystemParameters.ClientAreaAnimation && GroupList.SelectedItem is GroupEntry { Key: not "favorites" })
            await Task.Delay(700);
        if (_closing) return;
        UpdateCatalog(selectedChannel: (ChannelList.SelectedItem as Channel)?.Id);
    }

    // ── First list ──────────────────────────────────────────────────────────────────────────────────

    private void OnEmptyLibraryAdd(object? sender, EventArgs e) => OnSources(this, new RoutedEventArgs());

    // ── "This channel is sleeping" ──────────────────────────────────────────────────────────────────

    private void ShowSleeping(Channel channel)
    {
        if (_sleepingChannel?.Id == channel.Id)
        {
            // The retry failed too: count it and start the countdown again (or wait for the user after a few failures).
            _sleepingFailures++;
            SleepingView.AutoRetrySeconds = StateScreenPolicy.NextAutoRetrySeconds(_sleepingFailures);
            SleepingView.RetryFailed();
            return;
        }
        _sleepingChannel = channel;
        _sleepingFailures = 0;
        var index = VisibleChannels.FindIndex(item => item.Id == RowId(channel.Id));
        SleepingView.ChannelNumber = index >= 0 ? (index + 1).ToString() : "–";
        SleepingView.ChannelName = channel.Name;
        SleepingView.AutoRetrySeconds = StateScreenPolicy.NextAutoRetrySeconds(0);
        SleepingView.Visibility = Visibility.Visible;
    }

    private void HideSleeping()
    {
        if (_sleepingChannel is null) return;
        _sleepingChannel = null;
        _sleepingFailures = 0;
        SleepingView.Visibility = Visibility.Collapsed;
    }

    private async void OnSleepingRetry(object? sender, EventArgs e)
    {
        if (_sleepingChannel is { } channel) await PlayChannelAsync(channel);
    }

    private async void OnSleepingNext(object? sender, EventArgs e)
    {
        HideSleeping();
        await ZapAsync(1);
    }

    // ── "Connection lost" ───────────────────────────────────────────────────────────────────────────

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable || _closing) return;
        _ = Dispatcher.InvokeAsync(async () =>
        {
            // VPN and Wi-Fi switches report short drops; only react when the network is still gone a moment later.
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (_closing || NetworkInterface.GetIsNetworkAvailable() || OfflineView.IsVisible) return;
            if (_playingChannel is not { } channel) return;
            _offlineResume = channel;
            await StopPlaybackAsync("İnternet bağlantısı yok");
            if (!_closing) OfflineView.Visibility = Visibility.Visible;
        });
    }

    private async void OnOfflineRetry(object? sender, EventArgs e)
    {
        if (_offlineRetrying) return;
        _offlineRetrying = true;
        try
        {
            if (!await CanReachNetworkAsync())
            {
                OfflineView.RetryFailed();
                return;
            }
            OfflineView.Visibility = Visibility.Collapsed;
            var channel = _offlineResume;
            _offlineResume = null;
            if (channel is not null && !_closing) await PlayChannelAsync(channel);
        }
        finally
        {
            _offlineRetrying = false;
        }
    }

    /// <summary>Network is up and the source host name resolves. Only a DNS lookup: no request goes to the provider.</summary>
    private async Task<bool> CanReachNetworkAsync()
    {
        if (!NetworkInterface.GetIsNetworkAvailable()) return false;
        if (ActiveSource is not { Kind: not SourceKind.M3uFile } source || !Uri.TryCreate(source.Location, UriKind.Absolute, out var uri))
            return true;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await Dns.GetHostAddressesAsync(uri.Host, timeout.Token);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !_lifetime.IsCancellationRequested)
        {
            return false;
        }
    }
}
