using System.Globalization;
using System.Text.Json;

namespace Zaplio;

internal enum LibrarySection { Live, Movies, Series }

internal sealed record VodItem(long ProviderId, string Title, string Category, string Detail, Channel? Playable,
    int Season = 0, int Episode = 0, string? Poster = null, string? Plot = null, int SeasonCount = 0)
{
    public override string ToString() => Title;
}

internal sealed partial class CatalogLoader
{
    public async Task<IReadOnlyList<VodItem>> LoadVodAsync(Source source, LibrarySection section, CancellationToken token)
    {
        var connection = XtreamAddress(source);
        var api = $"{connection.Root}/player_api.php?username={connection.Username}&password={connection.Password}";
        var series = section == LibrarySection.Series;
        var groups = LoadGroupsAsync($"{api}&action={(series ? "get_series_categories" : "get_vod_categories")}", token);
        var items = ReadJsonAsync($"{api}&action={(series ? "get_series" : "get_vod_streams")}", token);
        try
        {
            await Task.WhenAll(groups, items).ConfigureAwait(false);
            var root = items.Result.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                throw new SourceException("Xtream sunucusu geçerli bir içerik listesi göndermedi.");

            var result = new List<VodItem>();
            foreach (var item in root.EnumerateArray())
            {
                if (!PositiveNumber(Value(item, series ? "series_id" : "stream_id"), out var id)) continue;
                var title = Value(item, "name")?.Trim();
                if (string.IsNullOrEmpty(title)) continue;
                var category = groups.Result.GetValueOrDefault(Value(item, "category_id") ?? "") ?? "Genel";
                var detail = Value(item, "year") ?? Value(item, "releaseDate") ?? "";
                if (detail.Length > 4 && int.TryParse(detail.AsSpan(0, 4), out _)) detail = detail[..4];
                var playable = series ? null : VodMedia(source, connection, "movie", id, title, category, Value(item, "container_extension"));
                result.Add(new VodItem(id, title, category, detail, playable,
                    Poster: Value(item, series ? "cover" : "stream_icon"), Plot: Value(item, "plot")));
            }
            return result.DistinctBy(item => item.ProviderId).ToList();
        }
        finally
        {
            if (items.IsCompletedSuccessfully) items.Result.Dispose();
        }
    }

    public async Task<IReadOnlyList<VodItem>> LoadEpisodesAsync(Source source, VodItem series, CancellationToken token)
    {
        var connection = XtreamAddress(source);
        var seriesId = series.ProviderId.ToString(CultureInfo.InvariantCulture);
        var api = $"{connection.Root}/player_api.php?username={connection.Username}&password={connection.Password}";
        using var response = await ReadJsonAsync($"{api}&action=get_series_info&series_id={seriesId}", token).ConfigureAwait(false);
        if (response.RootElement.ValueKind != JsonValueKind.Object ||
            !response.RootElement.TryGetProperty("episodes", out var episodes))
            throw new SourceException("Bu dizi için bölüm listesi alınamadı.");

        var result = new List<VodItem>();
        if (episodes.ValueKind == JsonValueKind.Object)
        {
            foreach (var season in episodes.EnumerateObject()) ReadSeason(season.Value, Number(season.Name));
        }
        else if (episodes.ValueKind == JsonValueKind.Array)
        {
            var season = 0;
            foreach (var entry in episodes.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Array) ReadSeason(entry, season);
                else ReadEpisode(entry, 0);
                season++;
            }
        }
        else if (episodes.ValueKind != JsonValueKind.Null)
        {
            throw new SourceException("Xtream sunucusu geçerli bir bölüm listesi göndermedi.");
        }

        return result.DistinctBy(item => item.ProviderId).OrderBy(item => item.Season).ThenBy(item => item.Episode).ToList();

        void ReadSeason(JsonElement entries, int season)
        {
            if (entries.ValueKind != JsonValueKind.Array) return;
            foreach (var entry in entries.EnumerateArray()) ReadEpisode(entry, season);
        }

        void ReadEpisode(JsonElement entry, int season)
        {
            if (!PositiveNumber(Value(entry, "id"), out var id)) return;
            season = Value(entry, "season") is string number ? Number(number) : season;
            var episode = Number(Value(entry, "episode_num"));
            var title = Value(entry, "title")?.Trim();
            if (string.IsNullOrEmpty(title)) title = episode > 0 ? $"Bölüm {episode}" : "Bölüm";
            var category = season > 0 ? $"{season}. Sezon" : "Özel bölümler";
            var detail = episode > 0 ? $"{episode}. Bölüm" : category;
            var media = VodMedia(source, connection, "series", id, $"{series.Title} · {title}",
                $"{category} · {detail}", Value(entry, "container_extension")) with { SeriesId = series.ProviderId, EpisodeId = id };
            result.Add(new VodItem(id, title, category, detail, media, season, episode));
        }
    }

    public async Task<VodItem> LoadVodDetailsAsync(Source source, VodItem item, CancellationToken token)
    {
        var (root, username, password) = XtreamAddress(source);
        var movie = item.Playable is not null;
        var id = item.ProviderId.ToString(CultureInfo.InvariantCulture);
        var action = movie ? $"get_vod_info&vod_id={id}" : $"get_series_info&series_id={id}";
        using var response = await ReadJsonAsync($"{root}/player_api.php?username={username}&password={password}&action={action}", token)
            .ConfigureAwait(false);
        if (response.RootElement.ValueKind != JsonValueKind.Object || !response.RootElement.TryGetProperty("info", out var info))
            return item;
        return item with
        {
            Poster = Value(info, "cover_big") ?? Value(info, "movie_image") ?? Value(info, "cover") ?? item.Poster,
            Plot = Value(info, "plot") ?? Value(info, "description") ?? item.Plot,
            Detail = Value(info, "year") ?? Value(info, "release_date") ?? Value(info, "releaseDate") ?? item.Detail,
            SeasonCount = response.RootElement.TryGetProperty("seasons", out var seasons) && seasons.ValueKind == JsonValueKind.Array
                ? seasons.GetArrayLength() : item.SeasonCount
        };
    }

    internal static Channel RebindVod(Source source, Channel previous)
    {
        var route = previous.Stream.Segments.TakeLast(4).First().Trim('/');
        var file = previous.Stream.Segments[^1];
        var parts = file.Split('.', 2);
        if (route is not ("movie" or "series") || !PositiveNumber(parts[0], out var id)) return previous;
        return VodMedia(source, XtreamAddress(source), route, id, previous.Name, previous.Group,
            parts.Length == 2 ? parts[1] : null) with { SeriesId = previous.SeriesId, EpisodeId = previous.EpisodeId };
    }

    private static Channel VodMedia(Source source, (string Root, string Username, string Password) connection,
        string route, long id, string title, string category, string? container)
    {
        var extension = container?.Trim().TrimStart('.').ToLowerInvariant() ?? "";
        if (extension.Length is 0 or > 12 || !extension.All(char.IsAsciiLetterOrDigit)) extension = "mp4";
        var number = id.ToString(CultureInfo.InvariantCulture);
        var stream = new Uri($"{connection.Root}/{route}/{connection.Username}/{connection.Password}/{number}.{extension}");
        return new Channel(Id(source, $"{route}:{number}"), source.Id, title, category, stream, IsOnDemand: true);
    }

    private static bool PositiveNumber(string? value, out long number) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) && number > 0;

    private static int Number(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
