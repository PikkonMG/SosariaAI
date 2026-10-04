using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>
/// Era templates for every class, fitted to the caps of the era and scaled by skill
/// tier. The dexxer, the hally mage, the provoking bard and the fighting smith are
/// the templates players of the period ran. Worker classes carry the swords,
/// tactics, anatomy, healing and resist a worker keeps for the road, so their seven
/// skills stay inside the 700 cap. Rolled from the id, so a reboot rebuilds the
/// same template.
/// </summary>
public static class ClassBuilds
{
    public const double SecondaryGap = 4;
    public const double UtilityShare = 0.5;
    public const int PrimaryJitterSides = 5;
    public const int SecondaryJitterSides = 4;
    private const double PrimaryJitterCenter = 2;

    public const int OneHandedSwordShare = 55;
    public const int TwoHandedSwordShare = 20;
    public const int MaceShare = 25;
    public const int KryssShare = 70;
    public const int SpearShare = 30;
    public const int HallyMageShare = 45;
    public const int MaceMageShare = 15;
    public const int PureMageShare = 40;

    public const string ThiefDagger = "Dagger";
    public const string MinerPick = "Pickaxe";
    public const string TailorKnife = "SkinningKnife";
    public const string SailorBlade = "Cutlass";

    private const int VariantSalt = 601;
    private const int PrimaryJitterSalt = 607;
    private const int SecondaryJitterSalt = 613;
    private const int PercentBase = 100;

    // Full stat lines at 225, the era's stat cap, each stat at most 100.
    public static readonly (int Strength, int Dexterity, int Intelligence) DexxerStats = (100, 100, 25);
    public static readonly (int Strength, int Dexterity, int Intelligence) TankMageStats = (100, 25, 100);
    public static readonly (int Strength, int Dexterity, int Intelligence) CasterStats = (70, 55, 100);
    public static readonly (int Strength, int Dexterity, int Intelligence) ScholarStats = (80, 45, 100);
    public static readonly (int Strength, int Dexterity, int Intelligence) BardStats = (75, 50, 100);
    public static readonly (int Strength, int Dexterity, int Intelligence) ArcherStats = (90, 100, 35);
    public static readonly (int Strength, int Dexterity, int Intelligence) RangerStats = (85, 100, 40);
    public static readonly (int Strength, int Dexterity, int Intelligence) ThiefStats = (60, 100, 65);
    public static readonly (int Strength, int Dexterity, int Intelligence) SmithStats = (100, 65, 60);
    public static readonly (int Strength, int Dexterity, int Intelligence) MinerStats = (100, 80, 45);
    public static readonly (int Strength, int Dexterity, int Intelligence) LumberjackStats = (100, 85, 40);
    public static readonly (int Strength, int Dexterity, int Intelligence) CarpenterStats = (100, 70, 55);
    public static readonly (int Strength, int Dexterity, int Intelligence) TailorStats = (75, 75, 75);
    public static readonly (int Strength, int Dexterity, int Intelligence) FishermanStats = (90, 80, 55);
    public static readonly (int Strength, int Dexterity, int Intelligence) NinjaStats = (85, 100, 40);

    private static readonly SkillName[] WorkerGuard =
    [
        SkillName.Swords, SkillName.Tactics, SkillName.Anatomy, SkillName.Healing, SkillName.MagicResist
    ];

    private static readonly SkillName[] DexxerCore =
    [
        SkillName.Tactics, SkillName.Anatomy, SkillName.Healing, SkillName.MagicResist, SkillName.Parry
    ];

    private static readonly SkillName[] MageCore =
    [
        SkillName.EvalInt, SkillName.Meditation, SkillName.MagicResist, SkillName.Wrestling
    ];

    /// <summary>
    /// The 1999 mage-tamer: lore to command strong beasts, veterinary to bandage them, and a
    /// full mage's magery, evaluate and meditation to heal itself, fight beside them and recall.
    /// </summary>
    public static readonly SkillName[] TamerCore =
    [
        SkillName.AnimalLore, SkillName.Veterinary, SkillName.Magery, SkillName.EvalInt, SkillName.Meditation,
        SkillName.MagicResist
    ];

    public static ResolvedBuild For(PersonProfile profile, CharacterRole role, string uniqueId, EraBand band)
    {
        var resolved = profile ?? PersonProfile.Default;
        var template = TemplateFor(resolved.Class, role, uniqueId);
        var skills = Skills(template, resolved.Tier, uniqueId);
        var percent = SkillTierRules.StatPercent(resolved.Tier);
        var stats = EraBuildCaps.FitStats(
            template.Stats.Strength * percent / PercentBase,
            template.Stats.Dexterity * percent / PercentBase,
            template.Stats.Intelligence * percent / PercentBase,
            band
        );

        return new ResolvedBuild
        {
            Style = template.Style,
            Role = role,
            Veteran = resolved.IsVeteran,
            Skills = skills,
            Strength = stats.Strength,
            Dexterity = stats.Dexterity,
            Intelligence = stats.Intelligence,
            Kit = ClassKits.For(template, resolved.Tier, resolved.Female, uniqueId),
            CanHeal = skills.ContainsKey(nameof(SkillName.Healing)),
            HealInterval = SosariaCombat.DefaultHealIntervalSeconds
        };
    }

    /// <summary>The template of a character's class and job, the one its build and gear follow.</summary>
    public static ClassBuildTemplate TemplateOf(SosariaCharacter character) =>
        TemplateFor(character.PersonProfile.Class, character.Build?.Role ?? CharacterRole.Worker, character.CharacterId);

    public static ClassBuildTemplate TemplateFor(PersonClass personClass, CharacterRole role, string uniqueId) =>
        personClass switch
        {
            PersonClass.Warrior => Warrior(role, uniqueId),
            PersonClass.Fencer => Fencer(uniqueId),
            PersonClass.Mage => Mage(uniqueId),
            PersonClass.Archer => new ClassBuildTemplate
            {
                Primary = SkillName.Archery,
                Secondary = [SkillName.Tactics, SkillName.Anatomy, SkillName.Healing, SkillName.MagicResist, SkillName.Tracking],
                Utility = [SkillName.Magery],
                Stats = ArcherStats,
                Style = CombatStyle.Archer,
                Weapon = KitVariation.Longbow,
                Armor = KitArmor.Medium
            },
            PersonClass.Ranger => new ClassBuildTemplate
            {
                Primary = SkillName.Archery,
                Secondary = [SkillName.Tracking, SkillName.Camping, SkillName.Tactics, SkillName.Hiding, SkillName.Anatomy, SkillName.Healing],
                Stats = RangerStats,
                Style = CombatStyle.Archer,
                Weapon = KitVariation.Longbow,
                Armor = KitArmor.Medium
            },
            PersonClass.Healer => new ClassBuildTemplate
            {
                Primary = SkillName.Healing,
                Secondary = [SkillName.Anatomy, SkillName.SpiritSpeak, SkillName.Magery, SkillName.EvalInt, SkillName.Meditation, SkillName.MagicResist],
                Stats = ScholarStats,
                Style = CombatStyle.Mage,
                Armor = KitArmor.None
            },
            PersonClass.Bard => new ClassBuildTemplate
            {
                Primary = SkillName.Musicianship,
                Secondary = [SkillName.Provocation, SkillName.Peacemaking, SkillName.Discordance, SkillName.Magery, SkillName.Meditation, SkillName.MagicResist],
                Stats = BardStats,
                Style = CombatStyle.Mage,
                Armor = KitArmor.None,
                ClassPieces = [KitVariation.Instrument]
            },
            PersonClass.Tamer => new ClassBuildTemplate
            {
                Primary = SkillName.AnimalTaming,
                Secondary = TamerCore,
                Stats = CasterStats,
                Style = CombatStyle.Mage,
                Weapon = KitVariation.HerdingStaff,
                Armor = KitArmor.Light
            },
            PersonClass.Thief => new ClassBuildTemplate
            {
                Primary = SkillName.Stealing,
                Secondary = [SkillName.Snooping, SkillName.Hiding, SkillName.Stealth, SkillName.Lockpicking, SkillName.Fencing, SkillName.MagicResist],
                Stats = ThiefStats,
                Style = CombatStyle.Melee,
                Weapon = ThiefDagger,
                Armor = KitArmor.Light
            },
            PersonClass.TreasureHunter => new ClassBuildTemplate
            {
                Primary = SkillName.Cartography,
                Secondary = [SkillName.Lockpicking, SkillName.RemoveTrap, SkillName.Magery, SkillName.EvalInt, SkillName.Meditation, SkillName.MagicResist],
                Stats = ScholarStats,
                Style = CombatStyle.Mage,
                Armor = KitArmor.Light
            },
            PersonClass.Paladin => SwordDexxer(SkillName.Chivalry, shield: true, ClassKits.BookOfChivalry),
            PersonClass.Samurai => SwordDexxer(SkillName.Bushido, shield: false, ClassKits.BookOfBushido),
            PersonClass.Necromancer => new ClassBuildTemplate
            {
                Primary = SkillName.Necromancy,
                Secondary = [SkillName.SpiritSpeak, SkillName.Magery, .. MageCore],
                Stats = CasterStats,
                Style = CombatStyle.Mage,
                Armor = KitArmor.None,
                ClassPieces = ClassKits.NecromancerPieces
            },
            PersonClass.Ninja => new ClassBuildTemplate
            {
                Primary = SkillName.Ninjitsu,
                Secondary = [SkillName.Fencing, SkillName.Hiding, SkillName.Stealth, SkillName.Tactics, SkillName.Anatomy, SkillName.Healing],
                Stats = NinjaStats,
                Style = CombatStyle.Melee,
                Weapon = KitVariation.OneHandedFencing,
                Armor = KitArmor.Light,
                ClassPieces = [ClassKits.BookOfNinjitsu]
            },
            PersonClass.Merchant => Worker(SkillName.ItemID, SkillName.ArmsLore, CasterStats, KitVariation.OneHandedSword, KitArmor.None),
            PersonClass.Smith => Worker(SkillName.Blacksmith, SkillName.Mining, SmithStats, KitVariation.OneHandedSword, KitArmor.None),
            PersonClass.Miner => Worker(SkillName.Mining, SkillName.Blacksmith, MinerStats, MinerPick, KitArmor.Work),
            PersonClass.Lumberjack => Worker(SkillName.Lumberjacking, SkillName.Fletching, LumberjackStats, KitVariation.WorkerBlade, KitArmor.Work),
            PersonClass.Carpenter => Worker(SkillName.Carpentry, SkillName.Lumberjacking, CarpenterStats, KitVariation.WorkerBlade, KitArmor.None),
            PersonClass.Tailor => Worker(SkillName.Tailoring, SkillName.ArmsLore, TailorStats, TailorKnife, KitArmor.None),
            PersonClass.Alchemist => Worker(SkillName.Alchemy, SkillName.TasteID, ScholarStats, KitVariation.WorkerBlade, KitArmor.None),
            PersonClass.Scribe => Worker(SkillName.Inscribe, SkillName.Magery, ScholarStats, KitVariation.WorkerBlade, KitArmor.None),
            PersonClass.Bowyer => Worker(SkillName.Fletching, SkillName.Lumberjacking, LumberjackStats, KitVariation.WorkerBlade, KitArmor.None),
            PersonClass.Tinker => Worker(SkillName.Tinkering, SkillName.Carpentry, CarpenterStats, KitVariation.WorkerBlade, KitArmor.None),
            _ => Worker(SkillName.Fishing, SkillName.Cooking, FishermanStats, SailorBlade, KitArmor.None)
        };

    private static ClassBuildTemplate Warrior(CharacterRole role, string uniqueId)
    {
        // A worker keeps a swords defence the worker floor already trains, so a
        // patrolling militiaman never rolls the mace row.
        var mace = role == CharacterRole.Worker ? 0 : MaceShare;
        var variant = PersonDice.Weighted(uniqueId, VariantSalt, OneHandedSwordShare, TwoHandedSwordShare, mace);

        return new ClassBuildTemplate
        {
            Primary = variant == 2 ? SkillName.Macing : SkillName.Swords,
            Secondary = DexxerCore,
            Utility = [SkillName.Magery],
            Stats = DexxerStats,
            Style = CombatStyle.Melee,
            Weapon = variant switch
            {
                0 => KitVariation.OneHandedSword,
                1 => KitVariation.TwoHandedSword,
                _ => KitVariation.OneHandedMace
            },
            Shield = variant != 1,
            Armor = KitArmor.Heavy
        };
    }

    /// <summary>
    /// The later sword dexxer: its era's skill leads, swords, tactics, anatomy, healing
    /// and parry follow, and magery stays at half for travel. A samurai parries with the
    /// blade alone; a paladin keeps the shield.
    /// </summary>
    private static ClassBuildTemplate SwordDexxer(SkillName eraSkill, bool shield, string book) =>
        new()
        {
            Primary = eraSkill,
            Secondary = [SkillName.Swords, SkillName.Tactics, SkillName.Anatomy, SkillName.Healing, SkillName.Parry],
            Utility = [SkillName.Magery],
            Stats = DexxerStats,
            Style = CombatStyle.Melee,
            Weapon = KitVariation.OneHandedSword,
            Shield = shield,
            Armor = KitArmor.Heavy,
            ClassPieces = [book]
        };

    private static ClassBuildTemplate Fencer(string uniqueId)
    {
        var kryss = PersonDice.Weighted(uniqueId, VariantSalt, KryssShare, SpearShare) == 0;

        return new ClassBuildTemplate
        {
            Primary = SkillName.Fencing,
            Secondary = DexxerCore,
            Utility = [SkillName.Magery],
            Stats = DexxerStats,
            Style = CombatStyle.Melee,
            Weapon = kryss ? KitVariation.OneHandedFencing : KitVariation.TwoHandedFencing,
            Shield = kryss,
            Armor = KitArmor.Heavy
        };
    }

    /// <summary>
    /// The hally mage was the king of the period: GM magery with a halberd and tactics.
    /// Some tank mages swung maces; the rest were pure mages who scribed and brewed.
    /// </summary>
    private static ClassBuildTemplate Mage(string uniqueId)
    {
        var variant = PersonDice.Weighted(uniqueId, VariantSalt, HallyMageShare, MaceMageShare, PureMageShare);

        if (variant == 2)
        {
            return new ClassBuildTemplate
            {
                Primary = SkillName.Magery,
                Secondary = [.. MageCore, SkillName.Inscribe, SkillName.Alchemy],
                Stats = CasterStats,
                Style = CombatStyle.Mage,
                Armor = KitArmor.None
            };
        }

        return new ClassBuildTemplate
        {
            Primary = SkillName.Magery,
            Secondary = [.. MageCore, variant == 0 ? SkillName.Swords : SkillName.Macing, SkillName.Tactics],
            Stats = TankMageStats,
            Style = CombatStyle.Mage,
            Weapon = variant == 0 ? KitVariation.TwoHandedSword : KitVariation.TwoHandedMace,
            Armor = KitArmor.Medium
        };
    }

    private static ClassBuildTemplate Worker(
        SkillName craft,
        SkillName second,
        (int Strength, int Dexterity, int Intelligence) stats,
        string weapon,
        KitArmor armor
    ) =>
        new()
        {
            Primary = craft,
            Secondary = [second, .. WorkerGuard],
            Stats = stats,
            Style = CombatStyle.Melee,
            Weapon = weapon,
            Armor = armor
        };

    private static Dictionary<string, double> Skills(ClassBuildTemplate template, SkillTier tier, string uniqueId)
    {
        var skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        // A grandmaster is exactly 100; below that each person's lead skill wobbles a little.
        var lead = tier == SkillTier.Grandmaster
            ? SkillTierRules.PrimarySkill(tier)
            : SkillTierRules.PrimarySkill(tier) + PersonDice.Roll(uniqueId, PrimaryJitterSalt, PrimaryJitterSides) -
              PrimaryJitterCenter;
        var follow = tier == SkillTier.Grandmaster ? lead : lead - SecondaryGap;

        skills[template.Primary.ToString()] = lead;

        for (var i = 0; i < template.Secondary.Length; i++)
        {
            var jitter = PersonDice.Roll(uniqueId, SecondaryJitterSalt + i, SecondaryJitterSides);
            skills[template.Secondary[i].ToString()] = Math.Max(0, follow - jitter);
        }

        for (var i = 0; i < template.Utility.Length; i++)
        {
            skills[template.Utility[i].ToString()] = Math.Floor(lead * UtilityShare);
        }

        return EraBuildCaps.FitSkills(skills);
    }
}
