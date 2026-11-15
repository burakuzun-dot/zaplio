using Xunit;

namespace Zaplio.Tests;

public sealed class StateScreenPolicyTests
{
    [Theory]
    [InlineData(0, StateScreenPolicy.AutoRetrySeconds)]
    [InlineData(1, StateScreenPolicy.AutoRetrySeconds)]
    [InlineData(StateScreenPolicy.MaxAutomaticFailures, 0)]
    [InlineData(10, 0)]
    public void Stops_retrying_by_itself_after_a_few_failures(int failures, int expectedSeconds) =>
        Assert.Equal(expectedSeconds, StateScreenPolicy.NextAutoRetrySeconds(failures));

    [Fact]
    public void Favorite_converter_asks_the_main_window()
    {
        var channel = new Channel("a:1", Guid.Empty, "Kanal A", "Ulusal", new Uri("http://example.com/a"));
        ChannelFavoriteConverter.Lookup = item => item.Id == "a:1";
        try
        {
            Assert.Equal(true, ChannelFavoriteConverter.Instance.Convert(channel, typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(false, ChannelFavoriteConverter.Instance.Convert("başka", typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            ChannelFavoriteConverter.Lookup = null;
        }
    }
}
