using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class MeleeTacticsRulesTests
{
    private const int OneFoe = 1;
    private const int TwoFoes = 2;
    private const int OpenSlots = 8;
    private const int WallSlots = 5;
    private const int CornerSlots = 3;
    private const int DeadEndSlots = 2;
    private const int NoIdleFoes = 0;
    private const int OneIdleFoe = 1;
    private const int Adjacent = 1;
    private const double Healthy = 0.9;
    private const double BelowHealLine = 0.3;
    private const double AboveHealLine = 0.6;
    private const double HealedUp = 0.85;
    private const double StillHurt = 0.5;
    private const double FoeHurt = 0.4;
    private const double FoeFresh = 0.95;
    private const long JustRan = 10000;
    private const int NoPackmates = 0;
    private const int OnePackmate = 1;

    [Fact]
    public void WeighsWall_SeveralFoesCloseAndFreeFeet()
    {
        Assert.True(MeleeTacticsRules.WeighsWall(TwoFoes, drawing: false, CombatStance.Press));
        Assert.False(MeleeTacticsRules.WeighsWall(OneFoe, drawing: false, CombatStance.Press));
        Assert.False(MeleeTacticsRules.WeighsWall(TwoFoes, drawing: true, CombatStance.Press));
        Assert.False(MeleeTacticsRules.WeighsWall(TwoFoes, drawing: false, CombatStance.Hold));
    }

    [Fact]
    public void BetterStand_FewerSlotsWithTheFoeInReach()
    {
        Assert.True(MeleeTacticsRules.BetterStand(OpenSlots, WallSlots, foeInReach: true, NoIdleFoes, NoIdleFoes));
        Assert.True(MeleeTacticsRules.BetterStand(OpenSlots, CornerSlots, foeInReach: true, NoIdleFoes, NoIdleFoes));
        Assert.False(MeleeTacticsRules.BetterStand(OpenSlots, WallSlots, foeInReach: false, NoIdleFoes, NoIdleFoes));
    }

    [Fact]
    public void BetterStand_NoShuffleNoDeadEndNoIdleHostile()
    {
        Assert.False(MeleeTacticsRules.BetterStand(OpenSlots, OpenSlots - 1, foeInReach: true, NoIdleFoes, NoIdleFoes));
        Assert.False(MeleeTacticsRules.BetterStand(OpenSlots, DeadEndSlots, foeInReach: true, NoIdleFoes, NoIdleFoes));
        Assert.False(MeleeTacticsRules.BetterStand(OpenSlots, WallSlots, foeInReach: true, NoIdleFoes, OneIdleFoe));
        Assert.True(MeleeTacticsRules.BetterStand(OpenSlots, WallSlots, foeInReach: true, OneIdleFoe, OneIdleFoe));
    }

    [Fact]
    public void StepsOutToHeal_LosingWithABandageOnInEveryEra()
    {
        Assert.True(StepsOut(everyBlowSlips: true, Healthy, FightOutlook.Losing));
        Assert.True(StepsOut(everyBlowSlips: false, Healthy, FightOutlook.Losing));
        Assert.False(StepsOut(everyBlowSlips: true, Healthy, FightOutlook.Losing, bandageOn: false));
    }

    [Fact]
    public void StepsOutToHeal_BelowTheLineOnlyWhereEveryBlowSlips()
    {
        Assert.True(StepsOut(everyBlowSlips: true, BelowHealLine, FightOutlook.Even));
        Assert.False(StepsOut(everyBlowSlips: false, BelowHealLine, FightOutlook.Even));
        Assert.False(StepsOut(everyBlowSlips: true, AboveHealLine, FightOutlook.Even));
    }

    [Fact]
    public void StepsOutToHeal_OnlyFromAFoeUpCloseWithGroundToStepOn()
    {
        Assert.False(StepsOut(everyBlowSlips: true, BelowHealLine, FightOutlook.Losing, foeHitsFromRange: true));
        Assert.False(StepsOut(everyBlowSlips: true, BelowHealLine, FightOutlook.Losing, pinned: true));
    }

    [Fact]
    public void StepsBack_WhileAFoeIsNear_StandsPastIt()
    {
        // Out of reach after one step, the fighter walked back in and the next blow slipped the bandage.
        Assert.True(MeleeTacticsRules.StepsBack(Adjacent));
        Assert.True(MeleeTacticsRules.StepsBack(MeleeTacticsRules.HealOutTiles));
        Assert.False(MeleeTacticsRules.StepsBack(MeleeTacticsRules.HealOutTiles + 1));
        Assert.False(MeleeTacticsRules.StepsBack(RoomSurvey.NoDistance));
    }

    [Fact]
    public void ComesBackFor_ALoneHurtMonsterOnceHealed()
    {
        Assert.True(ComesBack(HealedUp, FoeHurt));
        Assert.False(ComesBack(StillHurt, FoeHurt));
        Assert.False(ComesBack(HealedUp, FoeFresh));
    }

    [Fact]
    public void ComesBackFor_NeverAPersonAPackACalmedOrHarmlessFoe()
    {
        Assert.False(ComesBack(HealedUp, FoeHurt, isPerson: true));
        Assert.False(ComesBack(HealedUp, FoeHurt, packmates: OnePackmate));
        Assert.False(ComesBack(HealedUp, FoeHurt, calmed: true));
        Assert.False(ComesBack(HealedUp, FoeHurt, harmless: true));
    }

    [Fact]
    public void ComesBackFor_OnlyWhileTheGroundIsAvoided()
    {
        Assert.True(ComesBack(HealedUp, FoeHurt, sinceRunMs: RetreatRules.AvoidMs - 1));
        Assert.False(ComesBack(HealedUp, FoeHurt, sinceRunMs: RetreatRules.AvoidMs));
        Assert.False(ComesBack(HealedUp, FoeHurt, sinceRunMs: -1));
    }

    private static bool StepsOut(
        bool everyBlowSlips,
        double hitsFraction,
        FightOutlook outlook,
        bool bandageOn = true,
        bool foeHitsFromRange = false,
        bool pinned = false
    ) =>
        MeleeTacticsRules.StepsOutToHeal(bandageOn, everyBlowSlips, hitsFraction, outlook, foeHitsFromRange, pinned);

    private static bool ComesBack(
        double hitsFraction,
        double foeHitsFraction,
        bool isPerson = false,
        bool harmless = false,
        bool calmed = false,
        int packmates = NoPackmates,
        long sinceRunMs = JustRan
    ) =>
        MeleeTacticsRules.ComesBackFor(isPerson, harmless, calmed, packmates, hitsFraction, foeHitsFraction, sinceRunMs);
}
