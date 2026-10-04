using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class RetreatRulesTests
{
    private const double Tolerance = 1e-9;
    private const double BoldNerve = 1.5;
    private const double TimidNerve = 0.6;
    private const double PlainNerve = 1.0;
    private const double RecklessNerve = 50;
    private const int OneAttacker = 1;
    private const int ThreeAttackers = 3;
    private const int ManyAttackers = 20;
    private const int Dare = 100;
    private const double Multiple = 1.0;
    private const int SmallRoom = 150;
    private const int HugeRoom = 250;
    private const int EnormousRoom = 500;
    private const int TwoExtras = 2;
    private const double Healthy = 0.9;
    private const double Hurt = 0.3;
    private const double FoeNearlyDead = 0.2;
    private const double FoeHealthy = 0.9;
    private const int NoTurns = 0;
    private const double HalfHits = 0.5;
    private const double UnderFloor = 0.2;
    private const double WorkerOneFoeCeiling = 0.6;
    private const long RunAt = 100000;
    private const long NeverRan = 0;

    [Fact]
    public void BaseLine_WorkerLeavesSoonest_VeteranLatest()
    {
        Assert.Equal(RetreatRules.WorkerLine, RetreatRules.BaseLine(CharacterRole.Worker, veteran: true));
        Assert.Equal(RetreatRules.FighterLine, RetreatRules.BaseLine(CharacterRole.Fighter, veteran: false));
        Assert.Equal(RetreatRules.VeteranLine, RetreatRules.BaseLine(CharacterRole.Fighter, veteran: true));
    }

    [Fact]
    public void Line_OneAttacker_IsBaseOverNerve() =>
        Assert.Equal(
            RetreatRules.FighterLine / BoldNerve,
            RetreatRules.Line(RetreatRules.FighterLine, BoldNerve, OneAttacker),
            Tolerance
        );

    [Fact]
    public void Line_TimidLeavesEarlierThanBold() =>
        Assert.True(
            RetreatRules.Line(RetreatRules.FighterLine, TimidNerve, OneAttacker) >
            RetreatRules.Line(RetreatRules.FighterLine, BoldNerve, OneAttacker)
        );

    [Fact]
    public void Line_EachExtraAttackerRaisesIt()
    {
        var single = RetreatRules.Line(RetreatRules.FighterLine, PlainNerve, OneAttacker);
        var three = RetreatRules.Line(RetreatRules.FighterLine, PlainNerve, ThreeAttackers);

        Assert.Equal(single + TwoExtras * RetreatRules.ExtraAttackerRaise, three, Tolerance);
    }

    [Fact]
    public void Line_StaysInsideBounds()
    {
        Assert.Equal(RetreatRules.MaxLine, RetreatRules.Line(RetreatRules.WorkerLine, TimidNerve, ManyAttackers), Tolerance);
        Assert.Equal(RetreatRules.MinLine, RetreatRules.Line(RetreatRules.VeteranLine, RecklessNerve, OneAttacker), Tolerance);
    }

    [Fact]
    public void StartLine_SitsAboveTheRetreatLine() =>
        Assert.Equal(
            RetreatRules.Line(RetreatRules.FighterLine, PlainNerve, OneAttacker) + RetreatRules.StartMargin,
            RetreatRules.StartLine(RetreatRules.FighterLine, PlainNerve, OneAttacker),
            Tolerance
        );

    [Fact]
    public void IsOutnumbered_NeedsTwoAttackersAndAHugeRoom()
    {
        Assert.False(RetreatRules.IsOutnumbered(OneAttacker, EnormousRoom, Dare, Multiple));
        Assert.False(RetreatRules.IsOutnumbered(ThreeAttackers, SmallRoom, Dare, Multiple));
        Assert.True(RetreatRules.IsOutnumbered(ThreeAttackers, HugeRoom, Dare, Multiple));
    }

    [Fact]
    public void PackCaughtUp_TwoCloseAndBeyondDare() =>
        Assert.True(RetreatRules.PackCaughtUp(ThreeAttackers, SmallRoom, Dare, Multiple));

    [Fact]
    public void IsGambling_BoldOneOnOneAgainstALosingFoe()
    {
        Assert.True(RetreatRules.IsGambling(BoldNerve, OneAttacker, Hurt, FoeNearlyDead));
        Assert.False(RetreatRules.IsGambling(PlainNerve, OneAttacker, Hurt, FoeNearlyDead));
        Assert.False(RetreatRules.IsGambling(BoldNerve, ThreeAttackers, Hurt, FoeNearlyDead));
        Assert.False(RetreatRules.IsGambling(BoldNerve, OneAttacker, Hurt, FoeHealthy));
    }

    [Fact]
    public void ShouldRetreat_BelowLine_OrOutnumbered_UnlessGambling()
    {
        var line = RetreatRules.FighterLine;

        foreach (var outlook in new[] { FightOutlook.Unknown, FightOutlook.Even, FightOutlook.Losing })
        {
            Assert.True(RetreatRules.ShouldRetreat(Hurt, line, outnumbered: false, gambling: false, outlook));
            Assert.False(RetreatRules.ShouldRetreat(Hurt, line, outnumbered: false, gambling: true, outlook));
            Assert.False(RetreatRules.ShouldRetreat(Healthy, line, outnumbered: false, gambling: false, outlook));
            Assert.True(RetreatRules.ShouldRetreat(Healthy, line, outnumbered: true, gambling: true, outlook));
        }
    }

    [Fact]
    public void ShouldRetreat_AWinningTradeHoldsThroughTheNumbersAndTheRaisedLine()
    {
        // Three orcs on him raise his line to 0.6, and the room counts as outnumbering him,
        // but he kills them faster than they hurt him: he stays.
        var threeOrcLine = RetreatRules.Line(RetreatRules.FighterLine, PlainNerve, ThreeAttackers);

        Assert.False(RetreatRules.ShouldRetreat(HalfHits, threeOrcLine, outnumbered: true, gambling: false, FightOutlook.Winning));
        Assert.True(RetreatRules.ShouldRetreat(HalfHits, threeOrcLine, outnumbered: true, gambling: false, FightOutlook.Losing));
        Assert.True(RetreatRules.ShouldRetreat(UnderFloor, threeOrcLine, outnumbered: false, gambling: false, FightOutlook.Winning));
        Assert.False(RetreatRules.ShouldRetreat(UnderFloor, threeOrcLine, outnumbered: false, gambling: true, FightOutlook.Winning));
    }

    [Fact]
    public void WouldLeaveAtOnce_OutnumberedOverwhelmedOrUnderTheStartLine()
    {
        var startLine = RetreatRules.StartLine(RetreatRules.FighterLine, PlainNerve, ThreeAttackers);

        Assert.True(RetreatRules.WouldLeaveAtOnce(Healthy, startLine, outnumbered: true, overwhelmed: false));
        Assert.True(RetreatRules.WouldLeaveAtOnce(Healthy, startLine, outnumbered: false, overwhelmed: true));
        Assert.True(RetreatRules.WouldLeaveAtOnce(startLine - Tolerance, startLine, outnumbered: false, overwhelmed: false));
    }

    [Fact]
    public void WouldLeaveAtOnce_FitAndNotOutmatched_Starts() =>
        Assert.False(
            RetreatRules.WouldLeaveAtOnce(
                Healthy,
                RetreatRules.StartLine(RetreatRules.FighterLine, PlainNerve, OneAttacker),
                outnumbered: false,
                overwhelmed: false
            )
        );

    [Fact]
    public void Line_ATimidWorkerStillFightsAScratch()
    {
        // Tobiah, a worker at the lowest nerve, left a skeleton at 70 of 74 hits.
        var line = RetreatRules.Line(RetreatRules.WorkerLine, NerveRules.MinNerve, OneAttacker);

        Assert.Equal(RetreatRules.MaxOneFoeLine, line, Tolerance);
        Assert.True(line <= WorkerOneFoeCeiling);
        Assert.True(RetreatRules.Line(RetreatRules.WorkerLine, NerveRules.MinNerve, ThreeAttackers) > line);
    }

    [Fact]
    public void IsClear_NeedsThePackAndTheSourceWellOff()
    {
        Assert.True(RetreatRules.IsClear(RetreatRules.ClearTiles, RetreatRules.ClearTiles, hunted: false));
        Assert.True(RetreatRules.IsClear(RoomSurvey.NoDistance, RoomSurvey.NoDistance, hunted: false));
        Assert.False(RetreatRules.IsClear(RetreatRules.ClearTiles - 1, RoomSurvey.NoDistance, hunted: false));
        Assert.False(RetreatRules.IsClear(RoomSurvey.NoDistance, RetreatRules.ClearTiles - 1, hunted: false));
    }

    [Fact]
    public void IsClear_AHuntedRunnerGoesFarther()
    {
        Assert.False(RetreatRules.IsClear(RetreatRules.ClearTiles, RetreatRules.ClearTiles, hunted: true));
        Assert.True(RetreatRules.IsClear(RetreatRules.HuntedClearTiles, RetreatRules.HuntedClearTiles, hunted: true));
        Assert.True(RetreatRules.HuntedClearTiles > RetreatRules.ClearTiles);
    }

    [Fact]
    public void IsHunted_TheSameThingAgainInsideAMinute()
    {
        Assert.True(RetreatRules.IsHunted(sameSource: true, RunAt, RunAt + RetreatRules.HuntedWindowMs - 1));
        Assert.False(RetreatRules.IsHunted(sameSource: true, RunAt, RunAt + RetreatRules.HuntedWindowMs));
        Assert.False(RetreatRules.IsHunted(sameSource: false, RunAt, RunAt + 1));
        Assert.False(RetreatRules.IsHunted(sameSource: true, NeverRan, RunAt));
    }

    [Fact]
    public void StandsFast_OnlyAHuntedRunnerWithNowhereLeftToGo()
    {
        Assert.True(RetreatRules.StandsFast(hunted: true, cornered: true));
        Assert.False(RetreatRules.StandsFast(hunted: false, cornered: true));
        Assert.False(RetreatRules.StandsFast(hunted: true, cornered: false));
        Assert.False(RetreatRules.StandsFast(hunted: false, cornered: false));
    }

    [Fact]
    public void AvoidForMs_HuntedStaysAwayLonger() =>
        Assert.True(RetreatRules.AvoidForMs(hunted: true) > RetreatRules.AvoidForMs(hunted: false));

    [Fact]
    public void Avoids_OnlyAFreshFightOnTheGroundInsideTheWindow()
    {
        var until = RunAt + RetreatRules.AvoidMs;

        Assert.True(RetreatRules.Avoids(until, RunAt, RetreatRules.AvoidRadiusTiles, alreadyInFight: false));
        Assert.False(RetreatRules.Avoids(until, RunAt, RetreatRules.AvoidRadiusTiles, alreadyInFight: true));
        Assert.False(RetreatRules.Avoids(until, RunAt, RetreatRules.AvoidRadiusTiles + 1, alreadyInFight: false));
        Assert.False(RetreatRules.Avoids(until, until, RetreatRules.AvoidRadiusTiles, alreadyInFight: false));
    }

    [Fact]
    public void ShouldTurnOnChaser_AllConditionsNeeded()
    {
        Assert.True(RetreatRules.ShouldTurnOnChaser(NoTurns, false, true, true, true));
        Assert.False(RetreatRules.ShouldTurnOnChaser(RetreatRules.MaxTurns, false, true, true, true));
        Assert.False(RetreatRules.ShouldTurnOnChaser(NoTurns, true, true, true, true));
        Assert.False(RetreatRules.ShouldTurnOnChaser(NoTurns, false, false, true, true));
        Assert.False(RetreatRules.ShouldTurnOnChaser(NoTurns, false, true, false, true));
        Assert.False(RetreatRules.ShouldTurnOnChaser(NoTurns, false, true, true, false));
    }
}
