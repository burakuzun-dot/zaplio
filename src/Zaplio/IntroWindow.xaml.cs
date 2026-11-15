using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Zaplio;

/// <summary>
/// Plays the transparent intro, with the legacy video and still available as fallbacks.
/// </summary>
public partial class IntroWindow : Window
{
    private static readonly TimeSpan StillDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(5);
    private readonly DispatcherTimer _timer = new();
    private readonly bool _transparentIntro;
    private readonly Stopwatch _clock = new();
    private BitmapSource[]? _frames;
    private bool _finished;

    public IntroWindow()
    {
        InitializeComponent();
        _transparentIntro = File.Exists(IntroFrameSequence.PathOnDisk);
        if (_transparentIntro)
        {
            AllowsTransparency = true;
            Background = Brushes.Transparent;
        }
        _timer.Tick += OnTick;
        Loaded += async (_, _) => await StartAsync();
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

    private async Task StartAsync()
    {
        var animate = SystemParameters.ClientAreaAnimation;
        if (_transparentIntro)
        {
            _timer.Interval = SafetyTimeout;
            _timer.Start();
            try
            {
                var frames = await Task.Run(() => IntroFrameSequence.Load(IntroFrameSequence.PathOnDisk, animate));
                if (_finished) return;

                _timer.Stop();
                Video.Visibility = Visibility.Collapsed;
                TransparentFrame.Source = frames[0];
                TransparentFrame.Visibility = Visibility.Visible;
                if (animate)
                {
                    _frames = frames;
                    _clock.Start();
                }
                _timer.Interval = animate ? TimeSpan.FromMilliseconds(1000d / 60) : StillDuration;
                _timer.Start();
                return;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or FileFormatException or
                NotSupportedException or ArgumentException or InvalidOperationException)
            {
                if (_finished) return;
                _timer.Stop();
                AppLog.Warn("Şeffaf açılış animasyonu yüklenemedi.", exception);
                SetResourceReference(BackgroundProperty, "Canvas");
            }
        }

        // Windows "Animation effects" off → respect it and show the final frame only.
        if (!animate || !File.Exists(VideoPath))
        {
            ShowStill();
            return;
        }
        Video.Source = new Uri(VideoPath);
        Video.Play();
        _timer.Interval = SafetyTimeout;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_frames is not { } frames)
        {
            Finish();
            return;
        }

        var index = (int)(_clock.Elapsed.TotalSeconds * IntroFrameSequence.FramesPerSecond);
        if (index >= frames.Length) Finish();
        else TransparentFrame.Source = frames[index];
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
        _frames = null;
        if (Video.Source is not null) Video.Stop();
        Finished?.Invoke();
    }
}
