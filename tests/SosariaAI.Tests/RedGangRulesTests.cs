using System;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RedGangRulesTests
{
    [Fact]
    public void ScanRanges_AreTheirOwn_NotThePartyInviteRange()
    {
        Assert.Equal(12, RedGangRules.VictimScanTiles);
        Assert.Equal(10, RedGangRules.OutlawScanTiles);
        Assert.NotEqual(CareerSettings.DefaultPartyInviteRange, RedGangRules.VictimScanTiles);
    }

    [Fact]
    public void ShouldAmbush_AboutOneRollInThree()
    {
        var ambushes = 0;

        for (var roll = 0; roll < OutlawRules.PercentScale; roll++)
        {
            ambushes += RedGangRules.ShouldAmbush(roll) ? 1 : 0;
        }

        Assert.Equal(RedGangRules.AmbushChancePercent, ambushes);
        Assert.False(RedGangRules.ShouldAmbush(-1));
    }

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void JoinsRide_AFreeMateNotAlreadyOnARun(bool onRun, bool idle, bool hangingOut, bool joins) =>
        // A mate on a run still reads as hanging out: it was sent out again every second.
        Assert.Equal(joins, RedGangRules.JoinsRide(onRun, idle, hangingOut));

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, false)]
    public void MayRide_OnlyWhenTheMatesOwnRunCanStart(bool runRests, bool campInReach, bool setsOut, bool rides) =>
        // Kerr, with no camp in reach, was handed a run that failed at once every two seconds;
        // venom, short of reagents, was called out of the Den tavern and turned back at once.
        Assert.Equal(rides, RedGangRules.MayRide(runRests, campInReach, setsOut));

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void JoinsCall_OnlyAMateThatWouldStayInTheFight(bool leavesSideBe, bool wouldRunAtOnce, bool joins) =>
        // A mate called in against the odds said "too many" and ran the same second.
        Assert.Equal(joins, RedGangRules.JoinsCall(leavesSideBe, wouldRunAtOnce));

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void GoesForBody_OnlyAFreshBodyOutOfTheGuardsReach(bool guarded, bool looted, bool goes) =>
        Assert.Equal(goes, RedGangRules.GoesForBody(guarded, looted));

    [Fact]
    public void GivesUpLootWalk_ABodyNotReachedInAShortWhile()
    {
        var since = new DateTime(2026, 9, 27, 12, 0, 0);

        Assert.False(RedGangRules.GivesUpLootWalk(since, since + RedGangRules.LootWalkLimit - TimeSpan.FromSeconds(1)));
        Assert.True(RedGangRules.GivesUpLootWalk(since, since + RedGangRules.LootWalkLimit));
    }

    [Fact]
    public void Lurk_FourToEightMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(4), RedGangRules.LurkMin);
        Assert.Equal(TimeSpan.FromMinutes(8), RedGangRules.LurkMax);
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(0.5, 9)]
    [InlineData(1, 12)]
    [InlineData(2, 12)]
    [InlineData(-1, 6)]
    public void Between_SpansTheAmbushRoll(double fraction, int minutes) =>
        Assert.Equal(
            TimeSpan.FromMinutes(minutes),
            RedGangRules.Between(RedGangRules.AmbushRollMin, RedGangRules.AmbushRollMax, fraction)
        );

    [Theory]
    [InlineData(RedGangRules.CohesionTiles, false)]
    [InlineData(RedGangRules.CohesionTiles + 1, true)]
    [InlineData(RedGangRules.CohesionReachTiles - 1, true)]
    [InlineData(RedGangRules.CohesionReachTiles, false)]
    public void StraysFromPack_PastTwelveTilesButStillInThePack(int distance, bool strays) =>
        Assert.Equal(strays, RedGangRules.StraysFromPack(distance));

    [Fact]
    public void ShuffleStepsBack_PastTheLeash()
    {
        Assert.False(RedGangRules.ShuffleStepsBack(RedGangRules.ShuffleLeashTiles));
        Assert.True(RedGangRules.ShuffleStepsBack(RedGangRules.ShuffleLeashTiles + 1));
    }

    [Fact]
    public void NextLeg_NeverTheLegJustWalked()
    {
        const int Legs = 5;

        for (var previous = 0; previous < Legs; previous++)
        {
            for (var roll = -20; roll < 20; roll++)
            {
                var next = RedGangRules.NextLeg(Legs, previous, roll);

                Assert.InRange(next, 0, Legs - 1);
                Assert.NotEqual(previous, next);
            }
        }
    }

    [Fact]
    public void NextLeg_OneOrNone()
    {
        Assert.Equal(0, RedGangRules.NextLeg(1, 0, 7));
        Assert.Equal(RedGangRules.NoLeg, RedGangRules.NextLeg(0, RedGangRules.NoLeg, 7));
        Assert.InRange(RedGangRules.NextLeg(3, RedGangRules.NoLeg, 7), 0, 2);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void Musters_AGangRedInTheDen_AtAPointInTheDen(bool inGang, bool inDen, bool pointInDen, bool musters) =>
        Assert.Equal(musters, RedGangRules.Musters(inGang, inDen, pointInDen));

    [Fact]
    public void MusterOver_AfterTheLeastWait_OrWhenTheWaitRunsOut()
    {
        var since = new DateTime(2026, 9, 28, 12, 0, 0);
        var early = since + RedGangRules.MusterMin - TimeSpan.FromSeconds(1);
        var settled = since + RedGangRules.MusterMin;

        Assert.False(RedGangRules.MusterOver(PartyWaitResult.Formed, since, early));
        Assert.True(RedGangRules.MusterOver(PartyWaitResult.Formed, since, settled));
        Assert.False(RedGangRules.MusterOver(PartyWaitResult.Waiting, since, settled));
        Assert.True(RedGangRules.MusterOver(PartyWaitResult.TimedOut, since, early));
        Assert.True(RedGangRules.MusterMin < RedGangRules.MusterMax);
    }

    [Fact]
    public void MusterWait_LeavesALateMateBehind()
    {
        var since = new DateTime(2026, 9, 28, 12, 0, 0);
        const int farFromTheBank = RedGangRules.MusterTiles + 1;
        (string, int, bool, bool)[] late = [("late", farFromTheBank, true, false)];

        Assert.Equal(
            PartyWaitResult.Waiting,
            PartyWaitRules.WaitForMembers(since + RedGangRules.MusterMin, since, late, RedGangRules.MusterTiles, RedGangRules.MusterMax)
        );
        Assert.Equal(
            PartyWaitResult.TimedOut,
            PartyWaitRules.WaitForMembers(since + RedGangRules.MusterMax, since, late, RedGangRules.MusterTiles, RedGangRules.MusterMax)
        );
    }

    [Theory]
    [InlineData(3, 3, GangRideWay.ByRune)]
    [InlineData(3, 2, GangRideWay.OnFoot)]
    [InlineData(1, 1, GangRideWay.ByRune)]
    [InlineData(1, 0, GangRideWay.OnFoot)]
    [InlineData(0, 0, GangRideWay.OnFoot)]
    public void WayOut_ByRuneOnlyWhenEveryRiderCanRecall(int riders, int canRecall, GangRideWay way) =>
        Assert.Equal(way, RedGangRules.WayOut(riders, canRecall));

    [Fact]
    public void WayHolds_ForTheMusterAfterTheGangChose()
    {
        var chosen = new DateTime(2026, 9, 28, 12, 0, 0);

        Assert.False(RedGangRules.WayHolds(default, chosen));
        Assert.True(RedGangRules.WayHolds(chosen, chosen + RedGangRules.RideWayHold - TimeSpan.FromSeconds(1)));
        Assert.False(RedGangRules.WayHolds(chosen, chosen + RedGangRules.RideWayHold));
        Assert.NotEqual(RedGangRules.WayWords(GangRideWay.ByRune), RedGangRules.WayWords(GangRideWay.OnFoot));
    }

    [Fact]
    public void HearsRideCall_InPackRange_OrAnywhereInTheDenAtAMuster()
    {
        const int acrossTheDen = OutlawRules.PackRange + 1;

        Assert.True(RedGangRules.HearsRideCall(OutlawRules.PackRange, callerMusters: false, bothInDen: false));
        Assert.False(RedGangRules.HearsRideCall(acrossTheDen, callerMusters: false, bothInDen: true));
        Assert.False(RedGangRules.HearsRideCall(acrossTheDen, callerMusters: true, bothInDen: false));
        Assert.True(RedGangRules.HearsRideCall(acrossTheDen, callerMusters: true, bothInDen: true));
    }

    [Fact]
    public void Gather_TheFirstAtTheCampWaitForTheRest_ForAWhileOnly()
    {
        var since = new DateTime(2026, 9, 28, 12, 0, 0);
        const int stillRiding = RedGangRules.GatherTiles + 1;
        (string, int, bool, bool)[] riding = [("rider", stillRiding, true, false)];
        (string, int, bool, bool)[] there = [("rider", RedGangRules.GatherTiles, true, false)];
        (string, int, bool, bool)[] fallen = [("rider", stillRiding, false, false)];

        Assert.Equal(PartyWaitResult.Waiting, PartyWaitRules.WaitForMembers(since, since, riding, RedGangRules.GatherTiles, RedGangRules.GatherMax));
        Assert.Equal(PartyWaitResult.Formed, PartyWaitRules.WaitForMembers(since, since, there, RedGangRules.GatherTiles, RedGangRules.GatherMax));
        Assert.Equal(PartyWaitResult.Formed, PartyWaitRules.WaitForMembers(since, since, fallen, RedGangRules.GatherTiles, RedGangRules.GatherMax));
        Assert.Equal(
            PartyWaitResult.TimedOut,
            PartyWaitRules.WaitForMembers(since + RedGangRules.GatherMax, since, riding, RedGangRules.GatherTiles, RedGangRules.GatherMax)
        );
    }

    [Theory]
    [InlineData(LootKind.Gold, false, true)]
    [InlineData(LootKind.Reagent, false, true)]
    [InlineData(LootKind.Potion, false, true)]
    [InlineData(LootKind.Scroll, false, true)]
    [InlineData(LootKind.Other, true, true)]
    [InlineData(LootKind.Gear, false, false)]
    [InlineData(LootKind.Magic, false, false)]
    [InlineData(LootKind.Other, false, false)]
    public void StripsAsSupply_GoldAndWhatAFightBurns_NotGear(LootKind kind, bool bandageOrAmmo, bool strips) =>
        Assert.Equal(strips, RedGangRules.StripsAsSupply(kind, bandageOrAmmo));
}
