using System;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A hunter takes the valuables and its supplies off a kill, and stops short of overload.</summary>
public class LootRulesTests
{
    private const int MaxWeight = 215;
    private const int Light = 10;
    private const int NoRoom = 0;
    private static readonly DateTime Start = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(LootKind.Gold)]
    [InlineData(LootKind.Reagent)]
    [InlineData(LootKind.Gem)]
    [InlineData(LootKind.Scroll)]
    [InlineData(LootKind.Potion)]
    [InlineData(LootKind.Magic)]
    [InlineData(LootKind.Jewel)]
    [InlineData(LootKind.Gear)]
    public void Wants_TheValuables(LootKind kind) =>
        Assert.True(LootRules.Wants(kind, isSupply: false));

    [Fact]
    public void Wants_SuppliesButNotJunk()
    {
        Assert.True(LootRules.Wants(LootKind.Other, isSupply: true));
        Assert.False(LootRules.Wants(LootKind.Other, isSupply: false));
    }

    [Fact]
    public void HasRoom_StopsShortOfOverload()
    {
        var line = (int)(MaxWeight * LootRules.CarryFraction);

        Assert.True(LootRules.HasRoom(line - Light, MaxWeight, Light));
        Assert.False(LootRules.HasRoom(line - Light + 1, MaxWeight, Light));
        Assert.False(LootRules.HasRoom(0, NoRoom, Light));
    }

    [Fact]
    public void WorthTheWalk_OnlyNearby()
    {
        Assert.True(LootRules.WorthTheWalk(LootRules.CorpseWalkTiles));
        Assert.False(LootRules.WorthTheWalk(LootRules.CorpseWalkTiles + 1));
    }

    [Fact]
    public void TooLong_AfterTheWalkLimit()
    {
        Assert.False(LootRules.TooLong(Start, Start));
        Assert.True(LootRules.TooLong(Start + LootRules.WalkLimit, Start));
        Assert.False(LootRules.TooLong(Start, default));
    }
}
