using Xunit;

namespace Zaplio.Tests;

public sealed class TelevisionEffectTests
{
    private static readonly Channel Live = new("kanal-a", Guid.NewGuid(), "Kanal A", "Genel", new Uri("https://example.invalid/live"));
    private static readonly Channel Next = Live with { Id = "kanal-b", Name = "Kanal B" };

    [Fact]
    public void Shows_static_for_a_different_live_channel()
    {
        Assert.True(TelevisionEffect.IsChannelChange(Live, Next, reconnect: false));
    }

    [Fact]
    public void Skips_static_for_reconnections_first_play_and_on_demand()
    {
        Assert.False(TelevisionEffect.IsChannelChange(Live, Next, reconnect: true));
        Assert.False(TelevisionEffect.IsChannelChange(null, Next, reconnect: false));
        Assert.False(TelevisionEffect.IsChannelChange(Live, Live, reconnect: false));
        Assert.False(TelevisionEffect.IsChannelChange(Live, Next with { IsOnDemand = true }, reconnect: false));
        Assert.False(TelevisionEffect.IsChannelChange(Live with { IsOnDemand = true }, Next, reconnect: false));
    }
}
