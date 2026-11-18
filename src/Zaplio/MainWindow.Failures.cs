using System.Net.NetworkInformation;
using System.Windows;

namespace Zaplio;

/// <summary>
/// After a stream would not open: tells the user when the subscription has no free connection left, and otherwise
/// dims the live channel in the list for a day (<see cref="ChannelFailures"/>). Learned only from the user's own tries.
/// </summary>
public partial class MainWindow
{
    public static readonly DependencyProperty FailureMarksProperty =
        DependencyProperty.Register(nameof(FailureMarks), typeof(int), typeof(MainWindow), new PropertyMetadata(0));

    /// <summary>Bumped whenever a failure mark changes; channel rows bind to it to re-check.</summary>
    public int FailureMarks
    {
        get => (int)GetValue(FailureMarksProperty);
        set => SetValue(FailureMarksProperty, value);
    }

    /// <summary>One subscription check per source every two minutes at most, however many streams fail.</summary>
    private readonly CheckSpacing _limitChecks = new(TimeSpan.FromMinutes(2));

    private DateTimeOffset? FailedRecently(Channel channel)
    {
        var now = DateTimeOffset.UtcNow;
        return ChannelFailures.Recent(_state.FailedChannels, channel.Id, now)
               ?? ChannelFailures.Recent(_state.FailedChannels, RowId(channel.Id) ?? channel.Id, now);
    }

    /// <summary>Called once a stream has been given up on. <paramref name="everPlayed"/>: it did play before dropping.</summary>
    private async void AfterStreamFailed(Channel channel, bool everPlayed)
    {
        if (everPlayed || LocalMedia.IsLocal(channel) || _closing) return;

        if (_state.Sources.FirstOrDefault(source => source.Id == channel.SourceId) is { Kind: SourceKind.Xtream } source &&
            _limitChecks.TryStart(source.Id, DateTimeOffset.UtcNow))
        {
            try
            {
                var account = await _loader.LoadAccountAsync(source, _lifetime.Token);
                StoreAccount(source, account);
                if (account.LimitMessage(source.Name) is { } message)
                {
                    AppLog.Warn($"Yayın açılamadı, hesabın bütün bağlantıları kullanımda ({account.ActiveConnections}/{account.MaxConnections}).");
                    if (_player is null && !_closing) ShowPlaybackStopped(message, isError: true);
                    SetFooter(message, isError: true);
                    return; // not the channel's fault: do not dim it
                }
            }
            catch (Exception exception) when (!_closing)
            {
                AppLog.Warn($"Abonelik bilgisi alınamadı: {source.Name}", exception);
            }
        }

        // Only live channels, and only when this computer is online: a dropped network is not the channel's fault.
        if (channel.IsOnDemand || _closing || !NetworkInterface.GetIsNetworkAvailable()) return;
        ChannelFailures.Record(_state.FailedChannels, RowId(channel.Id) ?? channel.Id, DateTimeOffset.UtcNow);
        ChannelFailures.Prune(_state.FailedChannels, DateTimeOffset.UtcNow);
        SaveFailureMarks();
    }

    /// <summary>A live channel that opens is no longer marked.</summary>
    private void ClearFailureMark(Channel channel)
    {
        if (channel.IsOnDemand) return;
        var cleared = ChannelFailures.Clear(_state.FailedChannels, channel.Id) |
                      ChannelFailures.Clear(_state.FailedChannels, RowId(channel.Id) ?? channel.Id);
        if (cleared) SaveFailureMarks();
    }

    private void SaveFailureMarks()
    {
        FailureMarks++;
        try { SaveState(); }
        catch (SourceException exception) { AppLog.Warn("Açılmayan kanal işareti kaydedilemedi.", exception); }
    }

    /// <summary>Keeps a freshly loaded subscription (expiry, connections) for the settings card and the cache.</summary>
    private void StoreAccount(Source source, AccountInfo account)
    {
        if (_catalog.GetValueOrDefault(source.Id) is not { } catalog) return;
        var updated = catalog with { Account = account };
        _catalog[source.Id] = updated;
        SaveCatalogCache(source, updated);
        _sourceSettings?.NotifyChanged();
    }
}
