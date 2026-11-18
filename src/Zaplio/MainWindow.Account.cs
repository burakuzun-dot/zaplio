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
        CheckAccount = CheckAccountAsync,
        CurrentState = () => _state,
        Restore = RestoreFromBackup,
        EditChildProfile = EditChildProfileAsync,
        ChildStatus = ChildStatus
    };

    private bool _restartingWithoutSave;

    /// <summary>
    /// A restored backup replaces everything: it is written as the saved state and Zaplio restarts on it. Nothing from this
    /// session is saved over it on the way out.
    /// </summary>
    private void RestoreFromBackup(LocalState restored)
    {
        try
        {
            new LocalStateStore().Save(restored);
        }
        catch (Exception exception) when (IsStorageError(exception))
        {
            SetFooter(Lang.T("Yedek bu bilgisayara yazılamadı."), isError: true);
            return;
        }
        _restartingWithoutSave = true;
        foreach (var window in OwnedWindows.Cast<Window>().ToArray()) window.Close();
        ((App)Application.Current).Restart();
    }

    /// <summary>The active Xtream subscription (expiry, connections), shown in Settings › Kaynaklar.</summary>
    private AccountLine? CurrentAccountLine()
    {
        if (ActiveSource is not { Kind: SourceKind.Xtream } source) return null;
        if (_catalog.GetValueOrDefault(source.Id)?.Account is not { } account)
            return new AccountLine(_refreshingAccount ? Lang.T("Kontrol ediliyor…") : Lang.T("Abonelik bilgisi henüz alınmadı."), "Muted", null, !_refreshingAccount);

        var now = DateTimeOffset.UtcNow;
        return new AccountLine(_refreshingAccount ? Lang.T("Kontrol ediliyor…") : account.CardLine(now),
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
            SetFooter(Lang.F($"{source.Name} · abonelik bilgisi güncellendi."));
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
