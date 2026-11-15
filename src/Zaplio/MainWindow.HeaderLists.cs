using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;

namespace Zaplio;

/// <summary>The bolt in the player header (live channels, and movies or series while they play) and "Sonra izle" next to it.</summary>
public partial class MainWindow
{
    private sealed record PlayingVod(string Key, string Title);

    private VodItem? MovieItemFor(Channel channel) =>
        (_vodCatalogs.GetValueOrDefault((channel.SourceId, LibrarySection.Movies)) ?? []).FirstOrDefault(item => item.Playable?.Id == channel.Id);

    /// <summary>Favourite / watch-later key of a movie (null when its list is not loaded).</summary>
    private string? MovieKeyFor(Channel channel) =>
        MovieItemFor(channel) is { } movie ? VodLists.Key(channel.SourceId, LibrarySection.Movies, movie.ProviderId) : null;

    /// <summary>The movie, or the series of the episode, shown in the header while it plays in the movie or series section.</summary>
    private PlayingVod? PlayingVodTarget(Channel? channel)
    {
        if (channel is not { IsOnDemand: true } || _librarySection == LibrarySection.Live) return null;
        if (channel.SeriesId > 0)
        {
            var title = _selectedSeries?.ProviderId == channel.SeriesId ? _selectedSeries.Title
                : (_vodCatalogs.GetValueOrDefault((channel.SourceId, LibrarySection.Series)) ?? [])
                    .FirstOrDefault(item => item.ProviderId == channel.SeriesId)?.Title;
            return new PlayingVod(VodLists.Key(channel.SourceId, LibrarySection.Series, channel.SeriesId), title ?? channel.Name);
        }
        return MovieItemFor(channel) is { } movie
            ? new PlayingVod(VodLists.Key(channel.SourceId, LibrarySection.Movies, movie.ProviderId), movie.Title)
            : null;
    }

    private void UpdateHeaderLists(Channel? channel)
    {
        var vod = PlayingVodTarget(channel);
        var favoriteChannel = _guideOpen ? GuideChannel : channel;
        bool favorite;
        string? name;
        if (vod is not null)
        {
            favorite = _state.VodFavorites.Contains(vod.Key);
            name = vod.Title;
            FavoriteButton.Visibility = Visibility.Visible;
            FavoriteButton.IsEnabled = true;
        }
        else
        {
            favorite = favoriteChannel is not null && IsFavorite(favoriteChannel);
            name = favoriteChannel?.Name;
            FavoriteButton.Visibility = _librarySection == LibrarySection.Live && channel?.IsOnDemand != true ? Visibility.Visible : Visibility.Collapsed;
            FavoriteButton.IsEnabled = favoriteChannel is not null;
        }
        HeaderFavorite.IsChannel = vod is null;
        HeaderFavorite.ChannelName = name ?? "";
        HeaderFavorite.IsFavorite = favorite;
        FavoriteButton.Background = (Brush)FindResource(favorite ? "Active" : "Surface");
        FavoriteButton.BorderBrush = (Brush)FindResource(favorite ? "ActiveLine" : "Line");

        HeaderWatchlistButton.Visibility = vod is null ? Visibility.Collapsed : Visibility.Visible;
        if (vod is null) return;
        var later = _state.VodWatchlist.Contains(vod.Key);
        var whole = channel?.SeriesId > 0 ? "Diziyi " : "";
        var tooltip = later ? $"{whole}izleme listemden çıkar" : $"{whole}sonra izlemek için ekle";
        HeaderWatchlistText.Text = later ? "Listemde" : "Sonra izle";
        HeaderWatchlistButton.Foreground = (Brush)FindResource(later ? "Accent" : "Text");
        HeaderWatchlistButton.Background = (Brush)FindResource(later ? "Active" : "Surface");
        HeaderWatchlistButton.BorderBrush = (Brush)FindResource(later ? "ActiveLine" : "Line");
        HeaderWatchlistButton.ToolTip = tooltip;
        AutomationProperties.SetName(HeaderWatchlistButton, tooltip);
    }

    private async void OnHeaderFavoriteToggled(object? sender, bool added)
    {
        if (PlayingVodTarget(DisplayedChannel) is { } vod)
        {
            await ToggleVodKeyAsync(_state.VodFavorites, vod.Key, vod.Title, "favorilere eklendi.", "favorilerden çıkarıldı.");
            return;
        }
        OnFavorite(this, new RoutedEventArgs());
    }

    private async void OnHeaderWatchlist(object sender, RoutedEventArgs e)
    {
        if (PlayingVodTarget(DisplayedChannel) is { } vod)
            await ToggleVodKeyAsync(_state.VodWatchlist, vod.Key, vod.Title, "izleme listene eklendi.", "izleme listenden çıkarıldı.");
    }

    private Task ToggleVodKeyAsync(HashSet<string> list, string key, string title, string added, string removed)
    {
        var onList = VodLists.Toggle(list, key);
        try
        {
            SaveState();
        }
        catch (SourceException exception)
        {
            VodLists.Toggle(list, key);
            SetFooter(exception.Message, isError: true);
            UpdateSelection();
            return Task.CompletedTask;
        }
        SetFooter($"{title} {(onList ? added : removed)}");
        UpdateVodRows();
        UpdateSelection();
        return Task.CompletedTask;
    }
}
