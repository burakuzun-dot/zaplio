namespace Zaplio;

public partial class MainWindow
{
    private const int MaxFailovers = 2; // a refusing server is not hammered with every variant
    private readonly HashSet<string> _failoverTried = new();
    private double _peakDownload;

    /// <summary>
    /// Remembers the fastest download seen. When it crosses a quality step (HD → FHD → 4K) the merged list re-picks
    /// its variants; this happens a handful of times per session at most, not every second.
    /// </summary>
    private void NoteDownloadSpeed(double bitsPerSecond)
    {
        if (bitsPerSecond <= _peakDownload) return;
        var before = ChannelVariants.RankLimit(QualityMode.Auto, _peakDownload);
        _peakDownload = bitsPerSecond;
        if (!_state.Settings.MergeQualities || _state.Settings.QualityMode != QualityMode.Auto ||
            ChannelVariants.RankLimit(QualityMode.Auto, _peakDownload) == before) return;
        _customizationVersion++;
        if (_librarySection == LibrarySection.Live && !_closing) UpdateCatalog(selectedChannel: (ChannelList.SelectedItem as Channel)?.Id);
    }

    /// <summary>
    /// When a live channel cannot be played (refused, dropped for good or timed out), switch to another variant of the same
    /// channel — lower quality first — instead of showing an error.
    /// </summary>
    private async Task<bool> TryFailoverAsync(Channel failed)
    {
        if (_closing || failed.IsOnDemand || !_groupByVariant.TryGetValue(failed.Id, out var group) || !group.HasAlternatives) return false;
        _failoverTried.Add(failed.Id);
        if (_failoverTried.Count > MaxFailovers) return false;
        var current = group.Channels.FirstOrDefault(channel => channel.Id == failed.Id) ?? failed;
        if (ChannelVariants.Fallbacks(group, current).FirstOrDefault(channel => !_failoverTried.Contains(channel.Id)) is not { } next)
            return false;

        var from = ChannelVariants.Quality(current.Name).Label;
        var to = ChannelVariants.Quality(next.Name).Label;
        AppLog.Warn($"Yayın açılamadı, yedeğe geçiliyor: {current.Name} → {next.Name}");
        ShowOsd($"{failed.Name}{(from.Length > 0 ? $" {from}" : "")} açılamadı", $"{(to.Length > 0 ? to : "Yedek")} yayına geçiliyor");
        SetPlaybackStatus("Yedek yayına geçiliyor…");
        _reconnectAttempts = 0;
        _streamWasPlaying = false;
        // Keep the row's title so the user still sees the channel they picked.
        await PlayChannelAsync(next with { Name = failed.Name }, reconnect: true);
        return true;
    }
}
