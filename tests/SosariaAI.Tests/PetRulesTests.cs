using System;
using SosariaAI.Combat;
using SosariaAI.Behaviour;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PetRulesTests
{
    private const int FullHits = 100;
    private const int HurtHits = 50;
    private const int ScratchedHits = 90;
    private const int OneStabled = 1;
    private const int NoneStabled = 0;
    private const int LoyaltyHappy = 100;
    private const int DrakeSight = 10;

    [Fact]
    public void KeepsPets_OnlyTamers()
    {
        Assert.True(PetRules.KeepsPets(PersonClass.Tamer));
        Assert.False(PetRules.KeepsPets(PersonClass.Warrior));
        Assert.False(PetRules.KeepsPets(PersonClass.Mage));
    }

    [Fact]
    public void Lags_APetDroppedBackButStillInSight()
    {
        Assert.False(PetRules.Lags(PetRules.PetLagTiles, DrakeSight));
        Assert.True(PetRules.Lags(PetRules.PetLagTiles + 1, DrakeSight));
        Assert.True(PetRules.Lags(DrakeSight, DrakeSight));
        Assert.False(PetRules.Lags(DrakeSight + 1, DrakeSight));
    }

    [Fact]
    public void StillWaits_OnlyUpToTheMaxWait()
    {
        Assert.True(PetRules.StillWaits(TimeSpan.Zero));
        Assert.False(PetRules.StillWaits(PetRules.MaxPetWait));
    }

    [Fact]
    public void GivesUpLostPet_OnceLostLongEnough()
    {
        Assert.False(PetRules.GivesUpLostPet(PetRules.LostPetRelease - TimeSpan.FromSeconds(1), fetching: false));
        Assert.True(PetRules.GivesUpLostPet(PetRules.LostPetRelease, fetching: false));
    }

    [Fact]
    public void GivesUpLostPet_WaitsLongerWhileTheTamerWalksBackForIt()
    {
        // The walk home from Destard left the drake behind; the walk back takes longer than the plain wait.
        Assert.False(PetRules.GivesUpLostPet(PetRules.LostPetRelease, fetching: true));
        Assert.False(PetRules.GivesUpLostPet(PetRules.LostPetFetchRelease - TimeSpan.FromSeconds(1), fetching: true));
        Assert.True(PetRules.GivesUpLostPet(PetRules.LostPetFetchRelease, fetching: true));
    }

    [Fact]
    public void Keywords_AreTheEnginePetKeywords()
    {
        Assert.Equal(0x168, PetRules.AllKillKeyword);
        Assert.Equal(0x16B, PetRules.AllGuardMeKeyword);
        Assert.Equal(0x16C, PetRules.AllFollowMeKeyword);
        Assert.Equal(0x170, PetRules.AllStayKeyword);
        Assert.Equal(0x0009, PetRules.ClaimKeyword);
    }

    private static PetMoment Moment(
        bool inFight = false,
        bool mayAttackFoe = true,
        bool allOnFoe = false,
        bool allGuarding = false,
        bool taming = false,
        bool idleInTown = false,
        bool allFollowing = false,
        bool allStaying = false
    ) =>
        new(inFight, mayAttackFoe, allOnFoe, allGuarding, taming, idleInTown, allFollowing, allStaying);

    [Fact]
    public void Wanted_FightSendsEveryPetAtTheFoe()
    {
        Assert.Equal(PetCommand.Kill, PetRules.Wanted(Moment(inFight: true, allFollowing: true)));
        Assert.Equal(PetCommand.None, PetRules.Wanted(Moment(inFight: true, allOnFoe: true)));
    }

    [Fact]
    public void Wanted_UnderTheGuards_PetsGuardInsteadOfKillingAnInnocent()
    {
        Assert.Equal(PetCommand.Guard, PetRules.Wanted(Moment(inFight: true, mayAttackFoe: false)));
        Assert.Equal(PetCommand.None, PetRules.Wanted(Moment(inFight: true, mayAttackFoe: false, allGuarding: true)));
    }

    [Fact]
    public void MayAttackFoe_InTown_OnlyALawfulTarget()
    {
        Assert.True(PetRules.MayAttackFoe(underGuards: false, Server.Notoriety.Innocent));
        Assert.False(PetRules.MayAttackFoe(underGuards: true, Server.Notoriety.Innocent));
        Assert.True(PetRules.MayAttackFoe(underGuards: true, Server.Notoriety.Murderer));
        Assert.True(PetRules.MayAttackFoe(underGuards: true, Server.Notoriety.CanBeAttacked));
    }

    [Fact]
    public void Wanted_AfterTheFightPetsFollowOrStayInTown()
    {
        Assert.Equal(PetCommand.Follow, PetRules.Wanted(Moment()));
        Assert.Equal(PetCommand.None, PetRules.Wanted(Moment(allFollowing: true)));
        Assert.Equal(PetCommand.Stay, PetRules.Wanted(Moment(idleInTown: true, allFollowing: true)));
        Assert.Equal(PetCommand.None, PetRules.Wanted(Moment(idleInTown: true, allStaying: true)));
    }

    [Fact]
    public void Wanted_PetsStayBackWhileTheOwnerTames()
    {
        // A pet that bites the beast being tamed makes it "too angry" and ends the try.
        Assert.Equal(PetCommand.Stay, PetRules.Wanted(Moment(taming: true, allFollowing: true)));
        Assert.Equal(PetCommand.None, PetRules.Wanted(Moment(taming: true, allStaying: true)));
        Assert.Equal(PetCommand.Kill, PetRules.Wanted(Moment(inFight: true, taming: true, allStaying: true)));
    }

    [Fact]
    public void NeedsVet_HurtOrPoisoned()
    {
        Assert.True(PetRules.NeedsVet(HurtHits, FullHits, poisoned: false));
        Assert.False(PetRules.NeedsVet(ScratchedHits, FullHits, poisoned: false));
        Assert.True(PetRules.NeedsVet(FullHits, FullHits, poisoned: true));
        Assert.False(PetRules.NeedsVet(0, 0, poisoned: false));
    }

    [Fact]
    public void NeedsFeed_BelowTheLoyaltyMark()
    {
        Assert.True(PetRules.NeedsFeed(PetRules.FeedBelowLoyalty - 1));
        Assert.False(PetRules.NeedsFeed(PetRules.FeedBelowLoyalty));
        Assert.False(PetRules.NeedsFeed(LoyaltyHappy));
    }

    [Fact]
    public void NextNeed_ClaimFromTheStablesBeforeTaming()
    {
        Assert.Equal(PetNeed.Claim, Need(OneStabled, stableInReach: true, fighterOut: false, claimDue: true, tameDue: true, wantsMore: false));
        Assert.Equal(PetNeed.None, Need(OneStabled, stableInReach: true, fighterOut: false, claimDue: false, tameDue: true, wantsMore: false));
        Assert.Equal(PetNeed.Tame, Need(NoneStabled, stableInReach: true, fighterOut: false, claimDue: true, tameDue: true, wantsMore: false));
        Assert.Equal(PetNeed.None, Need(NoneStabled, stableInReach: true, fighterOut: false, claimDue: true, tameDue: false, wantsMore: false));
    }

    [Fact]
    public void NextNeed_WithNoStableInReach_TamesInsteadOfWalkingForAClaim()
    {
        // Runa in Jhelom had pets in the stables and no trainer in her leash: sixteen claims failed.
        Assert.Equal(PetNeed.Tame, Need(OneStabled, stableInReach: false, fighterOut: false, claimDue: true, tameDue: true, wantsMore: false));
        Assert.Equal(PetNeed.None, Need(OneStabled, stableInReach: false, fighterOut: false, claimDue: true, tameDue: false, wantsMore: false));
    }

    [Fact]
    public void NextNeed_WithAFighterOut_TamesAgainOnlyForAStrongerBeast()
    {
        // A grandmaster with a dragon (three slots of five) still goes out for a nightmare.
        Assert.Equal(PetNeed.Tame, Need(NoneStabled, stableInReach: false, fighterOut: true, claimDue: true, tameDue: true, wantsMore: true));
        Assert.Equal(PetNeed.None, Need(NoneStabled, stableInReach: false, fighterOut: true, claimDue: true, tameDue: true, wantsMore: false));
        Assert.Equal(PetNeed.None, Need(OneStabled, stableInReach: true, fighterOut: true, claimDue: true, tameDue: false, wantsMore: true));
    }

    [Fact]
    public void NextNeed_WhileAHurtPetRests_WaitsForIt()
    {
        Assert.Equal(PetNeed.None, Need(OneStabled, stableInReach: true, fighterOut: false, claimDue: true, tameDue: true, wantsMore: false, StableHold.Rest));
    }

    [Fact]
    public void NextNeed_WhilePetsWaitForATamingTrip_TamesInsteadOfClaiming()
    {
        // The dragon went in to free the slots for the drakes: claiming it back would fill them again.
        Assert.Equal(PetNeed.Tame, Need(OneStabled, stableInReach: true, fighterOut: false, claimDue: true, tameDue: true, wantsMore: false, StableHold.Taming));
        Assert.Equal(PetNeed.None, Need(OneStabled, stableInReach: true, fighterOut: false, claimDue: true, tameDue: false, wantsMore: false, StableHold.Taming));
    }

    private static PetNeed Need(
        int stabled,
        bool stableInReach,
        bool fighterOut,
        bool claimDue,
        bool tameDue,
        bool wantsMore,
        StableHold hold = StableHold.None
    ) =>
        PetRules.NextNeed(stabled, stableInReach, fighterOut, claimDue, tameDue, wantsMore, hold);

    [Fact]
    public void PetSpell_CureAPoisonedPet_GreaterHealAWoundedOne_HealBelowIt()
    {
        const double Grandmaster = 100;
        const double Apprentice = 40;
        const double NoMagery = 0;
        const int FullMana = 100;
        const int LowMana = 5;

        Assert.Equal(SpellKind.Cure, PetRules.PetSpell(poisoned: true, wounded: true, Grandmaster, FullMana));
        Assert.Equal(SpellKind.GreaterHeal, PetRules.PetSpell(poisoned: false, wounded: true, Grandmaster, FullMana));
        Assert.Equal(SpellKind.Heal, PetRules.PetSpell(poisoned: false, wounded: true, Apprentice, FullMana));
        Assert.Equal(SpellKind.Heal, PetRules.PetSpell(poisoned: false, wounded: true, Grandmaster, LowMana));
        Assert.Null(PetRules.PetSpell(poisoned: false, wounded: false, Grandmaster, FullMana));
        Assert.Null(PetRules.PetSpell(poisoned: false, wounded: true, NoMagery, FullMana));
    }
}
