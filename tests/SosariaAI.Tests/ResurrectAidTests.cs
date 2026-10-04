using System;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class ResurrectAidTests
{
    private const double Grandmaster = 100;
    private const double Journeyman = 60;
    private const int FullMana = 100;
    private const int LowMana = 20;
    private const int NoSlips = 0;
    private const int TwoSlips = 2;
    private const int OldMaxAttempts = 3;
    private const int BandageRaiseSeconds = 10;
    private const string Casimir = "Casimir";
    private const string Oswald = "Oswald the Red";

    [Fact]
    public void MethodFor_MageWithBookReagentsAndMana_Casts() =>
        Assert.Equal(
            AidMethod.Spell,
            ResurrectAid.MethodFor(Grandmaster, FullMana, knowsSpell: true, hasReagents: true, Grandmaster, Grandmaster, hasBandage: true)
        );

    [Fact]
    public void MethodFor_MageWithoutReagents_FallsBackToBandage() =>
        Assert.Equal(
            AidMethod.Bandage,
            ResurrectAid.MethodFor(Grandmaster, FullMana, knowsSpell: true, hasReagents: false, Grandmaster, Grandmaster, hasBandage: true)
        );

    [Fact]
    public void MethodFor_LowManaOrLowMagery_CannotCast()
    {
        Assert.Equal(
            AidMethod.None,
            ResurrectAid.MethodFor(Grandmaster, LowMana, knowsSpell: true, hasReagents: true, 0, 0, hasBandage: false)
        );
        Assert.Equal(
            AidMethod.None,
            ResurrectAid.MethodFor(Journeyman, FullMana, knowsSpell: true, hasReagents: true, 0, 0, hasBandage: false)
        );
    }

    [Fact]
    public void MethodFor_BandageNeedsHealingAndAnatomyEighty()
    {
        Assert.Equal(
            AidMethod.Bandage,
            ResurrectAid.MethodFor(0, 0, false, false, ResurrectAid.HealingMin, ResurrectAid.AnatomyMin, hasBandage: true)
        );
        Assert.Equal(
            AidMethod.None,
            ResurrectAid.MethodFor(0, 0, false, false, Grandmaster, Journeyman, hasBandage: true)
        );
        Assert.Equal(
            AidMethod.None,
            ResurrectAid.MethodFor(0, 0, false, false, Grandmaster, Grandmaster, hasBandage: false)
        );
    }

    [Fact]
    public void Willing_NeverTheKiller_NeverAWantedGhostUnderGuards()
    {
        Assert.False(ResurrectAid.Willing(false, false, helperIsFoe: true, false, bonded: true, DispositionKind.Lawful, asked: true));
        Assert.False(ResurrectAid.Willing(true, true, false, fallenWantedUnderGuards: true, bonded: true, DispositionKind.Outlaw, asked: true));
    }

    [Fact]
    public void WantedUnderGuards_ARedOrACriminal_OnlyUnderTheGuards()
    {
        Assert.True(ResurrectAid.WantedUnderGuards(criminal: false, murderer: true, underGuards: true));
        Assert.True(ResurrectAid.WantedUnderGuards(criminal: true, murderer: false, underGuards: true));
        Assert.False(ResurrectAid.WantedUnderGuards(criminal: false, murderer: false, underGuards: true));
        Assert.False(ResurrectAid.WantedUnderGuards(criminal: true, murderer: true, underGuards: false));
    }

    [Fact]
    public void Willing_RedLineHoldsUnlessBonded()
    {
        Assert.False(ResurrectAid.Willing(helperIsRed: false, fallenIsRed: true, false, false, bonded: false, DispositionKind.Lawful, asked: true));
        Assert.False(ResurrectAid.Willing(helperIsRed: true, fallenIsRed: false, false, false, bonded: false, DispositionKind.Outlaw, asked: true));
        Assert.True(ResurrectAid.Willing(helperIsRed: true, fallenIsRed: false, false, false, bonded: true, DispositionKind.Outlaw, asked: false));
    }

    [Fact]
    public void Willing_StrangerByDisposition()
    {
        Assert.True(ResurrectAid.Willing(false, false, false, false, bonded: false, DispositionKind.Lawful, asked: false));
        Assert.False(ResurrectAid.Willing(false, false, false, false, bonded: false, DispositionKind.Neutral, asked: false));
        Assert.True(ResurrectAid.Willing(false, false, false, false, bonded: false, DispositionKind.Neutral, asked: true));
        Assert.False(ResurrectAid.Willing(false, false, false, false, bonded: false, DispositionKind.Outlaw, asked: true));
        Assert.True(ResurrectAid.Willing(true, true, false, false, bonded: false, DispositionKind.Outlaw, asked: true));
    }

    [Fact]
    public void BandageChance_MatchesTheEngineFormula()
    {
        Assert.Equal(0.64, ResurrectAid.BandageChance(Grandmaster, NoSlips), 6);
        Assert.Equal(0.60, ResurrectAid.BandageChance(Grandmaster, TwoSlips), 6);
    }

    [Fact]
    public void BandageRaises_NeedsSkillsAndABetterRoll()
    {
        Assert.True(ResurrectAid.BandageRaises(Grandmaster, Grandmaster, NoSlips, roll: 0.5));
        Assert.False(ResurrectAid.BandageRaises(Grandmaster, Grandmaster, NoSlips, roll: 0.9));
        Assert.False(ResurrectAid.BandageRaises(Grandmaster, Journeyman, NoSlips, roll: 0.0));
    }

    [Fact]
    public void Hindrance_AFoeOnTheHelperFirst_ThenTheKillerAtTheBody_ThenTheGuards()
    {
        Assert.Equal(
            ResurrectAid.AttackedWhy(Casimir),
            ResurrectAid.Hindrance(Casimir, killerNearGhost: true, fallenWantedUnderGuards: true)
        );
        Assert.Equal(
            ResurrectAid.KillerNearWhy,
            ResurrectAid.Hindrance(null, killerNearGhost: true, fallenWantedUnderGuards: true)
        );
        Assert.Null(ResurrectAid.Hindrance(null, killerNearGhost: false, fallenWantedUnderGuards: false));
        Assert.Contains(Casimir, ResurrectAid.AttackedWhy(Casimir));
    }

    /// <summary>
    /// The guard rule was asked only when help began: a red ghost that walked in under the
    /// guards while its helper came over could be raised there. The raise stops at the next step.
    /// </summary>
    [Fact]
    public void Hindrance_AGhostWantedUnderTheGuards_StopsTheRaise() =>
        Assert.Equal(
            ResurrectAid.WantedUnderGuardsWhy,
            ResurrectAid.Hindrance(null, killerNearGhost: false, fallenWantedUnderGuards: true)
        );

    [Fact]
    public void HandsBusy_WaitsForTheCastCursorOrBandage_AndRecoveryOnlyHoldsASpell()
    {
        Assert.True(ResurrectAid.HandsBusy(casting: true, false, false, false, AidMethod.Bandage));
        Assert.True(ResurrectAid.HandsBusy(false, cursorUp: true, false, false, AidMethod.Spell));
        Assert.True(ResurrectAid.HandsBusy(false, false, bandaging: true, false, AidMethod.Bandage));
        Assert.True(ResurrectAid.HandsBusy(false, false, false, castRecovering: true, AidMethod.Spell));
        Assert.False(ResurrectAid.HandsBusy(false, false, false, castRecovering: true, AidMethod.Bandage));
        Assert.False(ResurrectAid.HandsBusy(false, false, false, false, AidMethod.Spell));
    }

    [Fact]
    public void FailLine_NamesTheGhostThatStayedDead()
    {
        var line = ResurrectAid.FailLine(ResurrectAid.TriesSpentWhy(ResurrectAidSkill.MaxAttempts), Oswald);

        Assert.StartsWith(ResurrectAid.TriesSpentWhy(ResurrectAidSkill.MaxAttempts), line);
        Assert.EndsWith($"({Oswald})", line);
        Assert.Equal(ResurrectAid.GhostGoneWhy, ResurrectAid.FailLine(ResurrectAid.GhostGoneWhy, null));
    }

    [Fact]
    public void MaxAttempts_FitInsideTheGiveUpAtABandagesPace()
    {
        // A bandage raise takes up to ten seconds, then the gap before the next try.
        var bandageTry = TimeSpan.FromSeconds(BandageRaiseSeconds) + ResurrectAidSkill.AttemptGap;

        Assert.True(ResurrectAidSkill.MaxAttempts > OldMaxAttempts);
        Assert.True(bandageTry * ResurrectAidSkill.MaxAttempts < ResurrectAidSkill.GiveUpAfter);
    }

    [Fact]
    public void SkilledToRaise_MageryAloneOrHealingWithAnatomy()
    {
        Assert.True(ResurrectAid.SkilledToRaise(ResurrectAid.MageryMin, 0, 0));
        Assert.True(ResurrectAid.SkilledToRaise(0, ResurrectAid.HealingMin, ResurrectAid.AnatomyMin));
        Assert.False(ResurrectAid.SkilledToRaise(Journeyman, Grandmaster, Journeyman));
    }

    [Fact]
    public void InPartyReach_OnlyAFallenMateTheHelperCanWalkTo()
    {
        // Bran walked for Sela's ghost 58 times from Britain while she stood dead at the Den.
        const int DenFromBritain = 1270;

        Assert.True(ResurrectAid.InPartyReach(sameMap: true, ResurrectAid.PartyAidRange));
        Assert.False(ResurrectAid.InPartyReach(sameMap: true, ResurrectAid.PartyAidRange + 1));
        Assert.False(ResurrectAid.InPartyReach(sameMap: true, DenFromBritain));
        Assert.False(ResurrectAid.InPartyReach(sameMap: false, 0));
    }

    [Theory]
    [InlineData(DispositionKind.Outlaw)]
    [InlineData(DispositionKind.Neutral)]
    [InlineData(DispositionKind.Lawful)]
    public void Willing_ARedRaisesARedGhostUnasked(DispositionKind disposition)
    {
        // The owner: reds help reds up.
        Assert.True(ResurrectAid.Willing(helperIsRed: true, fallenIsRed: true, false, false, bonded: false, disposition, asked: false));
    }

    [Fact]
    public void Willing_ARedStillRaisesNoRedWhoFellToIt_NorOneWantedUnderGuards()
    {
        Assert.False(ResurrectAid.Willing(true, true, helperIsFoe: true, false, bonded: false, DispositionKind.Outlaw, asked: true));
        Assert.False(ResurrectAid.Willing(true, true, false, fallenWantedUnderGuards: true, bonded: false, DispositionKind.Outlaw, asked: false));
    }

    [Fact]
    public void RedCallRange_AGangMateFromFartherThanTheSightScan_OneLegAtMost()
    {
        Assert.Equal(ResurrectAid.GangAidRange, ResurrectAid.RedCallRange(sameGang: true));
        Assert.Equal(ResurrectAid.GhostSeekRange, ResurrectAid.RedCallRange(sameGang: false));
        Assert.True(ResurrectAid.GangAidRange > ResurrectAid.GhostSeekRange);
        Assert.True(ResurrectAid.GangAidRange <= NavLimits.SoftLegDistance);
    }

    [Fact]
    public void CallsBefore_AGangMateFirst_ThenTheNearer()
    {
        const int Near = 5;
        const int Far = 30;

        Assert.True(ResurrectAid.CallsBefore(sameGang: true, Far, bestSameGang: false, Near));
        Assert.False(ResurrectAid.CallsBefore(sameGang: false, Near, bestSameGang: true, Far));
        Assert.True(ResurrectAid.CallsBefore(sameGang: true, Near, bestSameGang: true, Far));
        Assert.False(ResurrectAid.CallsBefore(sameGang: false, Far, bestSameGang: false, Near));
        Assert.True(ResurrectAid.CallsBefore(sameGang: false, Far, bestSameGang: false, int.MaxValue));
    }
}
