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
    private AmbientPalette? _ambientShown;

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
        AmbientGlow.Clear();
        _ambientShown = null;
        _nextAmbientSampleAt = 0;
    }

    private async Task UpdateAmbientLightAsync(long now)
    {
        UpdateAmbientLightVisibility();
        if (!CanShowAmbientLight || _player is not { } player) return;
        var animate = SystemParameters.ClientAreaAnimation;
        // Paused: let the light settle down with the picture instead of freezing at full strength.
        AmbientGlow.SetIntensity(player.IsPlaying ? 1 : 0.25, animate);
        if (!player.IsPlaying || now < _nextAmbientSampleAt || !_playbackLock.Wait(0)) return;

        var revision = _playbackRevision;
        _nextAmbientSampleAt = now + 900;
        try
        {
            var palette = await Task.Run(() => CaptureAmbientPalette(player));
            if (!CanShowAmbientLight || revision != _playbackRevision || palette is not { } colors) return;
            AmbientLightPanel.Visibility = Visibility.Visible;
            // Scene cuts: move only part of the way per sample so the light fades instead of jumping.
            var shown = _ambientShown is { } previous ? AmbientPalette.Blend(previous, colors, 0.6) : colors;
            _ambientShown = shown;
            AmbientGlow.Show(shown, animate);
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
}
