using System.Collections.Generic;
using SosariaAI.Configuration;

namespace SosariaAI.Combat;

/// <summary>
/// What a creature brings beside its blows: the Magery it casts with (zero for one that casts
/// nothing), a breath, the level of the poison its blows carry (zero for none, one for lesser up
/// to five for lethal), and a bow or a thrown weapon that hits from afar.
/// </summary>
public readonly record struct HostileArts(int SpellSkill, bool Breath, int PoisonLevel, bool Ranged);

/// <summary>A foe's hits, strength and average blow, and its arts (none unless given).</summary>
public readonly record struct HostileStats(int Hits, int Strength, int AverageDamage, HostileArts Arts = default);

/// <summary>
/// Pure threat math. No world objects.
/// One troll (110 hits, 190 strength, 11 average damage) is a coin flip versus power 120.
/// A veteran with healing opens on one troll and not two. Without healing they open on neither.
/// Two equal foes score ExtraFoeShare above a 1:1 add (1.0 + 0.85 = 1.85 times the stronger).
/// A foe's arts raise its own score by a share each (<see cref="ArtsFactor"/>): a dread spider
/// casts at Magery 72 and bites with lethal poison, and five fighters of power 128 to 225 died
/// to spiders their blows alone rated at 146.
/// </summary>
public static class ThreatRating
{
    public const double DefaultThreatMultiple = 1.15;

    /// <summary>
    /// The threat that sends a character running and marks a lesser creature harmless. It is
    /// career.minThreatToFlee in characters.json (<see cref="CareerSettings.FleeThreshold"/>).
    /// </summary>
    public static int MinThreatToFlee => CareerSettings.FleeThreshold(SosariaSettings.Characters?.Career);

    public const double OpenFightHitsFraction = 0.70;
    public const double NoHealingCaution = 0.70;
    public const double PartyPowerShare = 0.85;
    public const double SingleFoeFactor = 1.0;
    public const double ExtraFoeShare = 0.85;
    public const int StrengthDivisor = 20;
    public const int DamageWeight = 1;

    /// <summary>A caster at this Magery or more casts the great spells: its threat rises by <see cref="MasterSpellShare"/>.</summary>
    public const int MasterSpellSkill = 80;

    /// <summary>A caster at this Magery or more, below <see cref="MasterSpellSkill"/>, rises by <see cref="AdeptSpellShare"/>.</summary>
    public const int AdeptSpellSkill = 50;

    public const double MasterSpellShare = 0.5;
    public const double AdeptSpellShare = 0.3;

    /// <summary>A caster below <see cref="AdeptSpellSkill"/> still throws a bolt now and then.</summary>
    public const double NoviceSpellShare = 0.15;

    /// <summary>A breath strikes from afar through armour.</summary>
    public const double BreathShare = 0.3;

    /// <summary>Lesser poison on a foe's blows raises its threat this much; each level up adds <see cref="PoisonSharePerLevel"/>.</summary>
    public const double PoisonShareBase = 0.1;

    public const double PoisonSharePerLevel = 0.05;

    /// <summary>The poison level of a foe whose blows carry none.</summary>
    public const int NoPoison = 0;

    /// <summary>A foe that shoots from afar is hit less often than it hits.</summary>
    public const double RangedShare = 0.15;

    private const double BareFactor = 1.0;

    public static int Score(IReadOnlyList<HostileStats> hostiles)
    {
        if (hostiles == null || hostiles.Count == 0)
        {
            return 0;
        }

        var strongest = 0;

        for (var i = 0; i < hostiles.Count; i++)
        {
            var score = OfOne(hostiles[i]);

            if (score > strongest)
            {
                strongest = score;
            }
        }

        var extraFoes = hostiles.Count - 1;
        var countFactor = SingleFoeFactor + ExtraFoeShare * extraFoes;
        return (int)(strongest * countFactor);
    }

    public static bool ShouldEngage(
        int power,
        int threat,
        double hitsFraction,
        bool hasHealing,
        int alliesPower,
        double threatMultiple,
        double minHitsToOpen,
        bool alreadyAttacked
    )
    {
        if (alreadyAttacked)
        {
            return true;
        }

        if (hitsFraction < minHitsToOpen)
        {
            return false;
        }

        double effectivePower = power + (int)(alliesPower * PartyPowerShare);

        if (!hasHealing)
        {
            effectivePower *= NoHealingCaution;
        }

        return threat <= effectivePower * threatMultiple;
    }

    /// <summary>One foe alone: its hits, a share of its strength and its average blow, raised by its arts.</summary>
    public static int OfOne(HostileStats hostile) =>
        (int)((hostile.Hits + hostile.Strength / StrengthDivisor + hostile.AverageDamage * DamageWeight) * ArtsFactor(hostile.Arts));

    /// <summary>What a foe's arts multiply its threat by: one for a foe with none.</summary>
    public static double ArtsFactor(HostileArts arts) =>
        BareFactor +
        SpellShare(arts.SpellSkill) +
        (arts.Breath ? BreathShare : 0) +
        PoisonShare(arts.PoisonLevel) +
        (arts.Ranged ? RangedShare : 0);

    private static double SpellShare(int spellSkill) =>
        spellSkill >= MasterSpellSkill ? MasterSpellShare
        : spellSkill >= AdeptSpellSkill ? AdeptSpellShare
        : spellSkill > 0 ? NoviceSpellShare
        : 0;

    private static double PoisonShare(int poisonLevel) =>
        poisonLevel <= NoPoison ? 0 : PoisonShareBase + (poisonLevel - 1) * PoisonSharePerLevel;
}
