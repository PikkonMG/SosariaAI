using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class StanceRulesTests
{
    private const double Healthy = 1.0;
    private const double BadlyHurt = 0.3;
    private const double FullMana = 1.0;
    private const double NoMana = 0.05;
    private const int Adjacent = 1;
    private const int Strong = 500;
    private const int Weak = 50;
    private const int Dare = 100;
    private const int SlightlyStronger = 110;

    [Fact]
    public void Pick_BadlyHurtWithARemedy_Heals()
    {
        Assert.Equal(CombatStance.Heal, StanceRules.Pick(Mage() with { HitsFraction = BadlyHurt, NeedsCare = true }));
        Assert.Equal(
            CombatStance.Heal,
            StanceRules.Pick(Warrior() with { HitsFraction = BadlyHurt, NeedsCare = true, HasBandage = true })
        );
    }

    [Fact]
    public void Pick_BadlyHurtWithNothingAtHand_KeepsFighting() =>
        Assert.Equal(
            CombatStance.Press,
            StanceRules.Pick(Warrior() with { HitsFraction = BadlyHurt, NeedsCare = true })
        );

    [Fact]
    public void Pick_SpellsKeepBreaking_PutsUpTheWard()
    {
        var broken = Mage() with { RecentInterrupts = StanceRules.WardAfterInterrupts };

        Assert.Equal(CombatStance.Protect, StanceRules.Pick(broken));
        Assert.NotEqual(CombatStance.Protect, StanceRules.Pick(broken with { Warded = true }));
    }

    [Fact]
    public void Pick_WordsBreakThroughTheWard_KitesForRoom()
    {
        // With the ward up a mage pressed on and cast into the same blows again and again.
        var broken = Mage() with { RecentInterrupts = StanceRules.WardAfterInterrupts, Warded = true };

        Assert.Equal(CombatStance.Kite, StanceRules.Pick(broken));
        Assert.Equal(CombatStance.Kite, StanceRules.Pick(broken with { Warded = false, CanWard = false }));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(broken with { Cornered = true }));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(broken with { RecentInterrupts = StanceRules.WardAfterInterrupts - 1 }));
    }

    [Fact]
    public void Pick_TankMageWithBrokenWords_SwingsInsteadOfAWardItCannotCast()
    {
        var tank = Mage() with { TankMage = true, Style = CombatStyle.Melee, RecentInterrupts = StanceRules.WardAfterInterrupts };

        Assert.Equal(CombatStance.Press, StanceRules.Pick(tank));
    }

    [Fact]
    public void KeepsBreaking_AtTheWardCount()
    {
        Assert.False(StanceRules.KeepsBreaking(StanceRules.WardAfterInterrupts - 1));
        Assert.True(StanceRules.KeepsBreaking(StanceRules.WardAfterInterrupts));
    }

    [Fact]
    public void Pick_AMeleeFoeThatWillNotBeShaken_ParalyzeThenKite()
    {
        var caught = Mage() with { AdjacentFoes = Adjacent, Sticky = true };

        Assert.Equal(CombatStance.ParalyzeThenKite, StanceRules.Pick(caught));
        Assert.NotEqual(CombatStance.ParalyzeThenKite, StanceRules.Pick(caught with { Sticky = false }));
        Assert.NotEqual(CombatStance.ParalyzeThenKite, StanceRules.Pick(caught with { FoeStyle = CombatStyle.Mage }));
        Assert.NotEqual(CombatStance.ParalyzeThenKite, StanceRules.Pick(caught with { CanParalyze = false }));
    }

    [Fact]
    public void Pick_AFrozenMeleeFoe_IsWalkedAwayFromBeforeTheBigSpell()
    {
        var frozen = Mage() with { FoeHeld = true, CanParalyze = false };

        Assert.Equal(CombatStance.ParalyzeThenKite, StanceRules.Pick(frozen));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(frozen with { FoeDistance = FootworkRules.KiteBandTiles }));
        Assert.True(StanceRules.Feasible(CombatStance.ParalyzeThenKite, frozen));
    }

    [Fact]
    public void Pick_OutOfMana_KitesWhileItComesBack() =>
        Assert.Equal(CombatStance.Kite, StanceRules.Pick(Mage() with { ManaFraction = NoMana }));

    [Fact]
    public void Pick_Otherwise_Presses()
    {
        Assert.Equal(CombatStance.Press, StanceRules.Pick(Mage()));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(Mage() with { AdjacentFoes = Adjacent }));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(Warrior() with { Style = CombatStyle.Archer }));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(Mage() with { TankMage = true, Style = CombatStyle.Melee, Sticky = true }));
    }

    [Fact]
    public void Feasible_OnlyWhatTheCharacterCanDo()
    {
        Assert.False(StanceRules.Feasible(CombatStance.Kite, Warrior()));
        Assert.True(StanceRules.Feasible(CombatStance.Kite, Warrior() with { Style = CombatStyle.Archer }));
        Assert.False(StanceRules.Feasible(CombatStance.Heal, Mage()));
        Assert.False(StanceRules.Feasible(CombatStance.Protect, Mage() with { Warded = true }));
        Assert.False(StanceRules.Feasible(CombatStance.ParalyzeThenKite, Warrior()));
        Assert.True(StanceRules.Feasible(CombatStance.Flee, Warrior()));
        Assert.True(StanceRules.Feasible(CombatStance.Hold, Warrior()));
    }

    [Fact]
    public void MinYes_RiskierStancesNeedClearerYeses()
    {
        Assert.True(StanceRules.MinYes(CombatStance.Flee) > StanceRules.MinYes(CombatStance.Press));
        Assert.True(StanceRules.MinYes(CombatStance.ParalyzeThenKite) > StanceRules.MinYes(CombatStance.Kite));
    }

    [Fact]
    public void WorthAsking_APersonAClearlyStrongerFoeOrUnderHalfHits()
    {
        Assert.True(StanceRules.WorthAsking(foeIsPerson: true, Weak, Dare, Healthy));
        Assert.True(StanceRules.WorthAsking(foeIsPerson: false, Strong, Dare, Healthy));
        Assert.True(StanceRules.WorthAsking(foeIsPerson: false, Weak, Dare, BadlyHurt));
        Assert.False(StanceRules.WorthAsking(foeIsPerson: false, Weak, Dare, Healthy));
        Assert.False(StanceRules.WorthAsking(foeIsPerson: false, SlightlyStronger, Dare, Healthy));
    }

    [Fact]
    public void Cornered_NoKiteIntoTheWall()
    {
        var cornered = Mage() with { ManaFraction = NoMana, Cornered = true };

        Assert.False(StanceRules.Feasible(CombatStance.Kite, cornered));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(cornered));
    }

    [Fact]
    public void BlueOnRed_WinningTheTrade_Presses()
    {
        var winning = Mage() with { ManaFraction = NoMana, BlueOnRed = true, Outlook = FightOutlook.Winning };

        Assert.Equal(CombatStance.Press, StanceRules.Pick(winning));
        Assert.True(StanceRules.PressesRed(winning));
        Assert.False(StanceRules.Feasible(CombatStance.Kite, winning));
        Assert.False(StanceRules.Feasible(CombatStance.Hold, winning));
        Assert.False(StanceRules.Feasible(CombatStance.Flee, winning));
        Assert.False(StanceRules.PressesRed(winning with { BlueOnRed = false }));
    }

    [Fact]
    public void BlueOnRed_KitesOnlyWhenLosing()
    {
        var blue = Mage() with { ManaFraction = NoMana, BlueOnRed = true };

        Assert.False(StanceRules.Feasible(CombatStance.Kite, blue with { Outlook = FightOutlook.Unknown }));
        Assert.False(StanceRules.Feasible(CombatStance.Kite, blue with { Outlook = FightOutlook.Even }));
        Assert.Equal(CombatStance.Press, StanceRules.Pick(blue with { Outlook = FightOutlook.Even }));
        Assert.True(StanceRules.Feasible(CombatStance.Kite, blue with { Outlook = FightOutlook.Losing }));
        Assert.Equal(CombatStance.Kite, StanceRules.Pick(blue with { Outlook = FightOutlook.Losing }));
        Assert.True(StanceRules.Feasible(CombatStance.Flee, blue with { Outlook = FightOutlook.Losing }));
    }

    [Fact]
    public void BlueOnRed_BadlyHurt_StillHeals() =>
        Assert.Equal(
            CombatStance.Heal,
            StanceRules.Pick(Mage() with { BlueOnRed = true, Outlook = FightOutlook.Winning, HitsFraction = BadlyHurt, NeedsCare = true })
        );

    internal static StanceSituation Mage() =>
        new(
            CombatStyle.Mage,
            Caster: true,
            TankMage: false,
            Healthy,
            FullMana,
            Poisoned: false,
            NeedsCare: false,
            FoeIsPerson: true,
            FoeStyle: CombatStyle.Melee,
            FoeHitsFraction: Healthy,
            FoeDistance: Adjacent,
            FoeCasting: false,
            FoeHeld: false,
            AdjacentFoes: 0,
            RecentInterrupts: 0,
            Warded: false,
            CanWard: true,
            CanParalyze: true,
            HasHealPotion: false,
            HasBandage: false,
            CanHealSpell: true,
            AlliesNear: 0,
            Sticky: false,
            Outlook: FightOutlook.Unknown,
            LightDamage: false,
            Cornered: false,
            BlueOnRed: false
        );

    internal static StanceSituation Warrior() =>
        Mage() with
        {
            Style = CombatStyle.Melee,
            Caster = false,
            CanWard = false,
            CanParalyze = false,
            CanHealSpell = false
        };
}
