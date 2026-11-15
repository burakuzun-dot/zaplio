using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace Zaplio;

/// <summary>Previous / play-pause / next buttons in the taskbar preview and the keyboard media keys.</summary>
public partial class MainWindow
{
    private static readonly ImageSource PreviousImage = TaskbarIcon("M6,6 H8 V18 H6 Z M19,6 L9.5,12 L19,18 Z");
    private static readonly ImageSource NextImage = TaskbarIcon("M16,6 H18 V18 H16 Z M5,6 L14.5,12 L5,18 Z");
    private static readonly ImageSource PlayImage = TaskbarIcon("M8,5 L19,12 L8,19 Z");
    private static readonly ImageSource PauseImage = TaskbarIcon("M7,5 H11 V19 H7 Z M13,5 H17 V19 H13 Z");

    private readonly ThumbButtonInfo _taskbarPrevious = new() { ImageSource = PreviousImage, Description = "Önceki kanal", IsEnabled = false };
    private readonly ThumbButtonInfo _taskbarPlay = new() { ImageSource = PlayImage, Description = "Oynat", IsEnabled = false };
    private readonly ThumbButtonInfo _taskbarNext = new() { ImageSource = NextImage, Description = "Sonraki kanal", IsEnabled = false };

    /// <summary>White glyph on a transparent 24×24 square, so every button keeps the same size and centre.</summary>
    private static ImageSource TaskbarIcon(string data)
    {
        var drawing = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 24, 24))),
                new GeometryDrawing(Brushes.White, null, Geometry.Parse(data))
            }
        };
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    private void InitializeTaskbar()
    {
        _taskbarPrevious.Click += async (_, _) => await ZapAsync(-1);
        _taskbarPlay.Click += (_, _) => OnPlayToggle(this, new RoutedEventArgs());
        _taskbarNext.Click += async (_, _) => await ZapAsync(1);
        TaskbarItemInfo = new TaskbarItemInfo { ThumbButtonInfos = { _taskbarPrevious, _taskbarPlay, _taskbarNext } };
    }

    private bool CanZap => _librarySection == LibrarySection.Live && _playingChannel?.IsOnDemand != true && VisibleChannels.Count > 1;

    private void UpdateTaskbarButtons(string playLabel, bool showPlay)
    {
        _taskbarPlay.ImageSource = showPlay ? PlayImage : PauseImage;
        _taskbarPlay.Description = playLabel;
        _taskbarPlay.IsEnabled = PlayButton.IsEnabled;
        _taskbarPrevious.IsEnabled = _taskbarNext.IsEnabled = CanZap;
    }

    /// <summary>Keyboard media keys (while Zaplio is the active window). Returns true when the key was used.</summary>
    private bool HandleMediaKey(Key key)
    {
        switch (key)
        {
            case Key.MediaPlayPause:
                OnPlayToggle(this, new RoutedEventArgs());
                return true;
            case Key.MediaStop:
                if (_playingChannel is not null) _ = StopPlaybackAsync("Durduruldu");
                return true;
            case Key.MediaNextTrack when CanZap:
                _ = ZapAsync(1);
                return true;
            case Key.MediaPreviousTrack when CanZap:
                _ = ZapAsync(-1);
                return true;
            default:
                return false;
        }
    }
}
