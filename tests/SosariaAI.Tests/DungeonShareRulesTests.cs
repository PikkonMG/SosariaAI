using System.Collections.Generic;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonShareRulesTests
{
    private const int Fighters = 400;

    [Fact]
    public void Demand_EmptyHallsPullHard()
    {
        Assert.Equal(DungeonShareRules.MaxDemand, DungeonShareRules.Demand(underground: 12, Fighters));
        Assert.Equal(DungeonShareRules.MaxDemand, DungeonShareRules.Demand(underground: 0, Fighters));
    }

    [Fact]
    public void Demand_AtTheTargetIsNeutral()
    {
        var atTarget = (int)(Fighters * DungeonShareRules.TargetShare);

        Assert.Equal(DungeonShareRules.Neutral, DungeonShareRules.Demand(atTarget, Fighters), precision: 6);
    }

    [Fact]
    public void Demand_CrowdedHallsPullSoftly()
    {
        Assert.True(DungeonShareRules.Demand(underground: 150, Fighters) < DungeonShareRules.Neutral);
        Assert.Equal(DungeonShareRules.MinDemand, DungeonShareRules.Demand(underground: Fighters, Fighters));
    }

    [Fact]
    public void Demand_TooFewFightersToCount_IsNeutral()
    {
        Assert.Equal(DungeonShareRules.Neutral, DungeonShareRules.Demand(0, DungeonShareRules.MinFighters - 1));
        Assert.Equal(0, DungeonShareRules.Share(3, 0));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, false, false)]
    public void StaysUnderground_OnlyWellAndStocked(bool inDungeon, bool healthy, bool suppliesLow, bool stays) =>
        Assert.Equal(stays, DungeonShareRules.StaysUnderground(inDungeon, healthy, suppliesLow));

    [Fact]
    public void SpreadWeight_ACrowdWeighsLess()
    {
        Assert.Equal(1.0, DungeonShareRules.SpreadWeight(0));
        Assert.Equal(0.5, DungeonShareRules.SpreadWeight(DungeonShareRules.CrowdHalf), precision: 6);
        Assert.True(DungeonShareRules.SpreadWeight(DungeonShareRules.CrowdHalf * 4) < DungeonShareRules.SpreadWeight(DungeonShareRules.CrowdHalf));
        Assert.Equal(1.0, DungeonShareRules.SpreadWeight(-1));
    }

    [Fact]
    public void CensusLine_BusiestFirst_EasyToCount()
    {
        var visitors = new Dictionary<string, int> { ["Orc Cave"] = 3, ["Sanctuary"] = 12, ["Destard"] = 3 };

        Assert.Equal(
            "Dungeon visitors: Sanctuary 12, Destard 3, Orc Cave 3 (15 underground of 300 fighters)",
            DungeonShareRules.CensusLine(visitors, 15, 300)
        );
        Assert.Equal("Dungeon visitors: none (0 underground of 4 fighters)", DungeonShareRules.CensusLine(null, 0, 4));
    }

    [Fact]
    public void MissLine_BiggestReasonFirst_WithTheMeanOdds()
    {
        var reasons = new Dictionary<string, int>
        {
            [DungeonShareRules.RolledReason("bank")] = 2,
            [DungeonShareRules.NoDoorInReach] = 7,
            [DungeonShareRules.GoalReason("Pk")] = 1
        };

        Assert.Equal(
            "Dungeon misses: no door in reach 7, rolled bank 2, goal Pk 1 (dungeon odds 30 % for 2 open of 10 above ground)",
            DungeonShareRules.MissLine(reasons, oddsSum: 0.6, open: 2, aboveGround: 10)
        );
        Assert.Equal(
            "Dungeon misses: none (dungeon odds 0 % for 0 open of 0 above ground)",
            DungeonShareRules.MissLine(null, 0, 0, 0)
        );
    }

    [Fact]
    public void InDungeonBoost_OutweighsThePullHome() =>
        Assert.True(DungeonShareRules.InDungeonScoreBoost > ActionScorer.HomeLeashBoost);
}
