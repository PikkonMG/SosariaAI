using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A tamer sends its pets in, keeps out of the monster's reach, vanishes or hides when the
/// monster turns on it, comes out by healing its pet, and calls a dying pet off.
/// </summary>
public class TamerTacticsRulesTests
{
    private const double HealthyPet = 0.9;
    private const double DyingPet = 0.1;
    private const long JustHid = 500;
    private const long HidLong = TamerTacticsRules.HideHoldMs + 1;
    private const long FightStart = 0;
    private const long LongFight = TamerTacticsRules.PetLeadMs + 1;
    private const double Grandmaster = 100;
    private const double Adept = 60;
    private const double Untrained = 10;
    private const int Near = 3;
    private const int GrandmasterSpotTiles = 8;
    private const int TamerWithDrake = 549;
    private const int DragonThreat = 1392;
    private const int DrakeThreat = 369;
    private const int NoThreat = 0;
    private const int FullHits = 100;
    private const int QuarterHits = 25;
    private const int NoHitsMax = 0;
    private const double NoShare = 0;

    /// <summary>A tamer with a ready pet on the foe, unhurt, nothing on the tamer, a long way into the fight.</summary>
    private static readonly TamerSight Steady = new(
        PetsReady: true,
        PetOnFoe: true,
        WeakestPetOnFoe: HealthyPet,
        PetNeedsCare: false,
        CanTendPet: false,
        FoeOnTamer: false,
        Hidden: false,
        HiddenMs: 0,
        CanVanish: true,
        CanHide: true,
        FightMs: LongFight
    );

    [Fact]
    public void Next_PetsOnTheFoe_LeavesTheFightToTheCaster() =>
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(Steady));

    [Fact]
    public void Next_DyingPet_IsCalledOffBeforeAnythingElse()
    {
        Assert.Equal(TamerMove.SavePet, TamerTacticsRules.Next(Steady with { WeakestPetOnFoe = DyingPet }));
        Assert.Equal(TamerMove.SavePet, TamerTacticsRules.Next(Steady with { WeakestPetOnFoe = DyingPet, Hidden = true, FoeOnTamer = true }));
    }

    [Fact]
    public void Next_DyingPetOffTheFoe_IsNoReasonToRun() =>
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(Steady with { PetOnFoe = false, WeakestPetOnFoe = DyingPet }));

    [Fact]
    public void Next_FoeTurnsOnTheTamer_VanishesElseHides()
    {
        var turned = Steady with { FoeOnTamer = true };

        Assert.Equal(TamerMove.Vanish, TamerTacticsRules.Next(turned));
        Assert.Equal(TamerMove.Hide, TamerTacticsRules.Next(turned with { CanVanish = false }));
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(turned with { CanVanish = false, CanHide = false }));
    }

    [Fact]
    public void Next_FoeOnATamerWithNoReadyPet_LeavesItToTheCaster() =>
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(Steady with { FoeOnTamer = true, PetsReady = false }));

    [Fact]
    public void Next_Hidden_StaysHiddenAMomentThenComesOutHealing()
    {
        var hidden = Steady with { Hidden = true, PetNeedsCare = true, CanTendPet = true };

        Assert.Equal(TamerMove.StayHidden, TamerTacticsRules.Next(hidden with { HiddenMs = JustHid }));
        Assert.Equal(TamerMove.HealPet, TamerTacticsRules.Next(hidden with { HiddenMs = HidLong }));
        Assert.Equal(TamerMove.StayHidden, TamerTacticsRules.Next(hidden with { HiddenMs = HidLong, PetNeedsCare = false }));
        Assert.Equal(TamerMove.StayHidden, TamerTacticsRules.Next(hidden with { HiddenMs = HidLong, CanTendPet = false }));
    }

    [Fact]
    public void Next_HiddenWithNoReadyPet_LeavesTheWayOutToTheCaster() =>
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(Steady with { Hidden = true, HiddenMs = HidLong, PetsReady = false }));

    [Fact]
    public void Next_HurtPet_IsHealedWhenItCanBe()
    {
        Assert.Equal(TamerMove.HealPet, TamerTacticsRules.Next(Steady with { PetNeedsCare = true, CanTendPet = true }));
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(Steady with { PetNeedsCare = true, CanTendPet = false }));
    }

    [Fact]
    public void Next_PetsNotYetOnTheFoe_GoFirst()
    {
        Assert.Equal(TamerMove.LetPetsLead, TamerTacticsRules.Next(Steady with { PetOnFoe = false, FightMs = FightStart }));
        Assert.Equal(TamerMove.None, TamerTacticsRules.Next(Steady with { PetOnFoe = false, FightMs = LongFight }));
    }

    [Fact]
    public void PetReady_NeedsAQuarterOfItsHits()
    {
        Assert.True(TamerTacticsRules.PetReady(QuarterHits, FullHits));
        Assert.False(TamerTacticsRules.PetReady(QuarterHits - 1, FullHits));
        Assert.False(TamerTacticsRules.PetReady(QuarterHits, NoHitsMax));
    }

    [Fact]
    public void PetHits_IsTheShareLeft()
    {
        Assert.Equal(TamerTacticsRules.PetSaveFraction, TamerTacticsRules.PetHits(QuarterHits, FullHits));
        Assert.Equal(NoShare, TamerTacticsRules.PetHits(QuarterHits, NoHitsMax));
    }

    [Fact]
    public void HideSpotTiles_FollowsTheEngine()
    {
        Assert.Equal(GrandmasterSpotTiles, TamerTacticsRules.HideSpotTiles(Grandmaster));
        Assert.Equal(TamerTacticsRules.HideSpotMaxTiles, TamerTacticsRules.HideSpotTiles(Adept));
    }

    [Fact]
    public void HidesFrom_OutOfSightOrBeyondTheSpottingRange()
    {
        Assert.False(TamerTacticsRules.HidesFrom(Grandmaster, Near, inSight: true));
        Assert.True(TamerTacticsRules.HidesFrom(Grandmaster, GrandmasterSpotTiles + 1, inSight: true));
        Assert.True(TamerTacticsRules.HidesFrom(Grandmaster, Near, inSight: false));
        Assert.False(TamerTacticsRules.HidesFrom(Untrained, Near, inSight: false));
    }

    [Fact]
    public void TakesOn_TheDragonFloorIsPastAFreshTamerAndItsDrake()
    {
        Assert.False(TamerTacticsRules.TakesOn(DragonThreat, TamerWithDrake));
        Assert.True(TamerTacticsRules.TakesOn(DrakeThreat, TamerWithDrake));
        Assert.True(TamerTacticsRules.TakesOn(NoThreat, TamerWithDrake));
        Assert.True(TamerTacticsRules.TakesOn(HuntGround.Reach(TamerWithDrake), TamerWithDrake));
        Assert.False(TamerTacticsRules.TakesOn(HuntGround.Reach(TamerWithDrake) + 1, TamerWithDrake));
    }

    [Fact]
    public void TakesOn_SlackWidensTheReach() =>
        Assert.True(TamerTacticsRules.TakesOn(HuntGround.Reach(TamerWithDrake) + 1, TamerWithDrake, DungeonCrawlRules.StaySlack));
}
