using System.Windows;
using System.Windows.Threading;

namespace Zaplio;

public partial class App : Application
{
    private bool _reportedCrash;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("İşlenmeyen hata (uygulama kapanıyor).", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Gözlemlenmeyen görev hatası.", args.Exception);
            args.SetObserved();
        };
        AppLog.Info($"Zaplio {UpdateChecker.CurrentVersion} başlatıldı · {Environment.OSVersion}");
        base.OnStartup(e);
        ShowMainWindow();
    }

    /// <summary>
    /// Creates the main window right away (so VLC warms up meanwhile) and shows it after the intro animation,
    /// or immediately when the intro is turned off in settings.
    /// </summary>
    private void ShowMainWindow()
    {
        var main = new MainWindow();
        MainWindow = main;
        main.WarmUp();
        if (!IntroEnabled())
        {
            main.Show();
            return;
        }

        var intro = new IntroWindow();
        intro.Finished += () =>
        {
            main.Show();
            main.Activate();
            if (!intro.IsClosed) intro.Close();
        };
        intro.Show();
    }

    private static bool IntroEnabled()
    {
        try
        {
            return new LocalStateStore().Load().Settings.ShowIntro;
        }
        catch (Exception exception)
        {
            AppLog.Warn("Ayarlar okunamadı; açılış animasyonu varsayılan olarak gösteriliyor.", exception);
            return true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLog.Info("Zaplio kapatıldı.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Arayüzde işlenmeyen hata.", e.Exception);
        e.Handled = true;
        if (_reportedCrash) return;
        _reportedCrash = true;
        MessageBox.Show(
            $"Beklenmeyen bir hata oluştu. Uygulama çalışmaya devam edecek.\n\nAyrıntılar günlük dosyasına yazıldı:\n{AppLog.Folder}",
            "Zaplio", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
