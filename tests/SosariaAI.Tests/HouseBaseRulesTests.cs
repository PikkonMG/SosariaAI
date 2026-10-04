using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class HouseBaseRulesTests
{
    private static readonly Point3D YewGate = new(771, 752, 5);
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    [Fact]
    public void MultiFor_ATowerFirstWhereASpotHoldsSeveral_ElseSmallHouses()
    {
        Assert.Equal(HouseBaseRules.SmallTowerId, HouseBaseRules.MultiFor(0, HotSpotRules.YewGateHouses));
        Assert.Contains(HouseBaseRules.MultiFor(1, HotSpotRules.YewGateHouses), HouseBaseRules.SmallHouseIds);
        Assert.Contains(HouseBaseRules.MultiFor(0, HotSpotRules.SpotHouses), HouseBaseRules.SmallHouseIds);
    }

    [Fact]
    public void WantsRedOwner_EveryThirdBase()
    {
        Assert.False(HouseBaseRules.WantsRedOwner(0));
        Assert.False(HouseBaseRules.WantsRedOwner(1));
        Assert.True(HouseBaseRules.WantsRedOwner(HouseBaseRules.RedHouseEvery - 1));
    }

    [Fact]
    public void Plots_LieOnTheOutskirts_NearestRingFirst()
    {
        var last = 0;
        var count = 0;

        foreach (var plot in HouseBaseRules.Plots(YewGate))
        {
            var distance = NavMetric.Chebyshev(YewGate, plot);
            Assert.InRange(distance, HouseBaseRules.MinTiles, HouseBaseRules.MaxTiles);
            Assert.True(distance >= last);
            last = distance;
            count++;
        }

        Assert.True(count > 0);
    }

    [Fact]
    public void ClearOf_KeepsItsDistance()
    {
        List<Point3D> camps = [new(100, 100, 0)];

        Assert.False(HouseBaseRules.ClearOf(new Point3D(100 + HouseBaseRules.CampClearTiles, 100, 0), camps, HouseBaseRules.CampClearTiles));
        Assert.True(HouseBaseRules.ClearOf(new Point3D(101 + HouseBaseRules.CampClearTiles, 100, 0), camps, HouseBaseRules.CampClearTiles));
        Assert.True(HouseBaseRules.ClearOf(new Point3D(100, 100, 0), null, HouseBaseRules.CampClearTiles));
    }

    [Theory]
    [InlineData(0.4, false, false, 10, true)]
    [InlineData(0.9, true, false, 10, true)]
    [InlineData(0.9, false, false, 10, false)]
    [InlineData(0.4, false, true, 10, false)]
    [InlineData(0.4, false, false, HouseBaseRules.RetreatTiles + 1, false)]
    public void ShouldRetreat_HurtOrShortNearTheBase_OutOfAFight(double hits, bool low, bool fighting, int tiles, bool retreat) =>
        Assert.Equal(retreat, HouseBaseRules.ShouldRetreat(hits, low, fighting, tiles));

    [Fact]
    public void ReadyToReturn_HealedAndClean_OrInsideTooLong()
    {
        Assert.True(HouseBaseRules.ReadyToReturn(HouseBaseRules.ReturnHits, poisoned: false, TimeSpan.Zero));
        Assert.False(HouseBaseRules.ReadyToReturn(HouseBaseRules.ReturnHits, poisoned: true, TimeSpan.Zero));
        Assert.False(HouseBaseRules.ReadyToReturn(HouseBaseRules.RetreatHits, poisoned: false, TimeSpan.Zero));
        Assert.True(HouseBaseRules.ReadyToReturn(HouseBaseRules.RetreatHits, poisoned: true, HouseBaseRules.StayLimit));
    }

    [Fact]
    public void Rested_AfterTheRetreatRest()
    {
        Assert.True(HouseBaseRules.Rested(default, Now));
        Assert.False(HouseBaseRules.Rested(Now, Now + HouseBaseRules.RetreatRest - TimeSpan.FromSeconds(1)));
        Assert.True(HouseBaseRules.Rested(Now, Now + HouseBaseRules.RetreatRest));
    }

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(40, 100, 60)]
    [InlineData(120, 100, 0)]
    [InlineData(-5, 10, 10)]
    public void TopUp_ToTheMark(int have, int target, int add) => Assert.Equal(add, HouseBaseRules.TopUp(have, target));

    [Theory]
    [InlineData(20, 50, 20)]
    [InlineData(20, 5, 5)]
    [InlineData(-3, 5, 0)]
    public void Take_AsFarAsTheChestHolds(int want, int inChest, int taken) =>
        Assert.Equal(taken, HouseBaseRules.Take(want, inChest));

    [Fact]
    public void SpotReach_CoversEveryPlot() => Assert.True(HouseBaseRules.SpotReachTiles >= HouseBaseRules.MaxTiles);
}
