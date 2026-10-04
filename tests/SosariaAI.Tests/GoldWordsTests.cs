using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class GoldWordsTests
{
    [Theory]
    [InlineData("4k", 4000, true)]
    [InlineData("2.5k", 2500, true)]
    [InlineData("1m", 1000000, true)]
    [InlineData("500gp", 500, true)]
    [InlineData("3500", 3500, false)]
    [InlineData("1,200", 1200, false)]
    public void TryRead_ReadsTheWaysGoldWasTyped(string word, int value, bool money)
    {
        Assert.True(GoldWords.TryRead(word, out var read, out var isMoney));
        Assert.Equal(value, read);
        Assert.Equal(money, isMoney);
    }

    [Theory]
    [InlineData("k")]
    [InlineData("b19")]
    [InlineData("halberd")]
    [InlineData("2nd")]
    [InlineData("")]
    public void TryRead_WordsThatAreNotNumbers(string word) => Assert.False(GoldWords.TryRead(word, out _, out _));

    [Theory]
    [InlineData(750, "750")]
    [InlineData(1000, "1k")]
    [InlineData(1500, "1.5k")]
    [InlineData(1250, "1250")]
    [InlineData(12000, "12k")]
    public void Spoken_SaysGoldTheWayPlayersDid(int gold, string spoken) => Assert.Equal(spoken, GoldWords.Spoken(gold));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(47, 47)]
    [InlineData(137, 135)]
    [InlineData(4137, 4100)]
    [InlineData(7890, 7750)]
    [InlineData(23456, 23000)]
    public void RoundSpoken_NamesRoundSteps(int gold, int spoken) => Assert.Equal(spoken, GoldWords.RoundSpoken(gold));

    [Fact]
    public void InContext_SmallBareNumberInAHaggleOverThousandsMeansThousands()
    {
        Assert.Equal(3000, GoldWords.InContext(new HeardNumber(3, 0, false), 3500));
        Assert.Equal(3, GoldWords.InContext(new HeardNumber(3, 0, false), 40));
        Assert.Equal(3, GoldWords.InContext(new HeardNumber(3, 0, true), 3500));
    }
}
