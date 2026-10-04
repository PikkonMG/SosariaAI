using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Combat;

public static class BuildPresets
{
    public const string Swordsman = "swordsman";
    public const string Archer = "archer";
    public const string Mage = "mage";
    public const string Thief = "thief";
    public const string Tamer = "tamer";

    public const int NoviceStrength = 85;
    public const int WorkerStrength = 80;
    public const int NoviceDexterity = 70;
    public const int NoviceIntelligence = 30;
    public const double NoviceFightSkill = 75;
    public const double WorkerFightSkill = 65;
    public const double WorkerAnatomySkill = 50;
    public const double WorkerResistSkill = 50;
    public const int ArcherStrength = 70;
    public const int ArcherDexterity = 85;
    public const int ArcherIntelligence = 30;
    public const double NoviceTamingSkill = 60;
    public const double NoviceTamerSkill = 55;
    public const string TamerCrook = "ShepherdsCrook";
    public const int MageStrength = 50;
    public const int MageDexterity = 50;
    public const int MageIntelligence = 85;

    public static ResolvedBuild WorkerDefault { get; } = new()
    {
        Style = CombatStyle.Melee,
        Role = CharacterRole.Worker,
        Veteran = false,
        Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Swords"] = WorkerFightSkill,
            ["Tactics"] = WorkerFightSkill,
            ["Anatomy"] = WorkerAnatomySkill,
            ["Healing"] = WorkerFightSkill,
            ["MagicResist"] = WorkerResistSkill
        },
        Strength = WorkerStrength,
        Dexterity = NoviceDexterity,
        Intelligence = NoviceIntelligence,
        Kit =
        [
            "Hatchet",
            "LeatherChest",
            "LeatherLegs",
            "LeatherGorget",
            "LeatherGloves",
            "LeatherCap",
            "Boots",
            "Bandage"
        ],
        CanHeal = true,
        HealInterval = SosariaCombat.DefaultHealIntervalSeconds
    };

    public static ResolvedBuild Resolve(BuildDefinition definition) => Resolve(definition, EraBands.Current());

    /// <summary>The authored build over its preset, fitted inside the caps of <paramref name="band"/>.</summary>
    public static ResolvedBuild Resolve(BuildDefinition definition, EraBand band)
    {
        var preset = ResolvePreset(definition?.Preset);
        var veteran = definition?.Veteran == true;
        var skills = MergeSkills(preset.Skills, definition?.Skills, veteran);
        var kit = definition?.Kit is { Count: > 0 } ? definition.Kit : preset.Kit;

        var style = ParseStyle(definition?.Style, preset.Style);
        var role = ParseRole(definition?.Role, preset.Role);
        var stats = ResolveStats(definition?.Preset, preset, definition?.Stats, veteran, band);

        return new ResolvedBuild
        {
            Style = style,
            Role = role,
            Veteran = veteran,
            Skills = skills,
            Strength = stats.Strength,
            Dexterity = stats.Dexterity,
            Intelligence = stats.Intelligence,
            Kit = kit,
            CanHeal = definition?.CanHeal ?? preset.CanHeal,
            HealInterval = definition?.HealInterval ?? preset.HealInterval
        };
    }

    public static ResolvedBuild ResolvePreset(string name)
    {
        if (string.Equals(name, Archer, StringComparison.OrdinalIgnoreCase))
        {
            return ArcherNovice();
        }

        if (string.Equals(name, Mage, StringComparison.OrdinalIgnoreCase))
        {
            return MageNovice();
        }

        if (string.Equals(name, Swordsman, StringComparison.OrdinalIgnoreCase))
        {
            return SwordsmanNovice();
        }

        if (string.Equals(name, Thief, StringComparison.OrdinalIgnoreCase))
        {
            return ThiefNovice();
        }

        if (string.Equals(name, Tamer, StringComparison.OrdinalIgnoreCase))
        {
            return TamerNovice();
        }

        return WorkerDefault;
    }

    public static ResolvedBuild SwordsmanNovice() => new()
    {
        Style = CombatStyle.Melee,
        Role = CharacterRole.Fighter,
        Veteran = false,
        Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Swords"] = NoviceFightSkill,
            ["Tactics"] = NoviceFightSkill,
            ["Anatomy"] = 50,
            ["Healing"] = 50,
            ["Parry"] = 40,
            ["MagicResist"] = 50
        },
        Strength = NoviceStrength,
        Dexterity = NoviceDexterity,
        Intelligence = NoviceIntelligence,
        Kit = ["Katana", "MetalShield", "ChainChest", "ChainLegs", "ChainCoif", "LeatherGloves", "Boots"],
        CanHeal = true,
        HealInterval = SosariaCombat.DefaultHealIntervalSeconds
    };

    public static ResolvedBuild ArcherNovice() => new()
    {
        Style = CombatStyle.Archer,
        Role = CharacterRole.Fighter,
        Veteran = false,
        Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Archery"] = NoviceFightSkill,
            ["Tactics"] = NoviceFightSkill,
            ["Anatomy"] = 50,
            ["Healing"] = 40,
            ["MagicResist"] = 50
        },
        Strength = ArcherStrength,
        Dexterity = ArcherDexterity,
        Intelligence = ArcherIntelligence,
        Kit = ["Bow", "Arrow", "LeatherChest", "LeatherLegs", "LeatherGorget", "LeatherGloves", "LeatherCap", "Boots"],
        CanHeal = true,
        HealInterval = SosariaCombat.DefaultHealIntervalSeconds
    };

    public static ResolvedBuild MageNovice() => new()
    {
        Style = CombatStyle.Mage,
        Role = CharacterRole.Fighter,
        Veteran = false,
        Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Magery"] = NoviceFightSkill,
            ["EvalInt"] = NoviceFightSkill,
            ["Meditation"] = 60,
            ["MagicResist"] = NoviceFightSkill,
            ["Wrestling"] = 40
        },
        Strength = MageStrength,
        Dexterity = MageDexterity,
        Intelligence = MageIntelligence,
        Kit = [ClassKits.Spellbook, "Robe", "WizardsHat", "Boots", .. ClassKits.AllReagents, ClassKits.RecallRune],
        CanHeal = false,
        HealInterval = SosariaCombat.DefaultHealIntervalSeconds
    };

    public static ResolvedBuild ThiefNovice() => new()
    {
        Style = CombatStyle.Melee,
        Role = CharacterRole.Worker,
        Veteran = false,
        Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Stealing"] = 50,
            ["Snooping"] = 50,
            ["Hiding"] = 40,
            ["Stealth"] = 30,
            ["Lockpicking"] = 30
        },
        Strength = ArcherStrength,
        Dexterity = 80,
        Intelligence = NoviceIntelligence,
        Kit = ["Cloak", "LeatherChest", "LeatherLegs", "Boots"],
        CanHeal = false,
        HealInterval = SosariaCombat.DefaultHealIntervalSeconds
    };

    /// <summary>
    /// The authored tamer is a young mage-tamer: taming leads, lore and veterinary for its
    /// pets, magery, evaluate and meditation to heal itself and fight beside them. It carries
    /// a crook, a spellbook with every reagent, and a rune.
    /// </summary>
    public static ResolvedBuild TamerNovice() => new()
    {
        Style = CombatStyle.Mage,
        Role = CharacterRole.Fighter,
        Veteran = false,
        Skills = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(SkillName.AnimalTaming)] = NoviceTamingSkill,
            [nameof(SkillName.AnimalLore)] = NoviceTamerSkill,
            [nameof(SkillName.Veterinary)] = NoviceTamerSkill,
            [nameof(SkillName.Magery)] = NoviceTamerSkill,
            [nameof(SkillName.EvalInt)] = NoviceTamerSkill,
            [nameof(SkillName.Meditation)] = NoviceTamerSkill,
            [nameof(SkillName.MagicResist)] = NoviceTamerSkill
        },
        Strength = MageStrength,
        Dexterity = MageDexterity,
        Intelligence = MageIntelligence,
        Kit = [TamerCrook, "LeatherChest", "LeatherLegs", "LeatherGloves", "Boots", ClassKits.Spellbook, .. ClassKits.AllReagents, ClassKits.RecallRune],
        CanHeal = false,
        HealInterval = SosariaCombat.DefaultHealIntervalSeconds
    };

    /// <summary>
    /// Preset skills with the authored overrides on top. A veteran trains every one of
    /// them to grandmaster; the sum is then fitted inside the era's 700 cap.
    /// </summary>
    private static Dictionary<string, double> MergeSkills(
        IReadOnlyDictionary<string, double> preset,
        Dictionary<string, double> overrides,
        bool veteran
    )
    {
        var merged = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in preset)
        {
            merged[pair.Key] = veteran ? EraBuildCaps.SkillCap : pair.Value;
        }

        if (overrides != null)
        {
            foreach (var pair in overrides)
            {
                merged[pair.Key] = veteran ? EraBuildCaps.SkillCap : pair.Value;
            }
        }

        return EraBuildCaps.FitSkills(merged);
    }

    private static (int Strength, int Dexterity, int Intelligence) ResolveStats(
        string presetName,
        ResolvedBuild preset,
        StatsDefinition stats,
        bool veteran,
        EraBand band
    )
    {
        if (veteran)
        {
            return VeteranStats(presetName);
        }

        return EraBuildCaps.FitStats(
            stats?.Strength ?? preset.Strength,
            stats?.Dexterity ?? preset.Dexterity,
            stats?.Intelligence ?? preset.Intelligence,
            band
        );
    }

    /// <summary>A veteran's full stat line for its preset, 225 points as the era allowed.</summary>
    private static (int Strength, int Dexterity, int Intelligence) VeteranStats(string presetName)
    {
        if (string.Equals(presetName, Archer, StringComparison.OrdinalIgnoreCase))
        {
            return ClassBuilds.ArcherStats;
        }

        if (string.Equals(presetName, Mage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(presetName, Tamer, StringComparison.OrdinalIgnoreCase))
        {
            return ClassBuilds.CasterStats;
        }

        if (string.Equals(presetName, Swordsman, StringComparison.OrdinalIgnoreCase))
        {
            return ClassBuilds.DexxerStats;
        }

        return string.Equals(presetName, Thief, StringComparison.OrdinalIgnoreCase)
            ? ClassBuilds.ThiefStats
            : ClassBuilds.LumberjackStats;
    }

    private static CombatStyle ParseStyle(string value, CombatStyle fallback)
    {
        if (string.Equals(value, Archer, StringComparison.OrdinalIgnoreCase))
        {
            return CombatStyle.Archer;
        }

        if (string.Equals(value, Mage, StringComparison.OrdinalIgnoreCase))
        {
            return CombatStyle.Mage;
        }

        if (string.Equals(value, "melee", StringComparison.OrdinalIgnoreCase))
        {
            return CombatStyle.Melee;
        }

        return fallback;
    }

    private static CharacterRole ParseRole(string value, CharacterRole fallback)
    {
        if (string.Equals(value, "fighter", StringComparison.OrdinalIgnoreCase))
        {
            return CharacterRole.Fighter;
        }

        if (string.Equals(value, "worker", StringComparison.OrdinalIgnoreCase))
        {
            return CharacterRole.Worker;
        }

        return fallback;
    }
}
