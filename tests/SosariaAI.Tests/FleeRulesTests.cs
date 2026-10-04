using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class FleeRulesTests
{
    [Fact]
    public void MayFlee_IgnoresGuardedTownAndLowThreat()
    {
        Assert.False(FleeRules.MayFlee(inGuardedRegion: true, threat: 200));
        Assert.False(FleeRules.MayFlee(inGuardedRegion: false, threat: 0));
        Assert.False(FleeRules.MayFlee(inGuardedRegion: false, threat: ThreatRating.MinThreatToFlee - 1));
        Assert.True(FleeRules.MayFlee(inGuardedRegion: false, threat: ThreatRating.MinThreatToFlee));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    public void GuardsShelter_OnlyThoseTheGuardsDoNotComeFor(bool underGuards, bool criminal, bool murderer, bool sheltered) =>
        Assert.Equal(sheltered, FleeRules.GuardsShelter(underGuards, criminal, murderer));

    [Fact]
    public void MayFlee_ARedUnderTheGuardsMayRunOut()
    {
        var threat = ThreatRating.MinThreatToFlee;

        Assert.True(FleeRules.MayFlee(FleeRules.GuardsShelter(underGuards: true, criminal: false, murderer: true), threat));
        Assert.False(FleeRules.MayFlee(FleeRules.GuardsShelter(underGuards: true, criminal: false, murderer: false), threat));
    }

    [Fact]
    public void MayFleeAttacker_IgnoresGuardZones()
    {
        // A war enemy hitting a worker under the Britain guards is not a distant
        // sighting the guards will answer. The guard-zone rule must not apply.
        Assert.False(FleeRules.MayFleeAttacker(ThreatRating.MinThreatToFlee - 1));
        Assert.True(FleeRules.MayFleeAttacker(ThreatRating.MinThreatToFlee));
    }

    [Fact]
    public void MustRun_WorkerRunsFromAnyRealThreat()
    {
        Assert.False(FleeRules.MustRun(false, CharacterRole.Worker, 10, intolerable: false));
        Assert.True(FleeRules.MustRun(false, CharacterRole.Worker, ThreatRating.MinThreatToFlee, intolerable: false));
    }

    [Fact]
    public void MustRun_FighterRunsOnlyFromAThreatTooStrongForIt()
    {
        // The danger check stopped a fighter's action for a threat it could not take, but
        // the goal loop never offered it a flee. It stood still, frozen, all night.
        Assert.False(FleeRules.MustRun(false, CharacterRole.Fighter, ThreatRating.MinThreatToFlee, intolerable: false));
        Assert.True(FleeRules.MustRun(false, CharacterRole.Fighter, ThreatRating.MinThreatToFlee, intolerable: true));
    }

    [Fact]
    public void MustRun_NeverInsideGuardsOrForALowThreat()
    {
        Assert.False(FleeRules.MustRun(true, CharacterRole.Fighter, ThreatRating.MinThreatToFlee, intolerable: true));
        Assert.False(FleeRules.MustRun(false, CharacterRole.Fighter, ThreatRating.MinThreatToFlee - 1, intolerable: true));
    }

    [Fact]
    public void ShouldAbortWork_WhenFleeStillOpen()
    {
        Assert.True(FleeRules.ShouldAbortWork(mustRun: true, fleeBlocked: false, currentSkillKind: null));
        Assert.False(FleeRules.ShouldAbortWork(mustRun: false, fleeBlocked: false, currentSkillKind: null));
    }

    [Fact]
    public void ShouldAbortWork_WhenFleeBlocked_LetsWanderRun()
    {
        Assert.False(FleeRules.ShouldAbortWork(mustRun: true, fleeBlocked: true, currentSkillKind: null));
    }

    [Fact]
    public void ShouldAbortWork_GoHome_Never()
    {
        Assert.False(FleeRules.ShouldAbortWork(mustRun: true, fleeBlocked: false, SkillKinds.GoHome));
        Assert.True(FleeRules.ShouldAbortWork(mustRun: true, fleeBlocked: false, SkillKinds.IdleWander));
    }

    [Fact]
    public void ShouldAbortForAiAction_IdleWander_Never()
    {
        // Combat flicker at a dungeon mouth aborted idle every tick, then idle
        // fallback began a new IdleWander every second with no matching end.
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.IdleWander, actionIsWander: false));
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.IdleWander, actionIsWander: true));
    }

    [Fact]
    public void ShouldAbortForAiAction_Work_OnlyWhenAiLeavesWander()
    {
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.GoHome, actionIsWander: true));
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.GoHome, actionIsWander: false));
        Assert.True(FleeRules.ShouldAbortForAiAction(SkillKinds.GoTo, actionIsWander: false));
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.Flee, actionIsWander: false));
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.Hunt, actionIsWander: false));
        Assert.False(FleeRules.ShouldAbortForAiAction(SkillKinds.Dungeon, actionIsWander: false));
    }

    [Fact]
    public void ShouldReuseIdle_OnlyAfterAbort()
    {
        Assert.True(FleeRules.ShouldReuseIdle(SkillKinds.IdleWander, aborted: true));
        Assert.False(FleeRules.ShouldReuseIdle(SkillKinds.IdleWander, aborted: false));
        Assert.False(FleeRules.ShouldReuseIdle(SkillKinds.GoHome, aborted: true));
        Assert.False(FleeRules.ShouldReuseIdle(null, aborted: true));
    }

    [Fact]
    public void FleeIsBlocked_AfterRepeatLimitOnFleeId()
    {
        Assert.False(FleeRules.FleeIsBlocked("flee:Flee:0", RepeatFailure.Limit - 1));
        Assert.True(FleeRules.FleeIsBlocked("flee:Flee:0", RepeatFailure.Limit));
        Assert.False(FleeRules.FleeIsBlocked("town:IdleWander:1", RepeatFailure.Limit));
        Assert.False(FleeRules.FleeIsBlocked(null, RepeatFailure.Limit));
    }

    [Fact]
    public void IsEscapeWhenFleeBlocked_AllowsHomeAndWanderOnly()
    {
        Assert.True(FleeRules.IsEscapeWhenFleeBlocked(SkillKinds.GoHome));
        Assert.True(FleeRules.IsEscapeWhenFleeBlocked(SkillKinds.IdleWander));
        Assert.False(FleeRules.IsEscapeWhenFleeBlocked(SkillKinds.Flee));
        Assert.False(FleeRules.IsEscapeWhenFleeBlocked(SkillKinds.Dungeon));
        Assert.False(FleeRules.IsEscapeWhenFleeBlocked(SkillKinds.Hunt));
    }

    [Fact]
    public void IsDone_WhenDistanceCovered()
    {
        Assert.True(FleeRules.IsDone(threat: 80, coveredTiles: FleeRules.CoveredTiles));
        Assert.False(FleeRules.IsDone(threat: 80, coveredTiles: 1));
    }

    [Fact]
    public void IsDone_ThreatOutOfSightAfterAFewSteps_KeepsRunning()
    {
        // The scan reaches ten tiles. Four steps hid the monster, the flee ended, and the
        // walk home led straight back into it, every second, for hours.
        Assert.False(FleeRules.IsDone(threat: 0, coveredTiles: 4));
        Assert.False(FleeRules.IsDone(threat: ThreatRating.MinThreatToFlee - 1, coveredTiles: FleeRules.SafeGapTiles));
        Assert.True(FleeRules.IsDone(threat: 0, coveredTiles: FleeRules.SafeGapTiles));
    }

    [Fact]
    public void FleeSkill_ShouldGiveUp_AfterFailedLegsOrTime()
    {
        var start = new DateTime(2026, 1, 1, 12, 0, 0);

        Assert.False(FleeSkill.ShouldGiveUp(0, start, start));
        Assert.False(FleeSkill.ShouldGiveUp(FleeSkill.MaxFailedLegs - 1, start, start));
        Assert.True(FleeSkill.ShouldGiveUp(FleeSkill.MaxFailedLegs, start, start));
        Assert.True(FleeSkill.ShouldGiveUp(0, start + FleeSkill.FleeLimit, start));
        Assert.False(FleeSkill.ShouldGiveUp(0, start + FleeSkill.FleeLimit, default));
    }

    [Fact]
    public void KeepsAwayFromParty_AfterRepeatedRuns()
    {
        Assert.False(FleeRules.KeepsAwayFromParty(0));
        Assert.False(FleeRules.KeepsAwayFromParty(FleeRules.RunsBeforeLeavingTrip - 1));
        Assert.True(FleeRules.KeepsAwayFromParty(FleeRules.RunsBeforeLeavingTrip));
    }

    [Fact]
    public void BypassTowardHome_StepsAsideWhenHomeIsSouth()
    {
        var from = new Point3D(1302, 1264, 0);
        var home = new Point3D(1425, 1695, 0);
        var bypass = FleeRules.BypassTowardHome(from, home);

        Assert.Equal(from.X - FleeRules.BypassTiles, bypass.X);
        Assert.Equal(from.Y, bypass.Y);
        Assert.NotEqual(home.X, bypass.X);
    }

    [Fact]
    public void MayAnswerHitWithFlee_NeverWhileFleeingOrAlreadyFightingThem()
    {
        Assert.True(FleeRules.MayAnswerHitWithFlee(fleeing: false, fightingThisAttacker: false));
        Assert.False(FleeRules.MayAnswerHitWithFlee(fleeing: true, fightingThisAttacker: false));
        Assert.False(FleeRules.MayAnswerHitWithFlee(fleeing: false, fightingThisAttacker: true));
        Assert.False(FleeRules.MayAnswerHitWithFlee(fleeing: true, fightingThisAttacker: true));
    }

    [Fact]
    public void ShouldGoHomeAfterRuns_OnlyOutsideTownWhenHurtShortOrChasedOffOften()
    {
        const double Whole = 1.0;
        const double Hurt = 0.4;
        const int OneRun = 1;

        // Town is safe. One run in the wild, whole and stocked: back to work.
        Assert.False(FleeRules.ShouldGoHomeAfterRuns(inTown: true, OneRun, Hurt, suppliesLow: true));
        Assert.False(FleeRules.ShouldGoHomeAfterRuns(inTown: false, recentRuns: 0, Hurt, suppliesLow: true));
        Assert.False(FleeRules.ShouldGoHomeAfterRuns(inTown: false, OneRun, Whole, suppliesLow: false));

        // Hurt, short of supplies, or chased off again and again: walk home.
        Assert.True(FleeRules.ShouldGoHomeAfterRuns(inTown: false, OneRun, Hurt, suppliesLow: false));
        Assert.True(FleeRules.ShouldGoHomeAfterRuns(inTown: false, OneRun, Whole, suppliesLow: true));
        Assert.True(FleeRules.ShouldGoHomeAfterRuns(inTown: false, FleeRules.RunsBeforeGoingHomeAnyway, Whole, suppliesLow: false));
    }

    [Fact]
    public void NewEpisode_OneRunFromOneThreatIsOneDecision()
    {
        const uint mike = 0x100;
        const uint other = 0x101;
        var start = new DateTime(2026, 9, 24, 6, 51, 39);

        Assert.True(FleeRules.NewEpisode(0, default, mike, start));
        Assert.False(FleeRules.NewEpisode(mike, start, mike, start + TimeSpan.FromSeconds(2)));
        Assert.False(FleeRules.NewEpisode(mike, start, mike, start + FleeRules.EpisodeRest - TimeSpan.FromSeconds(1)));
        Assert.True(FleeRules.NewEpisode(mike, start, mike, start + FleeRules.EpisodeRest));
        Assert.True(FleeRules.NewEpisode(mike, start, other, start + TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void NewEpisode_AnHourOfChaseTakesAHandfulOfLines()
    {
        const uint mike = 0x100;
        var start = new DateTime(2026, 9, 24, 6, 51, 39);
        var lastAt = default(DateTime);
        var lines = 0;

        // The red turned and ran every two seconds for an hour: 367 lines before.
        for (var at = start; at < start + TimeSpan.FromHours(1); at += TimeSpan.FromSeconds(2))
        {
            if (FleeRules.NewEpisode(mike, lastAt, mike, at))
            {
                lastAt = at;
                lines++;
            }
        }

        Assert.Equal((int)(TimeSpan.FromHours(1) / FleeRules.EpisodeRest), lines);
    }

    [Fact]
    public void GiveUpWhy_NamesTheFailedLegsOrTheTimeLimit()
    {
        // 249 flees ended "(Failed)" with no word of the cause, 219 of them after one minute.
        Assert.Equal(FleeSkill.LegsFailedWhy, FleeSkill.GiveUpWhy(FleeSkill.MaxFailedLegs));
        Assert.Equal(FleeSkill.TimeUpWhy, FleeSkill.GiveUpWhy(FleeSkill.MaxFailedLegs - 1));
    }
}
