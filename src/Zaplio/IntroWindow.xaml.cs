using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Zaplio;

/// <summary>
/// The animated Zaplio intro (~2.8 s). It ends on its own, on a click or key press, or after a safety timeout, and
/// falls back to the still last frame when Windows asks for fewer animations or the video cannot be decoded.
/// </summary>
public partial class IntroWindow : Window
{
    private static readonly TimeSpan StillDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(5);
    private readonly DispatcherTimer _timer = new();
    private bool _finished;

    public IntroWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Finish();
        Loaded += (_, _) => Start();
        Closed += (_, _) =>
        {
            IsClosed = true;
            Finish(); // Alt+F4 also just skips to the app
        };
    }

    /// <summary>Raised once, when the main window should take over.</summary>
    internal event Action? Finished;

    internal bool IsClosed { get; private set; }

    internal static string VideoPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Intro.mp4");

    private void Start()
    {
        // Windows "Animation effects" off → respect it and show the final frame only.
        if (!SystemParameters.ClientAreaAnimation || !File.Exists(VideoPath))
        {
            ShowStill();
            return;
        }
        Video.Source = new Uri(VideoPath);
        Video.Play();
        _timer.Interval = SafetyTimeout;
        _timer.Start();
    }

    private void ShowStill()
    {
        Video.Visibility = Visibility.Collapsed;
        StillFrame.Visibility = Visibility.Visible;
        _timer.Stop();
        _timer.Interval = StillDuration;
        _timer.Start();
    }

    private void OnMediaEnded(object sender, RoutedEventArgs e) => Finish();

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        AppLog.Warn("Açılış animasyonu oynatılamadı; son kare gösteriliyor.", e.ErrorException);
        ShowStill();
    }

    private void OnSkip(object sender, MouseButtonEventArgs e) => Finish();

    private void OnKey(object sender, KeyEventArgs e) => Finish();

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        _timer.Stop();
        Video.Stop();
        Finished?.Invoke();
    }
}
