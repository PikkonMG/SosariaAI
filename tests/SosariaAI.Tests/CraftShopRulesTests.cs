using System;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class CraftShopRulesTests
{
    private const int ExceptionalValue = 3000;
    private const double QuarterChance = 0.25;
    private const int QuarterShare = 750;
    private const uint SomeSerial = 0x40001234;
    private const int ThreeBest = 3;

    [Fact]
    public void BestFirst_TheMostValuableFirst_FirstOfEqualsFirst()
    {
        Assert.Equal([1, 3, 2], CraftShopRules.BestFirst([60, 480, 90, 480], ThreeBest));
        Assert.Empty(CraftShopRules.BestFirst(null, CraftShopRules.ListedPieces));
    }

    [Fact]
    public void PiecesToBank_AllButTheBestEight()
    {
        int[] values = [10, 90, 20, 80, 30, 70, 40, 60, 50, 5];

        Assert.Equal([0, 9], CraftShopRules.PiecesToBank(values));
        Assert.Empty(CraftShopRules.PiecesToBank([1, 2, 3]));
    }

    [Theory]
    [InlineData(0, 5, 5)]
    [InlineData(2, 20, 6)]
    [InlineData(CraftShopRules.RestockBelow, 20, 0)]
    [InlineData(0, 0, 0)]
    public void PiecesToTake_OnlyWhenThePackRunsLow(int pack, int bank, int expected) =>
        Assert.Equal(expected, CraftShopRules.PiecesToTake(pack, bank));

    [Fact]
    public void ShopShare_TheExceptionalChanceOfTheValue()
    {
        Assert.Equal(QuarterShare, CraftShopRules.ShopShare(QuarterChance, ExceptionalValue));
        Assert.Equal(0, CraftShopRules.ShopShare(QuarterChance, 0));
    }

    [Fact]
    public void AskingRoll_SameForOnePiece_InsideTheBand()
    {
        Assert.Equal(CraftShopRules.AskingRoll(SomeSerial), CraftShopRules.AskingRoll(SomeSerial));
        Assert.InRange(CraftShopRules.AskingRoll(SomeSerial), 0, Appraisal.PercentScale - 1);
    }

    [Fact]
    public void ShoutGap_TwoToFourMinutes()
    {
        Assert.Equal(TimeSpan.FromSeconds(CraftShopRules.MinShoutGapSeconds), CraftShopRules.ShoutGap(0));
        Assert.InRange(
            CraftShopRules.ShoutGap(int.MinValue).TotalSeconds, CraftShopRules.MinShoutGapSeconds, CraftShopRules.MaxShoutGapSeconds
        );
    }

    [Fact]
    public void HasRoom_BelowTheCap() => Assert.False(CraftShopRules.HasRoom(CraftShopRules.PackStockCap));
}
