using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Server;
using Xunit;

namespace SosariaAI.Tests;

public class PkGangRulesTests
{
    private const int MaxReds = 80;
    private const int AllInGangs = 100;
    private const int NoneInGangs = 0;

    [Fact]
    public void Gangs_AreTwoToFourForEveryCountFromTwo()
    {
        for (var reds = PkGangRules.MinGangSize; reds <= MaxReds; reds++)
        {
            var sizes = new Dictionary<int, int>();

            for (var slot = 0; slot < reds; slot++)
            {
                var gang = PkGangRules.GangOf(slot, reds, AllInGangs);
                sizes[gang] = sizes.GetValueOrDefault(gang) + 1;
            }

            foreach (var size in sizes.Values)
            {
                Assert.InRange(size, PkGangRules.MinGangSize, PkGangRules.MaxGangSize);
            }
        }
    }

    [Fact]
    public void Gangs_TwentyRedsMakeSeveralGangs()
    {
        var ids = new List<string>();

        for (var i = 0; i < 20; i++)
        {
            ids.Add($"red#{i}");
        }

        var gangs = PkGangRules.Gangs(ids, AllInGangs);

        Assert.Equal(0, gangs[0]);
        Assert.True(gangs[^1] >= 5);
        Assert.Equal(PkGangRules.NoGang, PkGangRules.GangOf(20, 20, AllInGangs));
        Assert.Empty(PkGangRules.Gangs(null, AllInGangs));
    }

    [Fact]
    public void GangedCount_FollowsThePercentAndNeverLeavesAGangOfOne()
    {
        const int Reds = 75;
        const int EightyPercent = 80;

        Assert.Equal(60, PkGangRules.GangedCount(Reds, EightyPercent));
        Assert.Equal(Reds, PkGangRules.GangedCount(Reds, AllInGangs));
        Assert.Equal(0, PkGangRules.GangedCount(Reds, NoneInGangs));
        Assert.Equal(0, PkGangRules.GangedCount(Reds, 1));
        Assert.Equal(Reds, PkGangRules.GangedCount(Reds, 250));
    }

    [Fact]
    public void GangOf_RedsPastTheGangedShareHuntAlone()
    {
        const int Reds = 75;
        const int EightyPercent = 80;
        var sizes = new Dictionary<int, int>();

        for (var slot = 0; slot < Reds; slot++)
        {
            var gang = PkGangRules.GangOf(slot, Reds, EightyPercent);
            sizes[gang] = sizes.GetValueOrDefault(gang) + 1;
        }

        var loners = 0;

        foreach (var size in sizes.Values)
        {
            if (size == 1)
            {
                loners++;
            }
            else
            {
                Assert.InRange(size, PkGangRules.MinGangSize, PkGangRules.MaxGangSize);
            }
        }

        Assert.Equal(Reds - PkGangRules.GangedCount(Reds, EightyPercent), loners);
    }

    [Fact]
    public void IsDungeonMouth_SurfaceEntrancesOnly()
    {
        Assert.True(PkGangRules.IsDungeonMouth("Dungeon", "Destard Entrance", 1176));
        Assert.False(PkGangRules.IsDungeonMouth("Dungeon", "Destard Level 1", 5243));
        Assert.False(PkGangRules.IsDungeonMouth("Dungeon", "Ice Entrance", 5210));
        Assert.False(PkGangRules.IsDungeonMouth("Hunt", "Orc Camp Entrance", 1000));
    }

    [Fact]
    public void BuildRules_StartRedAndTradeForHidingAndTracking()
    {
        var murders = PkBuildRules.StartingMurders("felucca:red#1");

        Assert.True(PkRules.IsRed(murders));
        Assert.Equal(murders, PkBuildRules.StartingMurders("felucca:red#1"));
        Assert.Contains((SkillName.Parry, SkillName.Tracking), PkBuildRules.TradeSwaps(PersonClass.Warrior));
        Assert.Contains((SkillName.MagicResist, SkillName.Hiding), PkBuildRules.TradeSwaps(PersonClass.Fencer));
        Assert.Contains((SkillName.Wrestling, SkillName.Hiding), PkBuildRules.TradeSwaps(PersonClass.Mage));
        Assert.Empty(PkBuildRules.TradeSwaps(PersonClass.Miner));
        Assert.Contains((SkillName.Parry, SkillName.Tracking), PkBuildRules.TradeSwaps(PersonClass.Paladin));
        Assert.Contains((SkillName.Wrestling, SkillName.Hiding), PkBuildRules.TradeSwaps(PersonClass.Necromancer));
        Assert.True(PkBuildRules.ShouldSwap(100, 0));
        Assert.False(PkBuildRules.ShouldSwap(0, 100));
    }

    [Fact]
    public void PkRules_RedsKeepOutOfGuardedPlaces()
    {
        Assert.False(PkRules.MayVisit(isRed: true, guarded: true));
        Assert.True(PkRules.MayVisit(isRed: true, guarded: false));
        Assert.True(PkRules.MayVisit(isRed: false, guarded: true));
        Assert.True(PkRules.InBuccaneersDen(PkRules.BucsDenHaven.X, PkRules.BucsDenHaven.Y));
    }

    [Fact]
    public void SameGang_OnlyReds_OfOneRealGang()
    {
        const int Gang = 3;
        const int OtherGang = 4;

        Assert.True(PkGangRules.SameGang(Gang, Gang));
        Assert.False(PkGangRules.SameGang(Gang, OtherGang));
        Assert.False(PkGangRules.SameGang(PkGangRules.NoGang, PkGangRules.NoGang));
    }
}
