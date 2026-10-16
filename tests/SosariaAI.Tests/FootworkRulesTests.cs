using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class FootworkRulesTests
{
    private const long JustStarted = 0;
    private const long PastGrace = FootworkRules.KiteGraceMs;
    private const int Adjacent = 1;
    private const int ThreeTiles = 3;
    private const int Reach = 1;
    private const int BowStillMs = FootworkRules.PreAosBowStillMs;
    private const long StillHalfASecond = 500;
    private const long SwingReady = 0;
    private const long SwingIn800 = 800;
    private const int Twice = 2;
    private const int LoneFoe = 1;
    private const long Now = 100000;
    private const long BowDelayMs = 2500;
    private const int KeepMax = 8;
    private const int PastKeepMax = 9;
    private const int SafeLightning = 4;

    [Fact]
    public void Next_TheCastHolds_Acts() =>
        Assert.Equal(Footwork.Act, FootworkRules.Next(true, PastGrace, pinned: true, CombatStance.Press));

    [Fact]
    public void Next_ABlowWouldBreakIt_StepsAwayFirst() =>
        Assert.Equal(Footwork.OpenGap, FootworkRules.Next(false, JustStarted, pinned: false, CombatStance.Press));

    [Fact]
    public void Next_PinnedOrOutpaced_CastsWhereItStands()
    {
        Assert.Equal(Footwork.Commit, FootworkRules.Next(false, JustStarted, pinned: true, CombatStance.Press));
        Assert.Equal(Footwork.Commit, FootworkRules.Next(false, PastGrace, pinned: false, CombatStance.Press));
    }

    [Fact]
    public void Next_AKiterCommitsAfterTheGraceToo_AHolderNeverSteps()
    {
        // A kite that never committed never shot: bots outrun nearly every monster, so the
        // grace never ran out on its own and the archer ran the whole fight.
        Assert.Equal(Footwork.OpenGap, FootworkRules.Next(false, JustStarted, pinned: false, CombatStance.Kite));
        Assert.Equal(Footwork.Commit, FootworkRules.Next(false, PastGrace, pinned: false, CombatStance.Kite));
        Assert.Equal(Footwork.Commit, FootworkRules.Next(false, JustStarted, pinned: false, CombatStance.Hold));
    }

    [Fact]
    public void Fired_TheSwingClockMovedAFullDelayOn()
    {
        const long Before = Now - BowDelayMs;

        Assert.True(FootworkRules.Fired(Before, Now + BowDelayMs, Now));
        Assert.False(FootworkRules.Fired(Before, Now + FootworkRules.HeldShotRetryMs, Now));
        Assert.False(FootworkRules.Fired(Now + BowDelayMs, Now + BowDelayMs, Now));
    }

    [Fact]
    public void PlanCircle_WhatFitsNowInsideTheBand_TheTopCircleOutsideIt()
    {
        var bandMin = FootworkRules.KeepBandTiles;

        Assert.Equal(SafeLightning, FootworkRules.PlanCircle(bandMin, bandMin, KeepMax, SafeLightning));
        Assert.Equal(SafeLightning, FootworkRules.PlanCircle(KeepMax, bandMin, KeepMax, SafeLightning));
        Assert.Equal(CastTiming.TopCircle, FootworkRules.PlanCircle(bandMin - 1, bandMin, KeepMax, SafeLightning));
        Assert.Equal(CastTiming.TopCircle, FootworkRules.PlanCircle(PastKeepMax, bandMin, KeepMax, SafeLightning));
    }

    [Fact]
    public void RetreatSteps_DoubleStepWhenTheFoeIsRightThere()
    {
        Assert.Equal(FootworkRules.DoubleStep, FootworkRules.RetreatSteps(Adjacent));
        Assert.Equal(FootworkRules.DoubleStep, FootworkRules.RetreatSteps(FootworkRules.DoubleStepTiles));
        Assert.Equal(FootworkRules.SingleStep, FootworkRules.RetreatSteps(ThreeTiles));
    }

    [Fact]
    public void IsCrowd_TwoCloseFoesAreAMass()
    {
        Assert.False(FootworkRules.IsCrowd(LoneFoe));
        Assert.True(FootworkRules.IsCrowd(FootworkRules.CrowdCount));
    }

    [Fact]
    public void BreakAwayExpired_AfterTheGraceAndTheStand()
    {
        Assert.False(FootworkRules.BreakAwayExpired(PastGrace));
        Assert.True(FootworkRules.BreakAwayExpired(FootworkRules.KiteGraceMs + FootworkRules.CommitWindowMs));
    }

    [Fact]
    public void TakesWindow_OnlyAStrongSpell()
    {
        Assert.False(FootworkRules.TakesWindow(SpellBook.MagicArrow.Circle));
        Assert.True(FootworkRules.TakesWindow(SpellBook.Fireball.Circle));
    }

    [Fact]
    public void BandMin_KitersAndHealersKeepFurther()
    {
        Assert.Equal(FootworkRules.KeepBandTiles, FootworkRules.BandMin(CombatStance.Press, foeHeld: false));
        Assert.Equal(FootworkRules.KiteBandTiles, FootworkRules.BandMin(CombatStance.Kite, foeHeld: false));
        Assert.Equal(FootworkRules.KiteBandTiles, FootworkRules.BandMin(CombatStance.Heal, foeHeld: false));
        Assert.Equal(FootworkRules.KeepBandTiles, FootworkRules.BandMin(CombatStance.ParalyzeThenKite, foeHeld: false));
        Assert.Equal(FootworkRules.KiteBandTiles, FootworkRules.BandMin(CombatStance.ParalyzeThenKite, foeHeld: true));
    }

    [Fact]
    public void RetryChase_WhenTheFoeMovesOrAfterAWait()
    {
        Assert.True(FootworkRules.RetryChase(foeMoved: true, blockedMs: 0));
        Assert.False(FootworkRules.RetryChase(foeMoved: false, blockedMs: FootworkRules.ChaseRetryMs - 1));
        Assert.True(FootworkRules.RetryChase(foeMoved: false, blockedMs: FootworkRules.ChaseRetryMs));
    }

    [Fact]
    public void GivesUpChase_OnlyALivingFoeLostOnTheSameMap()
    {
        Assert.True(FootworkRules.GivesUpChase(foeAlive: true, sameMap: true, lostPastGrace: true));
        Assert.False(FootworkRules.GivesUpChase(foeAlive: false, sameMap: true, lostPastGrace: true));
        Assert.False(FootworkRules.GivesUpChase(foeAlive: true, sameMap: false, lostPastGrace: true));
        Assert.False(FootworkRules.GivesUpChase(foeAlive: true, sameMap: true, lostPastGrace: false));
    }

    [Fact]
    public void ShouldTurnOnCaster_APersonCastingInReach()
    {
        Assert.True(FootworkRules.ShouldTurnOnCaster(true, true, Reach + FootworkRules.InterruptSlackTiles, Reach));
        Assert.False(FootworkRules.ShouldTurnOnCaster(false, true, Adjacent, Reach));
        Assert.False(FootworkRules.ShouldTurnOnCaster(true, false, Adjacent, Reach));
        Assert.False(FootworkRules.ShouldTurnOnCaster(true, true, ThreeTiles, Reach));
    }

    [Fact]
    public void CanBreakCast_OnlyAPersonWithEnoughWordsLeft()
    {
        var arrow = FootworkRules.ArrowBreakMs(aos: false, slowedByProtection: false);
        var flameStrike = CastTiming.CastDelayMs(SpellBook.FlameStrike.Circle, aos: false, slowedByProtection: false);

        Assert.True(FootworkRules.CanBreakCast(true, flameStrike, arrow));
        Assert.False(FootworkRules.CanBreakCast(false, flameStrike, arrow));
        Assert.False(FootworkRules.CanBreakCast(true, arrow, arrow));
        Assert.False(FootworkRules.CanBreakCast(true, 0, arrow));
    }

    [Fact]
    public void ShotNeedMs_StillnessOrTheSwingClock()
    {
        Assert.Equal(BowStillMs, FootworkRules.ShotNeedMs(BowStillMs, 0, SwingReady));
        Assert.Equal(BowStillMs - StillHalfASecond, FootworkRules.ShotNeedMs(BowStillMs, StillHalfASecond, SwingReady));
        Assert.Equal(SwingIn800, FootworkRules.ShotNeedMs(BowStillMs, BowStillMs, SwingIn800));
        Assert.Equal(0, FootworkRules.ShotNeedMs(BowStillMs, BowStillMs * Twice, SwingReady));
    }

    [Fact]
    public void BowStillMs_ByEra()
    {
        Assert.Equal(FootworkRules.PreAosBowStillMs, FootworkRules.BowStillMs(aos: false, se: false));
        Assert.Equal(FootworkRules.AosBowStillMs, FootworkRules.BowStillMs(aos: true, se: false));
        Assert.Equal(FootworkRules.SeBowStillMs, FootworkRules.BowStillMs(aos: true, se: true));
    }

    [Fact]
    public void ArcherBandMin_KiteKeepsTheWiderBand()
    {
        Assert.Equal(FootworkRules.ArcherKeepMinTiles, FootworkRules.ArcherBandMin(CombatStance.Press));
        Assert.Equal(FootworkRules.ArcherKeepMinTiles, FootworkRules.ArcherBandMin(CombatStance.Hold));
        Assert.Equal(FootworkRules.KiteBandTiles, FootworkRules.ArcherBandMin(CombatStance.Kite));
        Assert.True(FootworkRules.KiteBandTiles <= FootworkRules.ArcherKeepMaxTiles);
    }
}
