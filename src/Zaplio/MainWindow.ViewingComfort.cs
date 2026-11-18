using System.Windows.Controls;
using VlcPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace Zaplio;

/// <summary>
/// Seçenekler › Altyazı görünümü, Görüntü and Sesi dengele. Subtitle look and levelling are engine options (the stream
/// reopens at the same place); the picture is adjusted on the running player at once. All are remembered.
/// </summary>
public partial class MainWindow
{
    private MenuItem BuildSubtitleLook()
    {
        var group = new MenuItem { Header = Lang.F($"Altyazı görünümü: {VlcOptions.SubtitleLabel(_state.Settings.SubtitleSize)}") };
        foreach (var size in Enum.GetValues<SubtitleSize>())
        {
            var item = new MenuItem { Header = VlcOptions.SubtitleLabel(size), IsCheckable = true, IsChecked = size == _state.Settings.SubtitleSize };
            item.Click += async (_, _) => await ChangeEngineSettingAsync(_state.Settings with { SubtitleSize = size });
            group.Items.Add(item);
        }
        group.Items.Add(new Separator());
        var box = new MenuItem { Header = Lang.T("Arkasına koyu kutu"), IsCheckable = true, IsChecked = _state.Settings.SubtitleBackground };
        box.Click += async (_, _) => await ChangeEngineSettingAsync(_state.Settings with { SubtitleBackground = box.IsChecked });
        group.Items.Add(box);
        return group;
    }

    private MenuItem BuildPicture(VlcPlayer player)
    {
        var picture = _state.Settings.Picture;
        var group = new MenuItem { Header = picture.IsNeutral ? Lang.T("Görüntü") : Lang.T("Görüntü (ayarlı)") };
        group.Items.Add(Ladder(Lang.T("Parlaklık"), picture.Brightness, step => picture with { Brightness = step }));
        group.Items.Add(Ladder(Lang.T("Kontrast"), picture.Contrast, step => picture with { Contrast = step }));
        group.Items.Add(Ladder(Lang.T("Renk"), picture.Saturation, step => picture with { Saturation = step }));
        group.Items.Add(new Separator());
        var reset = new MenuItem { Header = Lang.T("Özgün görüntü"), IsEnabled = !picture.IsNeutral };
        reset.Click += (_, _) => SetPicture(player, PictureAdjust.Neutral);
        group.Items.Add(reset);
        return group;

        MenuItem Ladder(string label, int current, Func<int, PictureAdjust> with)
        {
            var ladder = new MenuItem { Header = $"{label}: {PictureAdjust.Label(current)}" };
            for (var step = PictureAdjust.Steps; step >= -PictureAdjust.Steps; step--)
            {
                var value = step;
                var item = new MenuItem { Header = PictureAdjust.Label(value), IsCheckable = true, IsChecked = value == current };
                item.Click += (_, _) => SetPicture(player, with(value));
                ladder.Items.Add(item);
            }
            return ladder;
        }
    }

    private void SetPicture(VlcPlayer player, PictureAdjust picture)
    {
        _state.Settings = _state.Settings with { Picture = picture };
        _volumeDirty = true; // saved on close with the volume
        if (ReferenceEquals(player, _player)) ApplyPicture(player);
        ShowOsd(Lang.T("Görüntü"), picture.IsNeutral ? Lang.T("Özgün görüntü")
            : Lang.F($"Parlaklık {PictureAdjust.Label(picture.Brightness)} · Kontrast {PictureAdjust.Label(picture.Contrast)} · Renk {PictureAdjust.Label(picture.Saturation)}"));
    }

    /// <summary>Saves a subtitle or sound option and reopens what plays with it, at the same place.</summary>
    private async Task ChangeEngineSettingAsync(PlaybackSettings changed)
    {
        var previous = _state.Settings;
        _state.Settings = changed;
        try { SaveState(); }
        catch (SourceException exception)
        {
            _state.Settings = previous;
            SetFooter(exception.Message, isError: true);
            return;
        }
        await ReopenForEngineOptionsAsync();
    }
}
