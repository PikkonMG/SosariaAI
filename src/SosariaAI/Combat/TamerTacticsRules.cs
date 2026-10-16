using System;
using SosariaAI.Behaviour;

namespace SosariaAI.Combat;

/// <summary>What a tamer does this think in its pets' fight.</summary>
public enum TamerMove
{
    /// <summary>Nothing of the tamer's own: it fights as any caster.</summary>
    None,

    /// <summary>A pet is near death: "all follow me", and the tamer runs with it.</summary>
    SavePet,

    /// <summary>Hidden while its pets fight: it stands still, since a step or a spell shows it again.</summary>
    StayHidden,

    /// <summary>A heal or cure on the hurt pet: a spell from range, or a bandage beside it.</summary>
    HealPet,

    /// <summary>The foe turned on the tamer: Invisibility, so the foe loses it.</summary>
    Vanish,

    /// <summary>The foe turned on the tamer: the Hiding skill, where the foe cannot spot it.</summary>
    Hide,

    /// <summary>The pets go in first: the tamer holds back until one is on the foe.</summary>
    LetPetsLead
}

/// <summary>What the tamer rules read, as plain values.</summary>
public readonly record struct TamerSight(
    bool PetsReady,
    bool PetOnFoe,
    double WeakestPetOnFoe,
    bool PetNeedsCare,
    bool CanTendPet,
    bool FoeOnTamer,
    bool Hidden,
    long HiddenMs,
    bool CanVanish,
    bool CanHide,
    long FightMs
);

/// <summary>
/// A tamer's fight the way the 1999 tamers played it: the pets go in first ("all kill") and
/// the tamer keeps out of the monster's reach. When the monster turns on it, it goes
/// invisible, or hides where the monster cannot spot it, so the monster loses it (a creature
/// drops a combatant it cannot see at its next swing, Mobile.CheckCombatTime, and its AI
/// takes no focus it cannot see, BaseAI.AcquireFocusMob) and goes back to the pets. It stays
/// hidden a moment, then comes out by healing the pet (a cast reveals the caster, Spell.Cast),
/// and steps back out of reach; the next time the monster turns, it does it again. A pet near
/// death is called off and the tamer runs with it: a recall would strand a pet that cannot bond
/// (<see cref="PetKeeper.RecallStrandsPets"/>), so the run is on foot. Live, fresh Master
/// tamers in Destard stood in the dragon's reach with their drakes beside them, retreated
/// "overwhelmed" three times at full hits, and died four minutes in; tamers died more than any
/// other class. Pure.
/// </summary>
public static class TamerTacticsRules
{
    /// <summary>A pet under this share of its hits is near death: it is called off.</summary>
    public const double PetSaveFraction = 0.25;

    /// <summary>The share of hits of an unhurt pet, and what the weakest pet reads with none on the foe.</summary>
    public const double FullPetHits = 1;

    /// <summary>A hidden tamer stays hidden at least this long before it comes out to heal.</summary>
    public const int HideHoldMs = 3000;

    /// <summary>The tamer waits this long at most for a pet to reach the foe before it casts.</summary>
    public const int PetLeadMs = 4000;

    /// <summary>The engine's Hiding fails with a fighter this close in sight (Hiding.OnUse): eight tiles at Grandmaster.</summary>
    public const int HideSpotBaseTiles = 8;

    /// <summary>The engine caps that range at eighteen tiles.</summary>
    public const int HideSpotMaxTiles = 18;

    /// <summary>Each two points of Hiding under the full hundred add a tile to that range.</summary>
    public const double HidingPerTile = 2;

    public const double FullHiding = 100;

    /// <summary>A place is taken on as it is: no slack on the tamer's reach.</summary>
    public const double NoSlack = 1;

    public static TamerMove Next(TamerSight sight)
    {
        if (sight.PetOnFoe && sight.WeakestPetOnFoe < PetSaveFraction)
        {
            return TamerMove.SavePet;
        }

        // Hidden with no pet fit to fight for it, the tamer is a caster alone: its way out is the
        // caster's (MageTacticsRules.Escape). Held here, it stood still for the rest of the fight.
        if (sight.Hidden && sight.PetsReady)
        {
            return sight.HiddenMs >= HideHoldMs && sight.PetNeedsCare && sight.CanTendPet ? TamerMove.HealPet : TamerMove.StayHidden;
        }

        if (!sight.PetsReady)
        {
            return TamerMove.None;
        }

        if (sight.FoeOnTamer)
        {
            return sight.CanVanish ? TamerMove.Vanish
                : sight.CanHide ? TamerMove.Hide
                : TamerMove.None;
        }

        if (sight.PetNeedsCare && sight.CanTendPet)
        {
            return TamerMove.HealPet;
        }

        return !sight.PetOnFoe && sight.FightMs < PetLeadMs ? TamerMove.LetPetsLead : TamerMove.None;
    }

    /// <summary>A pet fit to fight beside its tamer: alive with a quarter of its hits or more.</summary>
    public static bool PetReady(int hits, int hitsMax) => hitsMax > 0 && hits >= hitsMax * PetSaveFraction;

    /// <summary>The share of its hits a pet has left; zero for a body with none.</summary>
    public static double PetHits(int hits, int hitsMax) => hitsMax > 0 ? (double)hits / hitsMax : 0;

    /// <summary>How close a fighter in sight spoils a hide at this Hiding (Hiding.OnUse).</summary>
    public static int HideSpotTiles(double hiding) =>
        Math.Min((int)((FullHiding - hiding) / HidingPerTile) + HideSpotBaseTiles, HideSpotMaxTiles);

    /// <summary>
    /// True when a fighter on the tamer at <paramref name="distance"/> lets it hide: out of its
    /// sight, or farther off than <see cref="HideSpotTiles"/>. Below
    /// <see cref="RecallOutRules.HideOutMinHiding"/> the tries only reveal it.
    /// </summary>
    public static bool HidesFrom(double hiding, int distance, bool inSight) =>
        RecallOutRules.HidesOut(hiding) && (!inSight || distance > HideSpotTiles(hiding));

    /// <summary>
    /// A place a tamer goes to, a dungeon floor or a taming ground, holds nothing stronger than
    /// what the tamer and its pets take on together: its strongest monster within the reach of
    /// their power (<see cref="HuntGround.Reach"/>), times <paramref name="slack"/>.
    /// A floor's rating weighs its common spawn and only a share of its strongest creature
    /// (<see cref="Navigation.Generation.AreaDifficulty.BossShare"/>); a tamer's body is weak and
    /// its power is its pets, so the one dragon that turns on it kills it. Live, a fresh Master
    /// tamer with its drake took Destard's first level at power 549 against a floor of 588, and
    /// a dragon of threat 1392 killed it there.
    /// </summary>
    public static bool TakesOn(int strongestThreat, int power, double slack = NoSlack) =>
        strongestThreat <= HuntGround.Reach(power) * slack;
}
