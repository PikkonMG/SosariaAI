using System.Collections.Generic;
using SosariaAI.Economy;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// The worn and carried kit of a class at a tier. The body pieces come from the armor
/// ladder (<see cref="GearLadder"/>): a novice dexxer wears leather, an apprentice studded
/// leather, a journeyman ring mail, an expert chain and a veteran plate; archers and tank
/// mages climb from leather to studded, and no tank mage wears bone; casters wear cloth
/// (dressed by the look pass). No build is kitted above its ceiling (see
/// <see cref="GearLadder.Ceiling(ClassBuildTemplate)"/>).
/// <see cref="KitFinish"/> makes most of it crafted work on the first day, some of it in
/// coloured ore. A class of a later era carries its own book beside the kit.
/// </summary>
public static class ClassKits
{
    public const string Spellbook = "Spellbook";
    public const string RecallRune = "RecallRune";
    public const string LeatherSkirt = "LeatherSkirt";
    public const int BonePercent = 20;
    public const int SkirtPercent = 50;
    public const int PartialLegsPercent = 50;

    private const int SkirtSalt = 503;
    private const int HelmSalt = 509;
    private const int ShieldSalt = 521;
    private const int BoneSalt = 523;
    private const int LegsSalt = 541;

    public static readonly string[] AllReagents =
    [
        "BlackPearl", "Bloodmoss", "Garlic", "Ginseng", "MandrakeRoot", "Nightshade", "SpidersSilk", "SulfurousAsh"
    ];

    /// <summary>Black pearl, blood moss and mandrake: Recall and Mark.</summary>
    public static readonly string[] TravelReagents = ["BlackPearl", "Bloodmoss", "MandrakeRoot"];

    public const string NecromancerSpellbook = "NecromancerSpellbook";
    public const string BookOfChivalry = "BookOfChivalry";
    public const string BookOfBushido = "BookOfBushido";
    public const string BookOfNinjitsu = "BookOfNinjitsu";

    /// <summary>The necromancer's book after the mage's one, then the five necromancy reagents.</summary>
    public static readonly string[] NecromancerPieces =
    [
        NecromancerSpellbook, "BatWing", "DaemonBlood", "GraveDust", "NoxCrystal", "PigIron"
    ];

    private static readonly string[] BoneSet = ["BoneChest", "BoneLegs", "BoneArms", "BoneGloves", "BoneHelm"];

    /// <summary>The body pieces of a suit, the helm aside: it comes from the tier's helm pool.</summary>
    private static readonly GearSlot[] BodySlots = [GearSlot.Chest, GearSlot.Legs, GearSlot.Arms, GearSlot.Gloves, GearSlot.Neck];

    private static readonly string[] LightHelms = ["LeatherCap", null];
    private static readonly string[] ChainHelms = ["ChainCoif", "Helmet", null];
    private static readonly string[] PlateHelms = ["PlateHelm", "CloseHelm", "NorseHelm", "Bascinet", "Helmet", null];

    private static readonly string[] NoviceShields = ["Buckler", "WoodenShield"];
    private static readonly string[] MiddleShields = ["MetalShield", "BronzeShield", "WoodenKiteShield"];
    private static readonly string[] VeteranShields = ["HeaterShield", "MetalKiteShield", "MetalShield"];

    public static List<string> For(ClassBuildTemplate template, SkillTier tier, bool female, string uniqueId)
    {
        var kit = new List<string>();

        if (template == null)
        {
            return kit;
        }

        if (template.Weapon != null)
        {
            kit.Add(template.Weapon);
        }

        if (template.Shield)
        {
            kit.Add(ShieldFor(tier, uniqueId));
        }

        AddArmor(kit, template, tier, female, uniqueId);

        if (template.Caster || template.TravelMagic)
        {
            kit.Add(Spellbook);
            kit.AddRange(template.Caster ? AllReagents : TravelReagents);
            kit.Add(RecallRune);
        }

        kit.AddRange(template.ClassPieces);

        if (template.Weapon == KitVariation.Longbow)
        {
            kit.Add(KitVariation.Arrow);
        }

        return KitVariation.Vary(kit, uniqueId);
    }

    /// <summary>The shield this person carries at this tier, the same one on every boot.</summary>
    public static string ShieldFor(SkillTier tier, string uniqueId) => PersonDice.Pick(uniqueId, ShieldSalt, ShieldsFor(tier));

    private static string[] ShieldsFor(SkillTier tier) =>
        tier switch
        {
            <= SkillTier.Apprentice => NoviceShields,
            <= SkillTier.Expert => MiddleShields,
            _ => VeteranShields
        };

    private static void AddArmor(List<string> kit, ClassBuildTemplate template, SkillTier tier, bool female, string uniqueId)
    {
        switch (template.Armor)
        {
            case KitArmor.Heavy:
                AddHeavy(kit, template, tier, female, uniqueId);
                break;
            case KitArmor.Medium:
                AddMedium(kit, template, tier, female, uniqueId);
                break;
            case KitArmor.Light:
                AddPiece(kit, GearSlot.Chest, GearMaterial.Leather, female, uniqueId);
                AddPiece(kit, GearSlot.Gloves, GearMaterial.Leather, female, uniqueId);

                if (PersonDice.Chance(uniqueId, LegsSalt, PartialLegsPercent))
                {
                    AddPiece(kit, GearSlot.Legs, GearMaterial.Leather, female, uniqueId);
                }

                break;
            case KitArmor.Work:
                AddPiece(kit, GearSlot.Gloves, GearMaterial.Leather, female, uniqueId);
                break;
        }
    }

    private static void AddHeavy(List<string> kit, ClassBuildTemplate template, SkillTier tier, bool female, string uniqueId)
    {
        if (tier >= SkillTier.Expert && WearsBoneSet(template, uniqueId))
        {
            kit.AddRange(BoneSet);
            return;
        }

        AddSuit(kit, GearLadder.KitMaterial(template, tier), female, uniqueId);
        AddHelm(kit, HelmsFor(tier), uniqueId);
    }

    private static void AddMedium(List<string> kit, ClassBuildTemplate template, SkillTier tier, bool female, string uniqueId)
    {
        if (tier >= SkillTier.Adept && WearsBoneSet(template, uniqueId))
        {
            kit.AddRange(BoneSet);
            return;
        }

        AddSuit(kit, GearLadder.KitMaterial(template, tier), female, uniqueId);
        AddHelm(kit, LightHelms, uniqueId);
    }

    /// <summary>A few veterans wore bone, never one who meditates (see <see cref="GearLadder.WearsBone"/>).</summary>
    private static bool WearsBoneSet(ClassBuildTemplate template, string uniqueId) =>
        GearLadder.WearsBone(template) && PersonDice.Chance(uniqueId, BoneSalt, BonePercent);

    private static string[] HelmsFor(SkillTier tier) =>
        tier switch
        {
            <= SkillTier.Apprentice => LightHelms,
            <= SkillTier.Expert => ChainHelms,
            _ => PlateHelms
        };

    /// <summary>Every body piece of the rung, taking the best lower piece where no shop sells one.</summary>
    private static void AddSuit(List<string> kit, GearMaterial material, bool female, string uniqueId)
    {
        for (var i = 0; i < BodySlots.Length; i++)
        {
            AddPiece(kit, BodySlots[i], material, female, uniqueId);
        }
    }

    private static void AddPiece(List<string> kit, GearSlot slot, GearMaterial material, bool female, string uniqueId)
    {
        if (GearLadder.BestAtOrBelow(slot, material, female) is not { } piece)
        {
            return;
        }

        var leatherLegs = slot == GearSlot.Legs && piece.Material == GearMaterial.Leather;
        kit.Add(leatherLegs && female && PersonDice.Chance(uniqueId, SkirtSalt, SkirtPercent) ? LeatherSkirt : piece.TypeName);
    }

    private static void AddHelm(List<string> kit, string[] helms, string uniqueId)
    {
        var helm = PersonDice.Pick(uniqueId, HelmSalt, helms);

        if (helm != null)
        {
            kit.Add(helm);
        }
    }
}
