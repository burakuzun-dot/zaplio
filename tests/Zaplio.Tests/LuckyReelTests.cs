using Xunit;

namespace Zaplio.Tests;

public sealed class LuckyReelTests
{
    [Fact]
    public void PickAvoidsRecentChoicesWhileOthersRemain()
    {
        string[] keys = ["a", "b", "c"];
        for (var seed = 0; seed < 50; seed++)
            Assert.Equal(2, LuckyReel.Pick(keys, ["a", "b"], new Random(seed)));
    }

    [Fact]
    public void PickFallsBackToAnyItemWhenAllWereRecent()
    {
        var index = LuckyReel.Pick(["a", "b"], ["a", "b"], new Random(1));
        Assert.InRange(index, 0, 1);
        Assert.Equal(-1, LuckyReel.Pick([], [], new Random(1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(400)]
    public void ReelStopsOnTheWinnerWithDifferentNeighbours(int count)
    {
        for (var seed = 0; seed < 30; seed++)
        {
            var winner = seed % count;
            var reel = LuckyReel.Build(count, winner, new Random(seed));

            Assert.Equal(LuckyReel.Length, reel.Length);
            Assert.Equal(winner, reel[LuckyReel.WinnerSlot]);
            Assert.All(reel, index => Assert.InRange(index, 0, count - 1));
            if (count > 1)
            {
                Assert.NotEqual(winner, reel[LuckyReel.WinnerSlot - 1]);
                Assert.NotEqual(winner, reel[LuckyReel.WinnerSlot + 1]);
            }
            if (count > 2)
                for (var slot = 1; slot < reel.Length; slot++)
                    Assert.NotEqual(reel[slot - 1], reel[slot]);
        }
    }

    [Fact]
    public void InvalidInputGivesAnEmptyReel()
    {
        Assert.Empty(LuckyReel.Build(0, 0, new Random(1)));
        Assert.Empty(LuckyReel.Build(3, 3, new Random(1)));
    }

    [Fact]
    public void OffsetCentresTheSlotInTheViewport()
    {
        var offset = LuckyReel.Offset(10, cardWidth: 150, gap: 12, viewport: 1000);
        var cardCentre = 10 * (150 + 12) + 75 - offset;
        Assert.Equal(500, cardCentre);
    }
}
