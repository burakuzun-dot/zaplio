namespace Zaplio;

internal sealed record VodPreview(string Title, string Metadata, string? Summary, string? Poster, string Action)
{
    internal static VodPreview? Create(VodItem? item, VodItem? series, Guid? activeSourceId, LocalState state)
    {
        if (item is null || activeSourceId is null) return null;
        var channel = item.Playable;
        if (channel is not null && channel.SourceId != activeSourceId) return null;
        if (series is not null && channel?.SeriesId != series.ProviderId) return null;
        if (series is null && channel is { SeriesId: > 0 }) return null;

        var position = channel is not null && state.Settings.ResumeOnDemand ? state.WatchPositions.GetValueOrDefault(channel.Id) : 0;
        var action = channel is null ? Lang.T("Bölümleri aç") : position > 0 ? Lang.F($"Devam et · {PlaybackTime.Format(position)}") : "Oynat";
        var seasons = item.SeasonCount > 0 ? Lang.F($"{item.SeasonCount} sezon") : "";
        var metadata = string.Join(" · ", new[] { item.Category, item.Detail, seasons }.Where(text => !string.IsNullOrWhiteSpace(text)).Distinct());
        var poster = string.IsNullOrWhiteSpace(item.Poster) ? series?.Poster : item.Poster;
        return new VodPreview(item.Title, metadata, item.Plot, poster, action);
    }
}
