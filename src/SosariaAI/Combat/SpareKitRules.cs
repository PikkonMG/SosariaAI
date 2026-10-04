using System;
using System.Collections.Generic;
using SosariaAI.Common;
using SosariaAI.Economy;

namespace SosariaAI.Combat;

/// <summary>What a piece of gear does for a build's spare kit; <see cref="None"/> for anything else.</summary>
public enum SpareRoleKind
{
    None,
    Weapon,
    Shield,
    Armor
}

/// <summary>
/// The place a piece fills in a spare kit: the weapon of the build's row, a shield, or the
/// armor of one slot. Two pieces of one role stand in for each other: a looted chain tunic
/// fills the bag's chest as well as the plate one the kit named.
/// </summary>
public readonly record struct SpareRole(SpareRoleKind Kind, GearSlot Slot)
{
    public static readonly SpareRole None = new(SpareRoleKind.None, default);
}

/// <summary>
/// A spare kit: a bag in the bank box holding one of each combat piece of the build
/// (<see cref="KitOnceRules.IsCombatKitPiece"/>), with a reserve of reagents and bandages, the
/// way a 1999 fighter kept a second suit in the bank and fought in cheap gear. A red keeps it
/// at the Den bank; a blue fighter at its own. Stripped by a red, a blue walked the shard half
/// naked: its gold went with the body, and 86 rebuys of two runs found no piece it could pay
/// for. The Den has no
/// mage or alchemist shop, so a red caster's reagents, lost to the corpse, come only from the
/// box (<see cref="Skills.DenStock"/>). Live, no red bought gear
/// after a death, and the reds that got home rode out again naked; 292 runs in one night
/// ended at once with "the reagents or bandages ran short". The bag is packed once; the bag
/// in the box is the saved mark that it was. After a death the red takes the pieces it lacks
/// out at the bank and dresses. The bag fills again three ways: a piece of its build's
/// <see cref="SpareRole"/> kept off a body it strips, a spare bought at a shop out of the
/// guards' reach, or one bought from a crafter at the Den bank. Pure.
/// </summary>
public static class SpareKitRules
{
    /// <summary>The name of the bag in the bank box that holds the spare kit.</summary>
    public const string BagName = "spare kit";

    /// <summary>Restocks the reserve holds: a reagent or bandage pile of this many full stocks.</summary>
    public const int ReserveRestocks = 3;

    /// <summary>Of each reagent the build uses, the bag holds this many.</summary>
    public const int ReagentReserve = SupplyRules.ReagentTarget * ReserveRestocks;

    /// <summary>Bandages the bag holds for a person that heals itself.</summary>
    public const int BandageReserve = SupplyRules.BandageTarget * ReserveRestocks;

    /// <summary>A red, and a blue fighter, keep a spare kit in the bank box.</summary>
    public static bool KeepsSpare(bool red, bool fighter) => red || fighter;

    /// <summary>A person that keeps a spare kit and has no bag in its box gets one, packed; anyone else does not.</summary>
    public static bool Seeds(bool keepsSpare, bool hasBag) => keepsSpare && !hasBag;

    /// <summary>
    /// A kit piece comes out of the bag when it is a combat piece the person carries none of
    /// and the bag holds one. A stack (reagents, arrows, bandages) is left to the supply draw,
    /// which takes only up to the carry target.
    /// </summary>
    public static bool TakesOut(bool combatPiece, bool carried, bool inBag, bool stackable) =>
        combatPiece && !carried && inBag && !stackable;

    /// <summary>
    /// A piece that fits the kit (<see cref="FitsBuild"/>, or a combat piece of the kit
    /// itself) goes into the bag when the bag holds none of its role and the person carries it
    /// loose while its own place is already dressed: a spare, not the piece it wears.
    /// </summary>
    public static bool Stows(bool fitsKit, bool inBag, bool looseSpare) => fitsKit && !inBag && looseSpare;

    /// <summary>
    /// A red keeps a piece of gear, off a body or away from the pawn counter, when it fits its
    /// build and the bag or its own back lacks that role, and it carries no loose piece of the
    /// role already. Anything else is pawned or tossed as before.
    /// </summary>
    public static bool Keeps(bool fits, bool inBag, bool worn, bool carriedLoose) => fits && (!inBag || !worn) && !carriedLoose;

    /// <summary>
    /// The role of a piece by its type: a weapon of the build's row that fires the kit weapon's
    /// ammunition, a shelf shield, or armor of a known slot; <see cref="SpareRole.None"/>
    /// otherwise.
    /// </summary>
    public static SpareRole RoleOf(string typeName, string weaponRow, string kitWeapon)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return SpareRole.None;
        }

        if (KitVariation.InRow(weaponRow, typeName) && KitVariation.SameAmmunition(typeName, kitWeapon ?? weaponRow))
        {
            return new SpareRole(SpareRoleKind.Weapon, default);
        }

        if (GearPlan.IsShelfShield(typeName))
        {
            return new SpareRole(SpareRoleKind.Shield, default);
        }

        return GearLadder.TrySlotOf(typeName, out var slot) ? new SpareRole(SpareRoleKind.Armor, slot) : SpareRole.None;
    }

    /// <summary>
    /// True when a piece of this role serves the build: its weapon, its shield when it carries
    /// one, or armor on a slot it covers (<paramref name="slots"/>) no heavier than it wears
    /// (<paramref name="armorScore"/> against <paramref name="wearCeiling"/>, the
    /// <see cref="GearLadder.WearCeiling"/>). A mage keeps no plate and a tamer no shield.
    /// </summary>
    public static bool FitsBuild(SpareRole role, bool carriesShield, IReadOnlyList<GearSlot> slots, int armorScore, int wearCeiling) =>
        role.Kind switch
        {
            SpareRoleKind.Weapon => true,
            SpareRoleKind.Shield => carriesShield,
            SpareRoleKind.Armor => Covers(slots, role.Slot) && armorScore <= wearCeiling,
            _ => false
        };

    /// <summary>How long a red that found no shop out of the guards' reach for its piece buys no gear.</summary>
    public static readonly TimeSpan ShopMissRest = TimeSpan.FromMinutes(20);

    /// <summary>
    /// True while a red rests from shopping after it found no shop out of the guards' reach
    /// that sells its piece (<see cref="ShopMissRest"/>), so it does not turn back to the Den
    /// to re-arm on every run. Live, 13 trips ended at the Den's counters with "no shop had
    /// the piece" once the walk on to a guarded town's shop was refused.
    /// </summary>
    public static bool ShopRests(DateTime missedAt, DateTime now) => !TimeRules.Rested(missedAt, now, ShopMissRest);

    /// <summary>A combat piece the bag and the pack both lack: the spare kit's next buy.</summary>
    public static bool Lacks(bool combatPiece, bool inBag, bool inPack) => combatPiece && !inBag && !inPack;

    /// <summary>True when the person's combat kit is gone, or its weapon when its build carries one.</summary>
    public static bool LacksArms(bool kitMissing, bool carriesWeapon, bool armed) => kitMissing || carriesWeapon && !armed;

    /// <summary>
    /// A red does not ride out while it lacks arms (<see cref="LacksArms"/>); it re-arms in the
    /// Den first, when the bag or the shops can arm it (<paramref name="canReArm"/>). With
    /// nothing to re-arm from it rides out as it is, strips what it can off the bodies its gang
    /// makes, and so the plan does not turn back to an empty bank every think.
    /// </summary>
    public static bool MustReArm(bool red, bool lacksArms, bool canReArm) => red && lacksArms && canReArm;

    private static bool Covers(IReadOnlyList<GearSlot> slots, GearSlot slot)
    {
        for (var i = 0; i < (slots?.Count ?? 0); i++)
        {
            if (slots[i] == slot)
            {
                return true;
            }
        }

        return false;
    }
}
