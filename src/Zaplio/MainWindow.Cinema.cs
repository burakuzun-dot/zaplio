using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Zaplio;

/// <summary>
/// "Sinema modu": the lights go down. Everything around the video fades almost to black so only the picture and the ambient
/// light remain; moving the mouse over a dimmed part brings it back while you use it. C, Esc or the bulb button turns the
/// lights on again, and they come up slowly by themselves when playback ends.
/// </summary>
public partial class MainWindow
{
    internal const double CinemaDim = 0.05;
    private const double CinemaPeek = 0.9;

    private bool _cinema;
    private bool _cinemaHooked;

    internal bool IsCinemaMode => _cinema;

    /// <summary>The parts that dim; the video frame itself never does.</summary>
    private FrameworkElement[] CinemaParts => [TitleBar, NavigationPane, ChannelPane, VodPane, PlayerHeader, PlayerControls, Footer, VodInfoPanel, GuidePane];

    private void OnCinema(object sender, RoutedEventArgs e) => SetCinema(!_cinema);

    /// <param name="slowly">The lights come up over two seconds, as at the end of a film.</param>
    internal void SetCinema(bool on, bool slowly = false)
    {
        if (on == _cinema) return;
        _cinema = on;
        HookCinemaPeek();
        var duration = TimeSpan.FromMilliseconds(on ? 900 : slowly ? 2000 : 450);
        foreach (var part in CinemaParts) Fade(part, on ? CinemaDim : 1, duration);
        Fade(CinemaShade, on ? 1 : 0, duration);
        Fade(AmbientLightPanel, on ? 0.95 : 0.6, duration); // the glow is the only light left in the room
        CinemaShade.Visibility = Visibility.Visible;
        CinemaIcon.Text = on ? IconGlyphs.LightbulbOn : IconGlyphs.Lightbulb;
        CinemaButton.ToolTip = on ? "Işıkları aç (C)" : "Sinema modu: çevreyi karart (C)";
        System.Windows.Automation.AutomationProperties.SetName(CinemaButton, on ? "Işıkları aç" : "Sinema modu");
        if (on) ShowOsd("Sinema modu", "Işıkları açmak için C ya da Esc");
    }

    private static void Fade(UIElement element, double to, TimeSpan duration)
    {
        if (!Motion.Enabled)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = to;
            return;
        }
        var animation = new DoubleAnimation(to, duration) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        if (to >= 1)
        {
            // When fully back, drop the animation so the element's own opacity is free again.
            animation.Completed += (_, _) =>
            {
                if (element.Opacity >= 0.999) return;
                element.BeginAnimation(OpacityProperty, null);
                element.Opacity = 1;
            };
        }
        element.BeginAnimation(OpacityProperty, animation);
    }

    /// <summary>While the lights are down, a dimmed part lights up under the mouse and fades again when the mouse leaves.</summary>
    private void HookCinemaPeek()
    {
        if (_cinemaHooked) return;
        _cinemaHooked = true;
        foreach (var part in CinemaParts)
        {
            part.MouseEnter += (_, _) => { if (_cinema) Fade(part, CinemaPeek, TimeSpan.FromMilliseconds(180)); };
            part.MouseLeave += (_, _) => { if (_cinema && !part.IsKeyboardFocusWithin) Fade(part, CinemaDim, TimeSpan.FromMilliseconds(700)); };
        }
    }

    /// <summary>Called when playback stops: the film is over, so the lights come up slowly.</summary>
    private void EndCinemaAfterPlayback()
    {
        if (_cinema) SetCinema(false, slowly: true);
    }

    private bool HandleCinemaKey(KeyEventArgs e)
    {
        if (!_cinema || e.Key != Key.Escape || _fullscreen) return false;
        SetCinema(false);
        return true;
    }
}
