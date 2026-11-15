using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace Zaplio;

public partial class MainWindow
{
    private UpdateInfo? _availableUpdate;

    /// <summary>Background chores that must not delay the first catalog load.</summary>
    private async Task RunStartupMaintenanceAsync()
    {
        _ = Task.Run(() => CatalogCache.Prune(TimeSpan.FromDays(30)));
        if (!_state.Settings.CheckForUpdates || SelfTest.Active) return; // the self test makes no network request
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(6), _lifetime.Token);
            var update = await UpdateChecker.FindNewerAsync(_lifetime.Token);
            if (update is null || _closing) return;
            _availableUpdate = update;
            UpdateText.Text = $"Yeni sürüm {update.Version} hazır";
            UpdateButton.ToolTip = "Sürüm sayfasını tarayıcıda aç";
            UpdateButton.Visibility = Visibility.Visible;
            AppLog.Info($"Yeni sürüm bulundu: {update.Tag}");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLog.Warn("Güncelleme denetimi başarısız.", exception);
        }
    }

    private void OnUpdate(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is not { } update) return;
        try
        {
            Process.Start(new ProcessStartInfo(update.Page.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            SetFooter($"Tarayıcı açılamadı: {update.Page}", isError: true);
        }
    }
}
