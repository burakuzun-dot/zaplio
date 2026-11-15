using System.Text.Json;
using Xunit;

namespace Zaplio.Tests;

public sealed class VodListsTests
{
    private static readonly Guid Source = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Movies_and_series_with_the_same_provider_id_do_not_collide()
    {
        Assert.NotEqual(VodLists.Key(Source, LibrarySection.Movies, 42), VodLists.Key(Source, LibrarySection.Series, 42));
        Assert.Equal(VodLists.Key(Source, LibrarySection.Movies, 42), VodLists.Key(Source, LibrarySection.Movies, 42));
    }

    [Fact]
    public void Keys_start_with_the_source_id_so_removing_a_source_can_clean_them_by_prefix()
    {
        Assert.StartsWith($"{Source:N}:", VodLists.Key(Source, LibrarySection.Series, 7), StringComparison.Ordinal);
        Assert.False(VodLists.Key(Guid.NewGuid(), LibrarySection.Series, 7).StartsWith($"{Source:N}:", StringComparison.Ordinal));
    }

    [Fact]
    public void Toggle_adds_then_removes()
    {
        var set = new HashSet<string>();

        Assert.True(VodLists.Toggle(set, "a"));
        Assert.Contains("a", set);
        Assert.False(VodLists.Toggle(set, "a"));
        Assert.Empty(set);
    }

    [Fact]
    public void Old_saved_state_without_the_new_lists_still_loads_with_empty_lists()
    {
        var state = JsonSerializer.Deserialize<LocalState>("""{"Volume":60}""");

        Assert.NotNull(state);
        Assert.Empty(state.VodFavorites);
        Assert.Empty(state.VodWatchlist);
    }

    [Fact]
    public void The_lists_survive_a_save_and_load_round_trip()
    {
        var state = new LocalState();
        state.VodFavorites.Add(VodLists.Key(Source, LibrarySection.Movies, 1));
        state.VodWatchlist.Add(VodLists.Key(Source, LibrarySection.Series, 2));

        var loaded = JsonSerializer.Deserialize<LocalState>(JsonSerializer.SerializeToUtf8Bytes(state));

        Assert.NotNull(loaded);
        Assert.Contains(VodLists.Key(Source, LibrarySection.Movies, 1), loaded.VodFavorites);
        Assert.Contains(VodLists.Key(Source, LibrarySection.Series, 2), loaded.VodWatchlist);
    }
}
