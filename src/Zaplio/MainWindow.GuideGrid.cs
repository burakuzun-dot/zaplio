using System.Windows;

namespace Zaplio;

/// <summary>Opens the grid programme guide for the channels of the current list and plays what is picked there.</summary>
public partial class MainWindow
{
    private GuideGridWindow? _guideGrid;

    private void OnGuideGrid(object sender, RoutedEventArgs e)
    {
        if (_guideGrid is not null)
        {
            _guideGrid.Activate();
            return;
        }
        if (ActiveSource is not { } source) return;
        var request = GetGuide(source.Id, GuideAddresses(source));
        if (request?.Guide is not { } guide)
        {
            // The inline guide shows the loading state, error or XMLTV setup action where the user is looking.
            if (!_guideOpen) SetGuideOpen(true);
            else UpdateGuide();
            return;
        }
        ShowGuideGrid(guide, source);
    }

    private void ShowGuideGrid(XmlTvGuide guide, Source source)
    {
        var now = DateTimeOffset.Now;
        var window = new GuideGridWindow(ChannelHeading.Text, VisibleChannels, guide.ForChannel,
            channel => VariantsOf(channel).Prepend(channel).Any(variant => CanCatchUp(source, variant, now.AddHours(-1), now)),
            PlayFromGuideGridAsync) { Owner = this };
        window.Closed += (_, _) => _guideGrid = null;
        _guideGrid = window;
        window.Show(); // not modal: the video keeps playing and the main window stays usable
    }

    private async Task PlayFromGuideGridAsync(Channel channel, GuideGrid.Cell? programme)
    {
        if (channel.SourceId != _activeSourceId) return;
        if (_guideOpen) SetGuideOpen(false);
        if (programme is not null && _state.Sources.FirstOrDefault(item => item.Id == channel.SourceId) is { } source)
        {
            var archiveChannel = VariantsOf(channel).Prepend(channel).FirstOrDefault(variant => variant.CatchupDays > 0) ?? channel;
            var archive = CatalogLoader.Catchup(source, archiveChannel with { Name = channel.Name },
                _catalog.GetValueOrDefault(source.Id)?.TimeZone, programme.Start, programme.End, programme.Title);
            if (archive is not null)
            {
                ShowOsd("Arşivden oynatılıyor", $"{programme.Title} · {programme.Start.LocalDateTime.ToString("d MMM HH:mm", Turkish)}");
                await PlayChannelAsync(archive);
                return;
            }
        }
        if (ChannelList.Items.Contains(channel)) ChannelList.SelectedItem = channel;
        await PlayChannelAsync(channel);
    }
}
