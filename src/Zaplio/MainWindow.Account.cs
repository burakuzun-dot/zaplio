using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Zaplio;

public partial class MainWindow
{
    private bool _refreshingAccount;
    private readonly HashSet<Guid> _expiryWarned = new();

    /// <summary>Shows the active Xtream subscription (expiry, connections) under the source name.</summary>
    private void UpdateAccountCard()
    {
        var account = ActiveSource is { Kind: SourceKind.Xtream } source ? _catalog.GetValueOrDefault(source.Id)?.Account : null;
        AccountCard.Cursor = ActiveSource?.Kind == SourceKind.Xtream ? Cursors.Hand : null;
        if (account is null)
        {
            AccountText.Visibility = Visibility.Collapsed;
            AccountCard.ToolTip = ActiveSource is { Kind: SourceKind.Xtream } ? $"{ActiveSource.Name}\nAbonelik bilgisini almak için tıkla" : ActiveSource?.Name;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        AccountText.Text = _refreshingAccount ? "Kontrol ediliyor…" : account.CardLine(now);
        AccountText.Foreground = (Brush)FindResource(account.IsExpired(now) ? "Error" : account.ExpiresSoon(now) ? "Warning" : "Muted");
        AccountText.Visibility = Visibility.Visible;
        AccountCard.ToolTip = $"{ActiveSource!.Name}\n{account.Details(now)}";

        if (account.Warning(ActiveSource.Name, now) is { } warning && _expiryWarned.Add(ActiveSource.Id))
            SetFooter(warning, isError: true);
    }

    private async void OnAccountCardClick(object sender, MouseButtonEventArgs e)
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
