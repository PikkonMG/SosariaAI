using System;
using System.Collections.Generic;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Economy;

/// <summary>Where an armor piece sits on the body.</summary>
public enum GearSlot
{
    Chest,
    Legs,
    Arms,
    Gloves,
    Neck,
    Helm
}

/// <summary>The armor a shop sells, weakest first. <see cref="None"/> wears no armor.</summary>
public enum GearMaterial
{
    None,
    Leather,
    Studded,
    Ringmail,
    Chainmail,
    Plate
}

/// <summary>
/// One piece on a shop shelf. <paramref name="Female"/> is null for a piece anyone wears,
/// true for the female cut and false for the male cut of a chest.
/// </summary>
public readonly record struct GearPiece(
    string TypeName,
    GearSlot Slot,
    GearMaterial Material,
    int Price,
    string Vendor,
    string FallbackVendor,
    bool? Female
);

/// <summary>
/// The armor ladder a player climbed: leather, studded, ring, chain, then plate. Each rung
/// names the pieces the shops really stock (a tanner for leather and studded, a smith for
/// metal) at their shelf prices, and scores on the <see cref="GearScore"/> scale. A class
/// wears up to its ceiling, a tier starts on its own rung, and savings buy the next ones.
/// Pure: no world reads.
/// </summary>
public static class GearLadder
{
    public const int LeatherScore = 8;
    public const int StuddedScore = 10;
    public const int RingmailScore = 12;
    public const int ChainmailScore = 14;
    public const int PlateScore = 22;
    public const int BoneScore = 12;

    /// <summary>Rungs a purse can climb above its tier's own kit: none for the poor, two for the rich.</summary>
    public const int PoorSteps = 0;
    public const int ModestSteps = 1;
    public const int RichSteps = 2;

    private static readonly GearSlot[] FullSuit =
        [GearSlot.Chest, GearSlot.Legs, GearSlot.Arms, GearSlot.Gloves, GearSlot.Neck, GearSlot.Helm];

    private static readonly GearSlot[] LightSuit = [GearSlot.Chest, GearSlot.Legs, GearSlot.Gloves];
    private static readonly GearSlot[] WorkSuit = [GearSlot.Gloves];

    // Shelf prices from the tanner's and the smith's buy lists.
    private static readonly GearPiece[] Shelf =
    [
        Leather("LeatherChest", GearSlot.Chest, 101, false),
        Leather("FemaleLeatherChest", GearSlot.Chest, 116, true),
        Leather("LeatherLegs", GearSlot.Legs, 80, null),
        Leather("LeatherArms", GearSlot.Arms, 80, null),
        Leather("LeatherGloves", GearSlot.Gloves, 60, null),
        Leather("LeatherGorget", GearSlot.Neck, 74, null),
        Leather("LeatherCap", GearSlot.Helm, 10, null),
        Studded("StuddedChest", GearSlot.Chest, 75, false),
        Studded("FemaleStuddedChest", GearSlot.Chest, 62, true),
        Studded("StuddedLegs", GearSlot.Legs, 67, null),
        Studded("StuddedArms", GearSlot.Arms, 57, null),
        Studded("StuddedGloves", GearSlot.Gloves, 45, null),
        Studded("StuddedGorget", GearSlot.Neck, 50, null),
        Metal("RingmailChest", GearSlot.Chest, GearMaterial.Ringmail, 121, null),
        Metal("RingmailLegs", GearSlot.Legs, GearMaterial.Ringmail, 90, null),
        Metal("RingmailArms", GearSlot.Arms, GearMaterial.Ringmail, 85, null),
        Metal("RingmailGloves", GearSlot.Gloves, GearMaterial.Ringmail, 93, null),
        Metal("ChainChest", GearSlot.Chest, GearMaterial.Chainmail, 143, null),
        Metal("ChainLegs", GearSlot.Legs, GearMaterial.Chainmail, 149, null),
        Metal("ChainCoif", GearSlot.Helm, GearMaterial.Chainmail, 17, null),
        Metal("PlateChest", GearSlot.Chest, GearMaterial.Plate, 243, false),
        // The tanner, not the smith, keeps the female plate cut on its shelf.
        new("FemalePlateChest", GearSlot.Chest, GearMaterial.Plate, 207, ShopFinder.TannerToken, ShopFinder.ArmorerToken, true),
        Metal("PlateLegs", GearSlot.Legs, GearMaterial.Plate, 218, null),
        Metal("PlateArms", GearSlot.Arms, GearMaterial.Plate, 188, null),
        Metal("PlateGloves", GearSlot.Gloves, GearMaterial.Plate, 155, null),
        Metal("PlateGorget", GearSlot.Neck, GearMaterial.Plate, 104, null),
        Metal("PlateHelm", GearSlot.Helm, GearMaterial.Plate, 21, null)
    ];

    // Worn pieces that no shelf in the ladder stocks, ranked beside the rung they match.
    private static readonly Dictionary<string, (int Score, GearSlot Slot)> WornOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LeatherSkirt"] = (LeatherScore, GearSlot.Legs),
        ["LeatherShorts"] = (LeatherScore, GearSlot.Legs),
        ["LeatherBustierArms"] = (LeatherScore, GearSlot.Chest),
        ["StuddedBustierArms"] = (StuddedScore, GearSlot.Chest),
        ["Bascinet"] = (StuddedScore, GearSlot.Helm),
        ["Helmet"] = (ChainmailScore, GearSlot.Helm),
        ["NorseHelm"] = (ChainmailScore, GearSlot.Helm),
        ["CloseHelm"] = (ChainmailScore, GearSlot.Helm),
        ["BoneChest"] = (BoneScore, GearSlot.Chest),
        ["BoneLegs"] = (BoneScore, GearSlot.Legs),
        ["BoneArms"] = (BoneScore, GearSlot.Arms),
        ["BoneGloves"] = (BoneScore, GearSlot.Gloves),
        ["BoneHelm"] = (BoneScore, GearSlot.Helm)
    };

    public static IReadOnlyList<GearPiece> Pieces => Shelf;

    public static int ScoreOf(GearMaterial material) =>
        material switch
        {
            GearMaterial.Leather => LeatherScore,
            GearMaterial.Studded => StuddedScore,
            GearMaterial.Ringmail => RingmailScore,
            GearMaterial.Chainmail => ChainmailScore,
            GearMaterial.Plate => PlateScore,
            _ => 0
        };

    /// <summary>The rung score of a known armor type, shelf or worn-only; false for anything else.</summary>
    public static bool TryScoreOf(string typeName, out int score) => TryKnown(typeName, out score, out _);

    /// <summary>The slot a known armor type, shelf or worn-only, covers; false for anything else.</summary>
    public static bool TrySlotOf(string typeName, out GearSlot slot) => TryKnown(typeName, out _, out slot);

    private static bool TryKnown(string typeName, out int score, out GearSlot slot)
    {
        score = 0;
        slot = default;

        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        for (var i = 0; i < Shelf.Length; i++)
        {
            if (string.Equals(Shelf[i].TypeName, typeName, StringComparison.OrdinalIgnoreCase))
            {
                score = ScoreOf(Shelf[i].Material);
                slot = Shelf[i].Slot;
                return true;
            }
        }

        if (!WornOnly.TryGetValue(typeName, out var worn))
        {
            return false;
        }

        (score, slot) = worn;
        return true;
    }

    /// <summary>The shelf piece of this rung for this slot and cut, or null when no shop sells one.</summary>
    public static GearPiece? Piece(GearSlot slot, GearMaterial material, bool female)
    {
        for (var i = 0; i < Shelf.Length; i++)
        {
            var piece = Shelf[i];

            if (piece.Slot == slot && piece.Material == material && (piece.Female == null || piece.Female == female))
            {
                return piece;
            }
        }

        return null;
    }

    /// <summary>
    /// The best shelf piece for the slot at or below <paramref name="material"/>: a chain
    /// suit takes ring sleeves and a studded gorget, since no smith sells chain ones.
    /// </summary>
    public static GearPiece? BestAtOrBelow(GearSlot slot, GearMaterial material, bool female)
    {
        for (var rung = material; rung > GearMaterial.None; rung--)
        {
            if (Piece(slot, rung, female) is { } piece)
            {
                return piece;
            }
        }

        return null;
    }

    /// <summary>The heaviest armor a class of this weight will wear.</summary>
    public static GearMaterial Ceiling(KitArmor weight) =>
        weight switch
        {
            KitArmor.Heavy => GearMaterial.Plate,
            KitArmor.Medium or KitArmor.Light => GearMaterial.Studded,
            KitArmor.Work => GearMaterial.Leather,
            _ => GearMaterial.None
        };

    /// <summary>
    /// The heaviest armor a build keeps on. The Second Age had no mage armor: ring, chain,
    /// plate and bone stopped meditation, and studded halved it. A pure caster who meditates
    /// wears leather at most, a tank mage studded; a build that never meditates has no
    /// such limit.
    /// </summary>
    public static GearMaterial MeditationCeiling(ClassBuildTemplate template) =>
        !template.Meditates ? GearMaterial.Plate
        : template.WeaponTrained ? GearMaterial.Studded
        : GearMaterial.Leather;

    /// <summary>The heaviest armor a build buys and puts on: its weight's ceiling, held under its meditation.</summary>
    public static GearMaterial Ceiling(ClassBuildTemplate template) =>
        Lower(Ceiling(template.Armor), MeditationCeiling(template));

    /// <summary>
    /// The heaviest armor, on the <see cref="GearScore"/> scale, a build keeps on. Anything
    /// above it comes off into the pack.
    /// </summary>
    public static int KeepCeiling(ClassBuildTemplate template) => ScoreOf(MeditationCeiling(template));

    /// <summary>Bone for the dexxer's and the archer's veterans. It stops meditation, so no tank mage wears it.</summary>
    public static bool WearsBone(ClassBuildTemplate template) =>
        template.Armor is KitArmor.Heavy or KitArmor.Medium && !template.Meditates;

    /// <summary>
    /// The heaviest armor, on the <see cref="GearScore"/> scale, a build puts on: its shop
    /// ceiling, or bone for the archers whose veterans wore it.
    /// </summary>
    public static int WearCeiling(ClassBuildTemplate template) =>
        Math.Max(ScoreOf(Ceiling(template)), WearsBone(template) ? BoneScore : 0);

    /// <summary>
    /// The rung a class wears on its first day: the dexxer climbs leather, studded, ring,
    /// chain and plate with its tier; the archer and tank mage move from leather to studded
    /// at expert; thieves, tamers and workers wear leather.
    /// </summary>
    public static GearMaterial KitMaterial(KitArmor weight, SkillTier tier) =>
        weight switch
        {
            KitArmor.Heavy => tier switch
            {
                SkillTier.Novice => GearMaterial.Leather,
                SkillTier.Apprentice => GearMaterial.Studded,
                SkillTier.Journeyman => GearMaterial.Ringmail,
                SkillTier.Expert => GearMaterial.Chainmail,
                _ => GearMaterial.Plate
            },
            KitArmor.Medium => tier >= SkillTier.Expert ? GearMaterial.Studded : GearMaterial.Leather,
            KitArmor.Light or KitArmor.Work => GearMaterial.Leather,
            _ => GearMaterial.None
        };

    /// <summary>The first-day rung of this build, never above its ceiling.</summary>
    public static GearMaterial KitMaterial(ClassBuildTemplate template, SkillTier tier) =>
        Lower(KitMaterial(template.Armor, tier), Ceiling(template));

    /// <summary>The slots a class of this weight covers with armor.</summary>
    public static IReadOnlyList<GearSlot> Slots(KitArmor weight) =>
        weight switch
        {
            KitArmor.Heavy or KitArmor.Medium => FullSuit,
            KitArmor.Light => LightSuit,
            KitArmor.Work => WorkSuit,
            _ => []
        };

    public static int WealthSteps(PersonWealth wealth) =>
        wealth switch
        {
            PersonWealth.Poor => PoorSteps,
            PersonWealth.Rich => RichSteps,
            _ => ModestSteps
        };

    /// <summary>
    /// The best rung a person shops for: its tier's kit plus what its purse allows, never
    /// above its build's ceiling. A modest novice dexxer saves up to studded; a rich one to ring.
    /// </summary>
    public static GearMaterial Target(ClassBuildTemplate template, SkillTier tier, PersonWealth wealth)
    {
        var kit = KitMaterial(template, tier);

        if (kit == GearMaterial.None)
        {
            return GearMaterial.None;
        }

        var reach = (GearMaterial)Math.Min((int)kit + WealthSteps(wealth), (int)GearMaterial.Plate);
        return Lower(reach, Ceiling(template));
    }

    private static GearMaterial Lower(GearMaterial first, GearMaterial second) => first < second ? first : second;

    private static GearPiece Leather(string typeName, GearSlot slot, int price, bool? female) =>
        new(typeName, slot, GearMaterial.Leather, price, ShopFinder.TannerToken, ShopFinder.ArmorerToken, female);

    private static GearPiece Studded(string typeName, GearSlot slot, int price, bool? female) =>
        new(typeName, slot, GearMaterial.Studded, price, ShopFinder.TannerToken, ShopFinder.ArmorerToken, female);

    private static GearPiece Metal(string typeName, GearSlot slot, GearMaterial material, int price, bool? female) =>
        new(typeName, slot, material, price, ShopFinder.SmithToken, ShopFinder.ArmorerToken, female);
}
