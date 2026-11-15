using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

public partial class MainWindow
{
    private long _nextAmbientSampleAt;

    private bool CanShowAmbientLight => _state.Settings.AmbientLight && !_closing && IsVisible &&
        WindowState != WindowState.Minimized && !Immersive && !IsSecondScreenActive &&
        MainVideoSlot.Visibility == Visibility.Visible && VideoView.Visibility == Visibility.Visible;

    private void SetAmbientLightEnabled(bool enabled)
    {
        var previous = _state.Settings;
        _state.Settings = previous with { AmbientLight = enabled };
        try { SaveState(); }
        catch (SourceException exception)
        {
            _state.Settings = previous;
            SetFooter(exception.Message, isError: true);
        }
        UpdateAmbientLightVisibility();
    }

    private void UpdateAmbientLightVisibility()
    {
        if (CanShowAmbientLight || AmbientLightPanel.Visibility == Visibility.Collapsed) return;
        AmbientLightPanel.Visibility = Visibility.Collapsed;
        SetAmbientColor(AmbientLeft, Colors.Transparent, animate: false);
        SetAmbientColor(AmbientRight, Colors.Transparent, animate: false);
        _nextAmbientSampleAt = 0;
    }

    private async Task UpdateAmbientLightAsync(long now)
    {
        UpdateAmbientLightVisibility();
        if (!CanShowAmbientLight || now < _nextAmbientSampleAt || _player is not { IsPlaying: true } player ||
            !_playbackLock.Wait(0)) return;

        var revision = _playbackRevision;
        _nextAmbientSampleAt = now + 2_000;
        try
        {
            var palette = await Task.Run(() => CaptureAmbientPalette(player));
            if (!CanShowAmbientLight || revision != _playbackRevision || palette is not { } colors) return;
            AmbientLightPanel.Visibility = Visibility.Visible;
            var animate = SystemParameters.ClientAreaAnimation;
            SetAmbientColor(AmbientLeft, colors.Left, animate);
            SetAmbientColor(AmbientRight, colors.Right, animate);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private static AmbientPalette? CaptureAmbientPalette(VlcPlayer player)
    {
        if (player.VoutCount == 0) return null;
        var path = Path.Combine(Path.GetTempPath(), $"zaplio-light-{Guid.NewGuid():N}.png");
        try
        {
            if (!player.TakeSnapshot(0, path, 64, 36) || !File.Exists(path)) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[bitmap.PixelHeight * stride];
            bitmap.CopyPixels(pixels, stride, 0);
            return AmbientPalette.FromBgra(pixels, bitmap.PixelWidth, bitmap.PixelHeight);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FileFormatException or NotSupportedException)
        {
            return null;
        }
        finally
        {
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    private static void SetAmbientColor(GradientStop stop, Color target, bool animate)
    {
        var current = stop.Color;
        stop.Color = target;
        stop.BeginAnimation(GradientStop.ColorProperty, animate && current != target
            ? new ColorAnimation(current, target, TimeSpan.FromMilliseconds(650)) { FillBehavior = FillBehavior.Stop }
            : null);
    }
}
