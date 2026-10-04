using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class TreasureMarketRulesTests
{
    private const int Asking = 1000;
    private const int SoftFloor = 600;
    private const int NoAsking = 0;
    private static readonly DateTime Start = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Affords_MeetsTheSoftestFloor()
    {
        Assert.True(TreasureMarketRules.Affords(SoftFloor, Asking));
        Assert.False(TreasureMarketRules.Affords(SoftFloor - 1, Asking));
        Assert.False(TreasureMarketRules.Affords(SoftFloor, NoAsking));
    }

    [Fact]
    public void DwellOver_CountsFromTheStart()
    {
        Assert.False(TreasureMarketRules.DwellOver(Start, default, TreasureMarketRules.SellDwell));
        Assert.False(TreasureMarketRules.DwellOver(Start + TreasureMarketRules.SellDwell - TimeSpan.FromSeconds(1), Start, TreasureMarketRules.SellDwell));
        Assert.True(TreasureMarketRules.DwellOver(Start + TreasureMarketRules.SellDwell, Start, TreasureMarketRules.SellDwell));
    }

    [Fact]
    public void Ranges_ReachPastABankFloor() =>
        Assert.True(TreasureMarketRules.NoticeRange > Economy.TradeRanges.ShoutRange);
}
