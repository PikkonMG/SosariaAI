using System;
using SosariaAI.Admin;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class WalkRecoveryRulesTests
{
    [Fact]
    public void TwoFrozenThinks_StepAside() =>
        Assert.Equal(WalkRecovery.StepAside, WalkRecoveryRules.Next(WalkRecoveryRules.FrozenTicksBeforeStepAside, stallTicks: 1));

    [Fact]
    public void MovingWalker_DoesNothing()
    {
        Assert.Equal(WalkRecovery.None, WalkRecoveryRules.Next(frozenTicks: 0, stallTicks: 0));
        Assert.Equal(WalkRecovery.None, WalkRecoveryRules.Next(frozenTicks: 1, stallTicks: 1));
    }

    [Fact]
    public void StalledSpells_ReplanThenStepAroundThenNudgeAndReplan()
    {
        // The second spell leaves the engine path for the walker's own steps: the engine
        // plans round a shut door, and a Trinsic home behind one stalled the whole leg.
        Assert.Equal(WalkRecovery.Replan, WalkRecoveryRules.Next(frozenTicks: 0, WalkRecoveryRules.StallTicksPerReplan));
        Assert.Equal(WalkRecovery.StepAround, WalkRecoveryRules.Next(frozenTicks: 0, WalkRecoveryRules.StallTicksPerReplan * 2));
        Assert.Equal(WalkRecovery.Replan, WalkRecoveryRules.Next(frozenTicks: 0, WalkRecoveryRules.StallTicksPerReplan * 3));
        Assert.Equal(WalkRecovery.NudgeAndReplan, WalkRecoveryRules.Next(frozenTicks: 0, WalkRecoveryRules.StallTicksPerReplan * 4));
    }

    [Fact]
    public void StepAround_ComesOnceASpell()
    {
        Assert.Equal(WalkRecoveryRules.StallTicksPerReplan * 2, WalkRecoveryRules.StepAroundStallTicks);
        Assert.NotEqual(WalkRecovery.StepAround, WalkRecoveryRules.Next(frozenTicks: 0, WalkRecoveryRules.StepAroundStallTicks + 1));
    }

    [Fact]
    public void Trip_ReplansAFewTimesThenEnds()
    {
        Assert.True(WalkRecoveryRules.MayReplanTrip(0));
        Assert.True(WalkRecoveryRules.MayReplanTrip(WalkRecoveryRules.MaxTripReplans - 1));
        Assert.False(WalkRecoveryRules.MayReplanTrip(WalkRecoveryRules.MaxTripReplans));
    }

    [Fact]
    public void CarriedOffLeg_AFightPullsTheWalkerWellAway()
    {
        // Walden stood 38 tiles off his leg after a running fight at the Minoc road.
        Assert.True(WalkRecoveryRules.CarriedOffLeg(sameMap: true, legTilesBefore: 4, legTilesNow: 38));
        Assert.True(WalkRecoveryRules.CarriedOffLeg(sameMap: false, legTilesBefore: 4, legTilesNow: 4));
    }

    [Fact]
    public void CarriedOffLeg_ASlowThinkOnTheWayIsNot()
    {
        Assert.False(WalkRecoveryRules.CarriedOffLeg(sameMap: true, legTilesBefore: 10, legTilesNow: 4));
        Assert.False(WalkRecoveryRules.CarriedOffLeg(
            sameMap: true,
            legTilesBefore: 4,
            legTilesNow: 4 + WalkRecoveryRules.CarriedOffLegTiles
        ));
    }

    [Fact]
    public void ReadsRoadForDanger_ATripChasedOffItsRoad_ReadsTheNextRoad()
    {
        // Terrin of Jhelom was run off his road home 66 times by the same reds.
        Assert.True(WalkRecoveryRules.ReadsRoadForDanger(shunsDanger: false, carriedOff: true, inParty: false));
        Assert.True(WalkRecoveryRules.ReadsRoadForDanger(shunsDanger: true, carriedOff: false, inParty: true));
    }

    [Fact]
    public void ReadsRoadForDanger_APartyCarriedOffItsRoad_WalksOnTogether() =>
        Assert.False(WalkRecoveryRules.ReadsRoadForDanger(shunsDanger: false, carriedOff: true, inParty: true));

    [Fact]
    public void ReadsRoadForDanger_AnOrdinaryTripNeverCarriedOff_WalksItsRoad() =>
        Assert.False(WalkRecoveryRules.ReadsRoadForDanger(shunsDanger: false, carriedOff: false, inParty: false));

    [Fact]
    public void DangerBarsRoad_APlaceJustRunFrom_BarsTheRoadWithoutALook()
    {
        // Maida Wilde got clear 14 tiles from Jago of Papua: the place was seconds old.
        var looked = false;

        Assert.True(WalkRecoveryRules.DangerBarsRoad(TimeSpan.FromSeconds(1), () => looked = true));
        Assert.False(looked);
    }

    [Fact]
    public void DangerBarsRoad_AQuietPlaceWithNobodyThere_IsWalkedPast() =>
        Assert.False(WalkRecoveryRules.DangerBarsRoad(WalkRecoveryRules.DangerQuietAfter, () => false));

    [Fact]
    public void DangerBarsRoad_AQuietPlaceAThreatStillHolds_BarsTheRoad() =>
        // The reds camped at the Minoc moongate ran Terrin of Jhelom off his road home 66 times.
        Assert.True(WalkRecoveryRules.DangerBarsRoad(WalkRecoveryRules.DangerQuietAfter * 3, () => true));

    [Fact]
    public void MayWaitOutDanger_AWalkCarriedOff_WaitsTwiceThenGivesUp()
    {
        Assert.True(WalkRecoveryRules.MayWaitOutDanger(shunsDanger: false, waitsSoFar: 0));
        Assert.True(WalkRecoveryRules.MayWaitOutDanger(shunsDanger: false, WalkRecoveryRules.MaxDangerWaits - 1));
        Assert.False(WalkRecoveryRules.MayWaitOutDanger(shunsDanger: false, WalkRecoveryRules.MaxDangerWaits));
    }

    [Fact]
    public void MayWaitOutDanger_ATripThatShunsDanger_GivesUpAtOnce() =>
        // Ursel Walsh's town trip past the Magincia red camp is given up; the planner picks again.
        Assert.False(WalkRecoveryRules.MayWaitOutDanger(shunsDanger: true, waitsSoFar: 0));

    [Fact]
    public void DangerWaits_OutlastTheQuietSpan_AndStayUnderTheWatchdog()
    {
        Assert.True(WalkRecoveryRules.MaxDangerWait > WalkRecoveryRules.DangerQuietAfter);
        Assert.True(WalkRecoveryRules.MaxDangerWait * WalkRecoveryRules.MaxDangerWaits < StallRules.StallAfter);
    }
}
