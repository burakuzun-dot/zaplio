using System.Windows;

namespace Zaplio;

public partial class MainWindow
{
    private bool _refreshingAccount;
    private readonly HashSet<Guid> _expiryWarned = new();
    private SourceSettingsHost? _sourceSettings;

    /// <summary>The bridge Settings › Kaynaklar uses to pick the active source, refresh it and show its subscription.</summary>
    private SourceSettingsHost SourceSettings => _sourceSettings ??= new SourceSettingsHost
    {
        ActiveSourceId = () => _activeSourceId,
        Activate = ActivateSourceAsync,
        CanRefresh = CanRefreshLists,
        Refresh = () => LoadActiveSectionAsync(refresh: true),
        Account = CurrentAccountLine,
        CheckAccount = CheckAccountAsync
    };

    /// <summary>The active Xtream subscription (expiry, connections), shown in Settings › Kaynaklar.</summary>
    private AccountLine? CurrentAccountLine()
    {
        if (ActiveSource is not { Kind: SourceKind.Xtream } source) return null;
        if (_catalog.GetValueOrDefault(source.Id)?.Account is not { } account)
            return new AccountLine(_refreshingAccount ? "Kontrol ediliyor…" : "Abonelik bilgisi henüz alınmadı.", "Muted", null, !_refreshingAccount);

        var now = DateTimeOffset.UtcNow;
        return new AccountLine(_refreshingAccount ? "Kontrol ediliyor…" : account.CardLine(now),
            account.IsExpired(now) ? "Error" : account.ExpiresSoon(now) ? "Warning" : "Muted", account.Details(now), !_refreshingAccount);
    }

    /// <summary>Warns once in the footer when the subscription is about to end, and refreshes an open settings window.</summary>
    private void UpdateAccountCard()
    {
        var now = DateTimeOffset.UtcNow;
        if (ActiveSource is { Kind: SourceKind.Xtream } source && _catalog.GetValueOrDefault(source.Id)?.Account is { } account &&
            account.Warning(source.Name, now) is { } warning && _expiryWarned.Add(source.Id))
            SetFooter(warning, isError: true);
        _sourceSettings?.NotifyChanged();
    }

    private async Task CheckAccountAsync()
    {
        if (_refreshingAccount || ActiveSource is not { Kind: SourceKind.Xtream } source) return;
        _refreshingAccount = true;
        UpdateAccountCard();
        try
        {
            var account = await _loader.LoadAccountAsync(source, _lifetime.Token);
            if (_catalog.GetValueOrDefault(source.Id) is { } catalog)
            {
                var updated = catalog with { Account = account };
                _catalog[source.Id] = updated;
                SaveCatalogCache(source, updated);
            }
            SetFooter($"{source.Name} · abonelik bilgisi güncellendi.");
        }
        catch (Exception exception) when (!_closing)
        {
            AppLog.Warn($"Abonelik bilgisi alınamadı: {source.Name}", exception);
            SetFooter($"{source.Name} · {CatalogLoader.UserMessage(exception)}", isError: true);
        }
        finally
        {
            _refreshingAccount = false;
            if (!_closing) UpdateAccountCard();
        }
    }
}
