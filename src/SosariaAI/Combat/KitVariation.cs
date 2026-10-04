using System;
using System.Collections.Generic;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// A kit names a row, not one item, so every copy of a swordsman does not carry the
/// same katana. Each row keeps one weapon skill and one grip: one-handed and
/// two-handed swords, fencing and maces, worker blades, herding staves, bows and
/// instruments. Copies roll within the row, seeded by their id so a reboot brings back
/// the same person, and a bow swap carries its ammunition with it.
/// </summary>
public static class KitVariation
{
    private const int KitSalt = 71;

    public const string Arrow = "Arrow";
    public const string Bolt = "Bolt";

    public const string OneHandedSword = "Katana";
    public const string TwoHandedSword = "Halberd";
    public const string OneHandedFencing = "Kryss";
    public const string TwoHandedFencing = "Spear";
    public const string OneHandedMace = "Mace";
    public const string TwoHandedMace = "WarHammer";
    public const string WorkerBlade = "Hatchet";
    public const string HerdingStaff = "Club";
    public const string Longbow = "Bow";
    public const string Instrument = "Lute";

    // Each row keeps one weapon skill and one grip, so a shield in the kit stays legal
    // beside every weapon of a one-handed row.
    private static readonly Dictionary<string, string[]> Alternates = new(StringComparer.OrdinalIgnoreCase)
    {
        [OneHandedSword] = ["Katana", "Broadsword", "Longsword", "VikingSword", "Scimitar", "Cutlass"],
        // Pole arms and great axes train Swordsmanship: the tank mage's halberd row.
        [TwoHandedSword] = ["Halberd", "Bardiche", "BattleAxe", "DoubleAxe", "LargeBattleAxe", "TwoHandedAxe", "ExecutionersAxe"],
        // The short spear is held in both hands: it rolls with the spears, never beside a shield.
        [OneHandedFencing] = ["Kryss", "WarFork"],
        [TwoHandedFencing] = ["Spear", "ShortSpear", "Pitchfork"],
        [OneHandedMace] = ["Mace", "WarMace", "Maul", "WarAxe", "HammerPick", "Club"],
        [TwoHandedMace] = ["WarHammer", "QuarterStaff", "GnarledStaff", "BlackStaff"],
        // Worker tools, still swords class: axes and knives, not a soldier's blade.
        [WorkerBlade] = ["Hatchet", "Axe", "Pickaxe", "ButcherKnife", "SkinningKnife", "Cleaver"],
        // Tamer has no weapon skill; the club was flavor. Crooks and staves fit better.
        [HerdingStaff] = ["Club", "ShepherdsCrook", "QuarterStaff", "GnarledStaff", "Mace", "BlackStaff"],
        [Longbow] = ["Bow", "Crossbow", "HeavyCrossbow"],
        [Instrument] = ["Lute", "Harp", "Drums", "Tambourine"]
    };

    private static readonly Dictionary<string, string> Ammunition = new(StringComparer.OrdinalIgnoreCase)
    {
        [Longbow] = Arrow,
        ["Crossbow"] = Bolt,
        ["HeavyCrossbow"] = Bolt
    };

    /// <summary>True when <paramref name="weapon"/> is one of the pieces the row rolls, the key itself included.</summary>
    public static bool InRow(string rowKey, string weapon)
    {
        if (string.IsNullOrWhiteSpace(rowKey) || string.IsNullOrWhiteSpace(weapon))
        {
            return false;
        }

        if (string.Equals(rowKey, weapon, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Alternates.TryGetValue(rowKey, out var alternates) &&
               Array.FindIndex(alternates, piece => string.Equals(piece, weapon, StringComparison.OrdinalIgnoreCase)) >= 0;
    }

    /// <summary>
    /// True when two weapons fire the same ammunition, or none: a bow and a crossbow do not
    /// share a quiver.
    /// </summary>
    public static bool SameAmmunition(string first, string second) =>
        string.Equals(AmmunitionOf(first), AmmunitionOf(second), StringComparison.OrdinalIgnoreCase);

    private static string AmmunitionOf(string weapon) =>
        weapon != null && Ammunition.TryGetValue(weapon, out var ammo) ? ammo : null;

    /// <summary>
    /// The kit a copy wears, with every weapon or instrument slot rolled from its
    /// same-skill row. Returns the kit untouched when it holds nothing the table knows.
    /// </summary>
    public static List<string> Vary(IReadOnlyList<string> kit, string uniqueId)
    {
        if (kit is not { Count: > 0 } || string.IsNullOrWhiteSpace(uniqueId))
        {
            return new List<string>(kit ?? []);
        }

        var varied = new List<string>(kit);

        for (var slot = 0; slot < varied.Count; slot++)
        {
            if (!Alternates.TryGetValue(kit[slot], out var alternates))
            {
                continue;
            }

            var pick = alternates[PersonDice.Roll(uniqueId, KitSalt + slot, alternates.Length)];
            varied[slot] = pick;

            if (Ammunition.TryGetValue(pick, out var ammo))
            {
                SwapAmmunition(varied, ammo);
            }
        }

        return varied;
    }

    /// <summary>A crossbow copy must carry bolts, not the bow preset's arrows.</summary>
    private static void SwapAmmunition(List<string> kit, string ammo)
    {
        for (var i = 0; i < kit.Count; i++)
        {
            if (string.Equals(kit[i], Arrow, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kit[i], Bolt, StringComparison.OrdinalIgnoreCase))
            {
                kit[i] = ammo;
            }
        }
    }
}
