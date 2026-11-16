using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LibVLCSharp.Shared;

namespace Zaplio;

/// <summary>
/// The space bar comes first: it pauses and resumes whatever plays, whichever control has the focus (a button, a list,
/// the timeline), in the main window, full screen, the mini window and on the second screen. Only a text box keeps it.
/// A large play / pause sign flashes over the video so the press is felt at once.
/// </summary>
public partial class MainWindow
{
    internal SpaceAction LastSpaceAction { get; private set; }

    private bool HandleSpaceKey(KeyEventArgs e)
    {
        if (e.Key != Key.Space || Keyboard.Modifiers != ModifierKeys.None) return false;
        var typing = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox ||
            Keyboard.FocusedElement is ComboBox { IsDropDownOpen: true } || Keyboard.FocusedElement is ComboBox { IsEditable: true };
        var last = _lastChannel is { } previous && PlayableHere(previous) ? previous : null;
        var action = SpaceKey.Decide(typing, e.IsRepeat, _player is not null, CanPausePlayback, last is not null);
        LastSpaceAction = action;
        switch (action)
        {
            case SpaceAction.PassThrough:
                return false;
            case SpaceAction.TogglePause when _player is { } player:
                var pause = player.State != VLCState.Paused;
                player.SetPause(pause);
                FlashPlayPause(paused: pause);
                break;
            case SpaceAction.ResumeLast when last is not null:
                FlashPlayPause(paused: false);
                _ = PlayChannelAsync(last);
                break;
            case SpaceAction.CannotPause:
                ShowOsd("Bu yayın duraklatılamıyor", "Canlı yayın sağlayıcısı duraklatmaya izin vermiyor.");
                break;
        }
        return true;
    }

    /// <summary>A large sign over the video: ❚❚ when pausing, ▶ when resuming, fading out as it grows.</summary>
    private void FlashPlayPause(bool paused)
    {
        PlayPauseFlashIcon.Text = paused ? IconGlyphs.Pause : IconGlyphs.Play;
        if (!Motion.Enabled)
        {
            ShowOsd(paused ? "Duraklatıldı" : "Devam", "");
            return;
        }
        PlayPauseFlash.Visibility = Visibility.Visible;
        var duration = TimeSpan.FromMilliseconds(520);
        var fade = new DoubleAnimationUsingKeyFrames
        {
            KeyFrames = { new LinearDoubleKeyFrame(0.92, KeyTime.FromPercent(0.15)), new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)) },
            Duration = duration
        };
        fade.Completed += (_, _) => PlayPauseFlash.Visibility = Visibility.Collapsed;
        var grow = new DoubleAnimation(0.82, 1.18, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        PlayPauseFlash.BeginAnimation(OpacityProperty, fade);
        PlayPauseFlashScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        PlayPauseFlashScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }
}
