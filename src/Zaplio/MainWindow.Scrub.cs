using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Zaplio;

/// <summary>
/// Scrubbing the timeline of a movie, an episode or a file from this computer: while you drag, the video pauses and follows
/// the thumb (a local file almost frame by frame, a provider's stream every half second), a bubble above the thumb shows the
/// time, and playback resumes where you let go. The wheel over the timeline and Shift + ←/→ step by single seconds.
/// </summary>
public partial class MainWindow
{
    private readonly ScrubThrottle _scrub = new();
    private readonly SmoothPlaybackClock _smoothClock = new();
    private bool _resumeAfterScrub;
    private Popup? _scrubBubble;
    private TextBlock? _scrubText;
    private DispatcherTimer? _scrubBubbleHide;

    internal bool IsScrubBubbleOpen => _scrubBubble?.IsOpen == true;

    internal int SeekPresses { get; private set; }

    /// <summary>The timeline's mouse handlers, added in code with handledEventsToo (see <see cref="OnVodSeekStart"/>).</summary>
    private void HookTimeline()
    {
        VodSeek.AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnVodSeekStart), handledEventsToo: true);
        VodSeek.AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnVodSeekEnd), handledEventsToo: true);
    }

    private bool CanScrub => _player is { IsSeekable: true, Length: > 0 } && _playingChannel?.IsOnDemand == true && VodSeek.IsEnabled;

    /// <summary>
    /// One press anywhere on the timeline starts the drag. WPF's own slider (IsMoveToPointEnabled) jumps to the click and marks
    /// the press handled before any XAML handler sees it, so a drag never started and a second press on the thumb was needed.
    /// The timeline now does the jump itself, holds the mouse until release, and its handlers run even for handled presses.
    /// </summary>
    private void OnVodSeekStart(object sender, MouseButtonEventArgs e)
    {
        SeekPresses++;
        if (!VodSeek.IsEnabled || _player is not { } player || _playingChannel is not { } channel) return;
        e.Handled = true; // the slider's own click handling would release the mouse after one move
        _seekingVod = true;
        _scrub.Begin(LocalMedia.IsLocal(channel), Environment.TickCount64);
        _resumeAfterScrub = player.IsPlaying;
        if (_resumeAfterScrub) player.SetPause(true); // a paused picture follows the thumb without stuttering sound
        VodSeek.CaptureMouse();
        FollowPointer(e.GetPosition(VodSeek).X);
    }

    /// <summary>Moves the timeline (and, throttled, the picture) to the time under the pointer.</summary>
    private void FollowPointer(double x) => VodSeek.Value = SeekTimeAt(x, VodSeek.ActualWidth, VodSeek.Maximum);

    private void OnVodSeekEnd(object sender, MouseEventArgs e)
    {
        if (!_seekingVod) return;
        _seekingVod = false;
        if (VodSeek.IsMouseCaptured) VodSeek.ReleaseMouseCapture(); // raises LostMouseCapture, which finds _seekingVod false
        if (e is MouseButtonEventArgs) e.Handled = true;
        SeekVod();
        if (_resumeAfterScrub && _player is { } player) player.SetPause(false);
        _resumeAfterScrub = false;
        HideScrubBubble(after: 500);
    }

    private void OnVodSeekChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingVodSeek || !_isReady || !VodSeek.IsEnabled) return;
        var target = (long)e.NewValue;
        VodElapsed.Text = PlaybackTime.Format(target);
        if (!_seekingVod)
        {
            SeekVod();
            return;
        }
        ShowScrubBubble(target);
        if (_player is { IsSeekable: true } player && _scrub.ShouldSeek(target, Environment.TickCount64))
        {
            MarkSeek();
            player.Time = target; // the live preview: the picture jumps with the thumb
        }
    }

    private void SeekVod()
    {
        if (_player is not { IsSeekable: true } player || _playingChannel?.IsOnDemand != true) return;
        _pendingVodPosition = 0;
        MarkSeek();
        player.Time = (long)VodSeek.Value;
    }

    /// <summary>The wheel over the timeline: five seconds a notch, one second with Shift.</summary>
    private void OnVodSeekWheel(object sender, MouseWheelEventArgs e)
    {
        if (!CanScrub) return;
        e.Handled = true;
        var step = Keyboard.Modifiers == ModifierKeys.Shift ? 1000 : 5000;
        JumpVod(Math.Sign(e.Delta) * step);
    }

    /// <summary>Moves playback by <paramref name="delta"/> milliseconds and shows where it landed above the timeline.</summary>
    private void JumpVod(long delta)
    {
        if (!CanScrub || _player is not { } player) return;
        _pendingVodPosition = 0;
        MarkSeek();
        var target = Math.Clamp(player.Time + delta, 0, player.Length);
        player.Time = target;
        _updatingVodSeek = true;
        VodSeek.Value = target;
        _updatingVodSeek = false;
        VodElapsed.Text = PlaybackTime.Format(target);
        ShowScrubBubble(target);
        HideScrubBubble(after: 900);
    }

    private void ShowScrubBubble(long time)
    {
        _scrubBubbleHide?.Stop();
        if (_scrubBubble is null)
        {
            _scrubText = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("Text") };
            _scrubBubble = new Popup
            {
                AllowsTransparency = true, Placement = PlacementMode.Relative, PlacementTarget = VodSeek, IsHitTestVisible = false,
                Child = new Border
                {
                    Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("ZpBoltBrush"), BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9), Padding = new Thickness(9, 4, 9, 4), Child = _scrubText
                }
            };
        }
        _scrubText!.Text = PlaybackTime.Format(time);
        var bubble = (FrameworkElement)_scrubBubble.Child;
        bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        _scrubBubble.HorizontalOffset = SeekX(time) - bubble.DesiredSize.Width / 2;
        _scrubBubble.VerticalOffset = -bubble.DesiredSize.Height - 10;
        _scrubBubble.IsOpen = true;
    }

    private const double SeekThumbWidth = 14; // SeekSlider's thumb; the track maps values between its half-widths

    /// <summary>Where a time sits on the timeline, as the slider's track places it.</summary>
    private double SeekX(double time) => SeekXFor(time, VodSeek.ActualWidth, VodSeek.Maximum);

    internal static double SeekXFor(double time, double width, double maximum)
    {
        var ratio = maximum > 0 ? Math.Clamp(time / maximum, 0, 1) : 0;
        return SeekThumbWidth / 2 + ratio * Math.Max(0, width - SeekThumbWidth);
    }

    /// <summary>The time under the mouse on the timeline (the inverse of <see cref="SeekXFor"/>).</summary>
    internal static double SeekTimeAt(double x, double width, double maximum)
    {
        var usable = Math.Max(1, width - SeekThumbWidth);
        return Math.Clamp((x - SeekThumbWidth / 2) / usable, 0, 1) * maximum;
    }

    /// <summary>Hovering the timeline shows the time under the mouse, before you click.</summary>
    private void OnVodSeekHover(object sender, MouseEventArgs e)
    {
        if (_seekingVod)
        {
            if (VodSeek.IsMouseCaptured) FollowPointer(e.GetPosition(VodSeek).X); // dragging, even outside the bar or the window
            return;
        }
        if (!CanScrub) return;
        ShowScrubBubble((long)SeekTimeAt(e.GetPosition(VodSeek).X, VodSeek.ActualWidth, VodSeek.Maximum));
    }

    private void OnVodSeekLeave(object sender, MouseEventArgs e)
    {
        if (!_seekingVod) HideScrubBubble(after: 1);
    }

    private void HideScrubBubble(int after)
    {
        if (_scrubBubble is null) return;
        _scrubBubbleHide ??= new DispatcherTimer();
        _scrubBubbleHide.Stop();
        _scrubBubbleHide.Interval = TimeSpan.FromMilliseconds(after);
        _scrubBubbleHide.Tick -= OnScrubBubbleTimeout;
        _scrubBubbleHide.Tick += OnScrubBubbleTimeout;
        _scrubBubbleHide.Start();
    }

    private void OnScrubBubbleTimeout(object? sender, EventArgs e)
    {
        _scrubBubbleHide?.Stop();
        if (!_seekingVod && _scrubBubble is not null) _scrubBubble.IsOpen = false;
    }
}
