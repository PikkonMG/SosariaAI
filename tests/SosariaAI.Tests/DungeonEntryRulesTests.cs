using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonEntryRulesTests
{
    private static readonly Point3D Door = new(1298, 1080, 0);
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void FirstLeg_OutsideWithADoor_WalksToThePad()
    {
        Assert.Equal(DungeonLeg.WalkToDoor, DungeonEntryRules.FirstLeg(inside: false, knowsDoorPad: true));
        Assert.Equal(DungeonLeg.WalkToHall, DungeonEntryRules.FirstLeg(inside: false, knowsDoorPad: false));
        Assert.Equal(DungeonLeg.WalkInside, DungeonEntryRules.FirstLeg(inside: true, knowsDoorPad: false));
    }

    [Fact]
    public void Legs_GoDoorPadInsideThenTheHall()
    {
        Assert.Equal(DungeonLeg.StepOnPad, DungeonEntryRules.After(DungeonLeg.WalkToDoor));
        Assert.Equal(DungeonLeg.WalkInside, DungeonEntryRules.After(DungeonLeg.StepOnPad));
        Assert.Null(DungeonEntryRules.After(DungeonLeg.WalkInside));
        Assert.Null(DungeonEntryRules.After(DungeonLeg.WalkToHall));
    }

    [Fact]
    public void CarriedInsideEarly_SkipsToTheInsideWalk()
    {
        Assert.True(DungeonEntryRules.SkipsToInside(DungeonLeg.WalkToDoor, inside: true));
        Assert.True(DungeonEntryRules.SkipsToInside(DungeonLeg.StepOnPad, inside: true));
        Assert.False(DungeonEntryRules.SkipsToInside(DungeonLeg.WalkToDoor, inside: false));
        Assert.False(DungeonEntryRules.SkipsToInside(DungeonLeg.WalkInside, inside: true));
    }

    [Fact]
    public void PadStep_GivesUpAfterTheTimeout()
    {
        Assert.False(DungeonEntryRules.PadStepExpired(Start, Start));
        Assert.True(DungeonEntryRules.PadStepExpired(Start + DungeonEntryRules.PadStepTimeout, Start));
    }

    [Fact]
    public void PickPad_NearestPadThatLandsInside()
    {
        var links = new List<(Point3D Pad, bool LandsInside)>
        {
            (new Point3D(1296, 1082, 0), false),
            (new Point3D(1299, 1083, 0), true),
            (new Point3D(1297, 1081, 0), true),
            (new Point3D(1298 + DungeonEntryRules.PadSearchTiles + 1, 1080, 0), true)
        };

        Assert.Equal(2, DungeonEntryRules.PickPad(Door, links));
    }

    [Fact]
    public void PickPad_WiderSearch_FindsTheWayOutFartherOff()
    {
        // The way out of a dungeon looks farther than a door's pad: a walker stood forty
        // minutes on one Britain Sewer tile with no pad under it.
        var stuck = new Point3D(6032, 1498, 22);
        var exit = new Point3D(stuck.X + DungeonEscapeRules.ExitPadSearchTiles, stuck.Y, stuck.Z);
        var stair = new Point3D(stuck.X + 1, stuck.Y, stuck.Z);
        var links = new List<(Point3D Pad, bool LandsWanted)> { (stair, false), (exit, true) };

        Assert.Equal(DungeonEntryRules.NoPad, DungeonEntryRules.PickPad(stuck, links));
        Assert.Equal(1, DungeonEntryRules.PickPad(stuck, links, DungeonEscapeRules.ExitPadSearchTiles));
    }

    [Fact]
    public void PickPad_NoneInside_IsNoPad()
    {
        Assert.Equal(DungeonEntryRules.NoPad, DungeonEntryRules.PickPad(Door, [(Door, false)]));
        Assert.Equal(DungeonEntryRules.NoPad, DungeonEntryRules.PickPad(Door, []));
    }

    [Fact]
    public void FailureReason_NamesTheLeg()
    {
        Assert.Equal(DelveFailure.NoWayToDoor, DungeonEntryRules.FailureReason(DungeonLeg.WalkToDoor));
        Assert.Equal(DelveFailure.DoorPadRefused, DungeonEntryRules.FailureReason(DungeonLeg.StepOnPad));
        Assert.Equal(DelveFailure.NoWayInside, DungeonEntryRules.FailureReason(DungeonLeg.WalkInside));
        Assert.Equal(DelveFailure.NoWayToHall, DungeonEntryRules.FailureReason(DungeonLeg.WalkToHall));
    }

    [Fact]
    public void NeedsApproach_TheStepReachesOnlyAPadClose_AndOnTheSameFloor()
    {
        var pad = new Point3D(1013, 1433, 0);

        Assert.False(DungeonEntryRules.NeedsApproach(new Point3D(1014, 1433, 0), pad));
        Assert.False(DungeonEntryRules.NeedsApproach(new Point3D(1013 + GatePad.ReachTiles, 1433, 0), pad));
        Assert.True(DungeonEntryRules.NeedsApproach(new Point3D(1018, 1431, 0), pad));
        Assert.True(DungeonEntryRules.NeedsApproach(new Point3D(1014, 1433, 40), pad));
    }

    [Fact]
    public void PadApproach_EndsInsideTheStepsReach() =>
        Assert.True(DungeonEntryRules.PadApproachTiles <= GatePad.ReachTiles);

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, true, false)]
    public void RecallsToDoor_AGroupOnlyWhenTheCrewCanFollow(bool inside, bool grouped, bool crewCanRecall, bool recalls) =>
        Assert.Equal(recalls, DungeonEntryRules.RecallsToDoor(inside, grouped, crewCanRecall));

    [Fact]
    public void RecalledToDoorLine_IsEasyToCount() =>
        Assert.Equal("Iolo recalled to the door of Destard", DungeonEntryRules.RecalledToDoorLine("Iolo", "Destard"));

    [Fact]
    public void RecallsAgain_AFizzleIsRecastWhileCastsAndTheWaitLast()
    {
        var retryEnds = Start + DungeonEntryRules.DoorRecallRetryWait;

        Assert.True(DungeonEntryRules.RecallsAgain(casts: 1, Start, retryEnds));
        Assert.True(DungeonEntryRules.RecallsAgain(DungeonEntryRules.DoorRecallCasts - 1, Start, retryEnds));
        Assert.False(DungeonEntryRules.RecallsAgain(DungeonEntryRules.DoorRecallCasts, Start, retryEnds));
        Assert.False(DungeonEntryRules.RecallsAgain(casts: 1, retryEnds, retryEnds));
    }

    [Fact]
    public void DoorRecall_IsCastAgainAtLeastOnce_AndWaitsOutTheRunebookRest()
    {
        Assert.True(DungeonEntryRules.DoorRecallCasts >= 2);
        Assert.True(DungeonEntryRules.DoorRecallRetryWait > Server.Items.Runebook.UseDelay);
    }

    [Fact]
    public void PathsOntoPad_AfterTheStraightStepHadItsTime_AndBeforeTheStepIsRefused()
    {
        Assert.False(DungeonEntryRules.PathsOntoPad(Start, Start));
        Assert.True(DungeonEntryRules.PathsOntoPad(Start + DungeonEntryRules.PadDirectStep, Start));
        Assert.True(DungeonEntryRules.PadDirectStep < DungeonEntryRules.PadStepTimeout);
    }

    [Theory]
    [InlineData(DungeonLeg.WalkInside, true, SkillStatus.Failed, true)]
    [InlineData(DungeonLeg.WalkToHall, true, SkillStatus.Failed, true)]
    [InlineData(DungeonLeg.WalkInside, false, SkillStatus.Failed, false)]
    [InlineData(DungeonLeg.WalkInside, true, SkillStatus.Running, false)]
    [InlineData(DungeonLeg.WalkToDoor, true, SkillStatus.Failed, false)]
    [InlineData(DungeonLeg.StepOnPad, true, SkillStatus.Failed, false)]
    public void CrawlsWhereItStands_AFailedWalkInsideCrawlsTheFloorItReached(
        DungeonLeg leg,
        bool inside,
        SkillStatus status,
        bool crawls
    ) =>
        Assert.Equal(crawls, DungeonEntryRules.CrawlsWhereItStands(leg, inside, status));

    [Fact]
    public void Reenters_OnlyAfterBeingInside_AndOnlyAFewTimes()
    {
        Assert.True(DungeonEntryRules.Reenters(reachedInside: true, inside: false, reentries: 0));
        Assert.False(DungeonEntryRules.Reenters(reachedInside: true, inside: true, reentries: 0));
        Assert.False(DungeonEntryRules.Reenters(reachedInside: false, inside: false, reentries: 0));
        Assert.False(DungeonEntryRules.Reenters(reachedInside: true, inside: false, DungeonEntryRules.MaxReentries));
    }

    [Fact]
    public void WalkIsLong_FromTheLongWalkOn_AndARecallTripIsShorter()
    {
        Assert.True(DungeonEntryRules.WalkIsLong(DungeonEntryRules.LongWalkTiles));
        Assert.False(DungeonEntryRules.WalkIsLong(DungeonEntryRules.LongWalkTiles - 1));
        Assert.True(DungeonEntryRules.RecallMinTripTiles < DungeonEntryRules.LongWalkTiles);
    }

    [Fact]
    public void WalkAndNearerLines_AreEasyToCount()
    {
        Assert.Equal(
            "Iolo walks 612 tiles to the door of Despise: no rune lands near the goal",
            DungeonEntryRules.WalkLine("Iolo", "Despise", 612, RecallRules.NoRuneWhy)
        );
        Assert.Equal(
            "Iolo turns from the long walk to Covetous to the nearer Orc Cave",
            DungeonEntryRules.NearerLine("Iolo", "Covetous", "Orc Cave")
        );
        Assert.Equal(
            "Iolo was carried out of Orc Cave by a pad and goes back in",
            DungeonEntryRules.ReenterLine("Iolo", "Orc Cave")
        );
    }
}
