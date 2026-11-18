using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Zaplio;

public partial class MainWindow
{
    private readonly DispatcherTimer _numberEntryTimer = new() { Interval = TimeSpan.FromMilliseconds(1300) };
    private readonly DispatcherTimer _osdTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    // Opening a stream per key press while zapping quickly makes providers count (and block) many connections,
    // so only the channel the user stops on is opened.
    private readonly DispatcherTimer _zapDelay = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private Channel? _zapTarget;
    private string _numberEntry = "";
    private Channel? _previousLiveChannel;

    private List<Channel> VisibleChannels => ChannelList.ItemsSource as List<Channel> ?? [];

    private void InitializeZapping()
    {
        _numberEntryTimer.Tick += async (_, _) => await CommitNumberEntryAsync();
        _zapDelay.Tick += async (_, _) =>
        {
            _zapDelay.Stop();
            if (_zapTarget is { } target && !_closing) await PlayChannelAsync(target);
            _zapTarget = null;
        };
        _osdTimer.Tick += (_, _) =>
        {
            _osdTimer.Stop();
            ChannelOsd.Visibility = Visibility.Collapsed;
        };
    }

    private void RememberPreviousChannel(Channel next)
    {
        if (next.IsOnDemand) return;
        var current = _playingChannel ?? _lastChannel;
        if (current is { IsOnDemand: false } && current.Id != next.Id && current.SourceId == next.SourceId)
            _previousLiveChannel = current;
    }

    private static bool IsTextInputFocused() => Keyboard.FocusedElement is
        System.Windows.Controls.Primitives.TextBoxBase or PasswordBox or ComboBox;

    /// <summary>Handles live-TV zapping keys. Returns true when the key was consumed.</summary>
    private bool HandleZappingKey(KeyEventArgs e)
    {
        if (_librarySection != LibrarySection.Live || IsTextInputFocused()) return false;
        var modifiers = Keyboard.Modifiers;

        if ((modifiers == ModifierKeys.Control && e.Key is Key.Up or Key.Down) ||
            (modifiers == ModifierKeys.None && e.Key is Key.PageUp or Key.PageDown && (_fullscreen || _pip)))
        {
            _ = ZapAsync(e.Key is Key.Up or Key.PageUp ? -1 : 1);
            return true;
        }

        if (modifiers == ModifierKeys.None && e.Key == Key.Back && _previousLiveChannel is { } previous)
        {
            _ = ZapToAsync(previous);
            return true;
        }

        if (modifiers == ModifierKeys.None && DigitOf(e.Key) is int digit)
        {
            if (_numberEntry.Length >= 5) _numberEntry = "";
            _numberEntry += digit.ToString(CultureInfo.InvariantCulture);
            ShowOsd(_numberEntry + "_", Lang.T("Kanal numarası"), sticky: true);
            _numberEntryTimer.Stop();
            _numberEntryTimer.Start();
            return true;
        }

        if (modifiers == ModifierKeys.None && e.Key == Key.Enter && _numberEntry.Length > 0)
        {
            _ = CommitNumberEntryAsync();
            return true;
        }
        return false;
    }

    private static int? DigitOf(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => key - Key.D0,
        >= Key.NumPad0 and <= Key.NumPad9 => key - Key.NumPad0,
        _ => null
    };

    private async Task CommitNumberEntryAsync()
    {
        _numberEntryTimer.Stop();
        var entry = _numberEntry;
        _numberEntry = "";
        if (!int.TryParse(entry, NumberStyles.None, CultureInfo.InvariantCulture, out var number)) return;
        var channels = VisibleChannels;
        if (number < 1 || number > channels.Count)
        {
            ShowOsd(entry, Lang.T("Bu numarada kanal yok"));
            return;
        }
        await ZapToAsync(channels[number - 1]);
    }

    private async Task ZapAsync(int step)
    {
        var channels = VisibleChannels;
        if (channels.Count == 0) return;
        var anchor = _zapTarget ?? _playingChannel ?? _cast?.Channel ?? ChannelList.SelectedItem as Channel; // on the TV too
        var anchorId = RowId(anchor?.Id);
        var index = anchorId is null ? -1 : channels.FindIndex(channel => channel.Id == anchorId);
        var next = index < 0
            ? channels[step > 0 ? 0 : channels.Count - 1]
            : channels[((index + step) % channels.Count + channels.Count) % channels.Count];
        await ZapToAsync(next);
    }

    private async Task ZapToAsync(Channel channel)
    {
        if (channel.SourceId != _activeSourceId) return;
        var channels = VisibleChannels;
        var rowId = RowId(channel.Id);
        var index = channels.FindIndex(item => item.Id == rowId);
        if (index >= 0)
        {
            ChannelList.SelectedItem = channels[index];
            ChannelList.ScrollIntoView(channels[index]);
        }
        var now = DateTimeOffset.Now;
        var programme = _guideRequests.GetValueOrDefault(channel.SourceId)?.Guide?.ForChannel(channel)
            .LastOrDefault(item => item.IsOnAir(now))?.Title;
        ShowOsd(index >= 0 ? $"{index + 1}  {channel.Name}" : channel.Name,
            string.IsNullOrWhiteSpace(programme) ? channel.GroupLabel : Lang.F($"Şimdi: {programme}"));
        _zapTarget = channel;
        _zapDelay.Stop();
        _zapDelay.Start();
        await Task.CompletedTask;
    }

    private void ShowOsd(string title, string subtitle, bool sticky = false)
    {
        ChannelOsdTitle.Text = title;
        ChannelOsdSubtitle.Text = subtitle;
        ChannelOsdSubtitle.Visibility = string.IsNullOrEmpty(subtitle) ? Visibility.Collapsed : Visibility.Visible;
        ChannelOsd.Visibility = Visibility.Visible;
        _osdTimer.Stop();
        if (!sticky) _osdTimer.Start();
    }
}
