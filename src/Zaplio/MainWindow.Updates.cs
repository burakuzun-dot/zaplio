using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Threading;

namespace Zaplio;

public partial class MainWindow
{
    private UpdateInfo? _availableUpdate;
    private DispatcherTimer? _updateConfirm;
    private bool _updating;

    /// <summary>The installed copy can update itself; the ZIP version and development builds open the release page instead.</summary>
    private static bool CanUpdateInPlace(UpdateInfo update) =>
        update.Installer is not null && UpdateInstaller.IsInstalledCopy(AppContext.BaseDirectory);

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
            UpdateText.Text = Lang.F($"Yeni sürüm {update.Version} hazır");
            UpdateButton.ToolTip = CanUpdateInPlace(update)
                ? Lang.T("Zaplio güncellemeyi GitHub'dan indirip kurar ve yeniden açılır")
                : Lang.T("Sürüm sayfasını tarayıcıda aç");
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

    private async void OnUpdate(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is not { } update || _updating) return;
        if (CanUpdateInPlace(update))
        {
            // Two presses: the first says what will happen, the second (within 8 seconds) starts it.
            if (_updateConfirm is not { IsEnabled: true })
            {
                UpdateText.Text = Lang.T("İndir ve kur · Zaplio kapanıp yeniden açılır");
                _updateConfirm ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                _updateConfirm.Tick -= OnUpdateConfirmTimeout;
                _updateConfirm.Tick += OnUpdateConfirmTimeout;
                _updateConfirm.Start();
                return;
            }
            _updateConfirm.Stop();
            await InstallUpdateAsync(update);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(update.Page.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            SetFooter(Lang.F($"Tarayıcı açılamadı: {update.Page}"), isError: true);
        }
    }

    /// <summary>Started by the installer after an in-app update.</summary>
    internal void AnnounceUpdated()
    {
        AppLog.Info($"Zaplio {UpdateChecker.CurrentVersion} sürümüne güncellendi.");
        EventHandler? shown = null;
        shown = (_, _) =>
        {
            ContentRendered -= shown;
            SetFooter(Lang.F($"Zaplio {UpdateChecker.CurrentVersion} sürümüne güncellendi. Yenilikler: Ayarlar › Hakkında."));
        };
        ContentRendered += shown;
    }

    private void OnUpdateConfirmTimeout(object? sender, EventArgs e)
    {
        _updateConfirm?.Stop();
        if (_availableUpdate is { } update && !_updating) UpdateText.Text = Lang.F($"Yeni sürüm {update.Version} hazır");
    }

    /// <summary>Downloads, checks and runs the installer; Zaplio closes and the installer opens the new version.</summary>
    private async Task InstallUpdateAsync(UpdateInfo update)
    {
        _updating = true;
        UpdateButton.IsEnabled = false;
        var progress = new Progress<double>(part => UpdateText.Text = Lang.F($"İndiriliyor %{Math.Clamp((int)(part * 100), 0, 100)}"));
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Zaplio", UpdateChecker.CurrentVersion.ToString()));
            var folder = Path.Combine(Path.GetTempPath(), "Zaplio-guncelleme");
            var installer = await UpdateInstaller.DownloadAsync(http, update.Installer!, folder, progress, _lifetime.Token);
            AppLog.Info($"Güncelleme indirildi ve doğrulandı: {update.Tag}");

            UpdateText.Text = Lang.T("Kuruluyor…");
            SetFooter(Lang.F($"Zaplio {update.Version} kuruluyor; birazdan yeniden açılacak."));
            var setup = Process.Start(new ProcessStartInfo(installer, UpdateInstaller.InstallerArguments) { UseShellExecute = true });
            // Windows may refuse an unsigned installer (Smart App Control); then Zaplio stays open instead of vanishing.
            await Task.Delay(1500);
            if (setup is null || setup.HasExited && setup.ExitCode != 0)
                throw new InvalidOperationException(Lang.T("Kurulum başlatılamadı; Windows engellemiş olabilir."));
            Close();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLog.Warn("Güncelleme kurulamadı.", exception);
            SetFooter(exception is InvalidDataException or InvalidOperationException
                ? Lang.F($"Güncelleme kurulamadı: {exception.Message}")
                : Lang.T("Güncelleme indirilemedi; bağlantını kontrol edip yeniden dene."), isError: true);
            _updating = false;
            UpdateButton.IsEnabled = true;
            UpdateText.Text = Lang.F($"Yeni sürüm {update.Version} hazır");
        }
    }
}
