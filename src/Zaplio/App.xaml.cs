using System.Windows;
using System.Windows.Threading;

namespace Zaplio;

public partial class App : Application
{
    private bool _reportedCrash;

    private string[] _startupFiles = [];
    private SingleInstance? _instance;
    private readonly CancellationTokenSource _instanceListener = new();

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
        Lang.Hook(); // XAML text is translated as elements load
        base.OnStartup(e);
        if (!SelfTest.Active) ApplySavedLook(); // the self test starts from the default look and switches it itself
        _startupFiles = e.Args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal) &&
            (System.IO.File.Exists(arg) || System.IO.Directory.Exists(arg))).ToArray();
        if (SelfTest.Active)
        {
            RunSelfTest();
            return;
        }

        // One Zaplio per data folder: a second launch hands its files to the open window and leaves.
        _instance = new SingleInstance(AppPaths.Root);
        if (!_instance.IsFirst && _instance.SendToFirst(_startupFiles))
        {
            AppLog.Info("Zaplio zaten açık; dosyalar açık pencereye gönderildi.");
            Shutdown(0);
            return;
        }
        ShowMainWindow();
        if (e.Args.Contains("--updated")) (MainWindow as MainWindow)?.AnnounceUpdated();
        _ = _instance.ListenAsync(paths => Dispatcher.BeginInvoke(() => (MainWindow as MainWindow)?.OpenFromAnotherLaunch(paths)),
            _instanceListener.Token);
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
            SelfTest.Fail(Lang.T("öz denetim başlatılamadı"), exception);
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

    /// <summary>The saved theme and accent, applied before the first window opens.</summary>
    /// <summary>Theme and language come before any window is built (the intro included).</summary>
    private void ApplySavedLook()
    {
        try
        {
            var settings = new LocalStateStore().Load().Settings;
            Lang.Use(settings.Language);
            ThemeManager.Apply(Resources, settings.Theme, settings.Accent);
        }
        catch (Exception exception)
        {
            Lang.Use(AppLanguage.System);
            AppLog.Warn("Tema ve dil ayarı okunamadı; varsayılanlar kullanılıyor.", exception);
        }
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

    /// <summary>Starts a fresh Zaplio and closes this one (after a backup is restored).</summary>
    internal void Restart()
    {
        _instanceListener.Cancel();
        _instance?.Dispose(); // free the single-window lock first, or the new Zaplio would hand itself back to this one
        _instance = null;
        if (Environment.ProcessPath is { } program)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(program) { UseShellExecute = false });
        MainWindow?.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceListener.Cancel();
        _instance?.Dispose();
        AppLog.Info("Zaplio kapatıldı.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("Arayüzde işlenmeyen hata.", e.Exception);
        e.Handled = true;
        if (SelfTest.Active)
        {
            SelfTest.Fail(Lang.T("arayüzde işlenmeyen hata"), e.Exception); // no dialog: nobody is there to close it
            return;
        }
        if (_reportedCrash) return;
        _reportedCrash = true;
        MessageBox.Show(
            $"Beklenmeyen bir hata oluştu. Uygulama çalışmaya devam edecek.\n\nAyrıntılar günlük dosyasına yazıldı:\n{AppLog.Folder}",
            "Zaplio", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
