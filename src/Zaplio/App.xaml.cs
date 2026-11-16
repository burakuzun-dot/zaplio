using System.Windows;
using System.Windows.Threading;

namespace Zaplio;

public partial class App : Application
{
    private bool _reportedCrash;

    private string[] _startupFiles = [];

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
        if (!SelfTest.TryStart(e.Args, out var refusal))
        {
            AppLog.Error(refusal!);
            Shutdown(2);
            return;
        }
        AppLog.Info($"Zaplio {UpdateChecker.CurrentVersion} başlatıldı · {Environment.OSVersion}");
        Motion.InstallPressFeedback();
        base.OnStartup(e);
        _startupFiles = e.Args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal) &&
            (System.IO.File.Exists(arg) || System.IO.Directory.Exists(arg))).ToArray();
        if (SelfTest.Active) RunSelfTest();
        else ShowMainWindow();
    }

    /// <summary>Walks through the screens off screen with a demo list, writes the report and exits (see <see cref="SelfTest"/>).</summary>
    private async void RunSelfTest()
    {
        try
        {
            var main = new MainWindow { ShowActivated = false, ShowInTaskbar = false };
            MainWindow = main;
            main.Show();
            main.Left = -30000; // rendered and animated like any window, just not in the user's way
            await main.RunSelfTestAsync(SelfTest.WriteDemoList(System.IO.Path.Combine(AppPaths.Root, "demo")));
        }
        catch (Exception exception)
        {
            SelfTest.Fail("öz denetim başlatılamadı", exception);
        }
        SelfTest.WriteReport();
        AppLog.Info($"Öz denetim bitti: {SelfTest.FailureCount} hata.");
        Shutdown(SelfTest.FailureCount == 0 ? 0 : 1);
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
        // "Birlikte aç" / a file given on the command line opens once the window is up.
        main.OpenWhenShown(_startupFiles);
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
        if (SelfTest.Active)
        {
            SelfTest.Fail("arayüzde işlenmeyen hata", e.Exception); // no dialog: nobody is there to close it
            return;
        }
        if (_reportedCrash) return;
        _reportedCrash = true;
        MessageBox.Show(
            $"Beklenmeyen bir hata oluştu. Uygulama çalışmaya devam edecek.\n\nAyrıntılar günlük dosyasına yazıldı:\n{AppLog.Folder}",
            "Zaplio", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
