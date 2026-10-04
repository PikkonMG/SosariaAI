using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Spawning;

namespace SosariaAI.Combat;

/// <summary>Item properties of the Age of Shadows kind: how many, and how strong in percent of their range.</summary>
public readonly record struct AosMagic(int Properties, int MinIntensity, int MaxIntensity);

/// <summary>
/// Magic gear was earned in the dungeons and traded at the bank. A novice owns none; an
/// apprentice or journeyman rarely swings a weapon of ruin bought cheap; an expert may
/// own one of might; a master or grandmaster often carries force, power or even
/// vanquishing and wears armor of hardening. Gold buys magic at the bank: a rich person is
/// far likelier to own a piece than a poor one of the same tier, and a novice owns none
/// whatever its purse. A veteran knows what it wields: the piece
/// is identified, so a click shows its classic name. From the Age of Shadows on the same
/// odds give item properties instead, more and stronger with the tier, the way loot
/// rolled them.
/// </summary>
public static class KitMagicRules
{
    public static readonly AosMagic LowAosMagic = new(1, 10, 30);
    public static readonly AosMagic ExpertAosMagic = new(1, 10, 40);
    public static readonly AosMagic AdeptAosMagic = new(2, 20, 50);
    public static readonly AosMagic MasterAosMagic = new(3, 30, 70);
    public static readonly AosMagic GrandmasterAosMagic = new(4, 40, 90);

    /// <summary>Percent of each tier, novice first, that owns a magic weapon.</summary>
    public static readonly int[] WeaponPercentByTier = [0, 2, 5, 10, 20, 35, 55];

    /// <summary>Percent of each tier, novice first, that wears one magic armor piece.</summary>
    public static readonly int[] ArmorPercentByTier = [0, 1, 3, 7, 14, 25, 40];

    /// <summary>How a purse scales the tier's odds, in percent, poor first.</summary>
    public static readonly int[] WealthPercent = [50, 100, 125, 175];

    private const int PercentBase = 100;
    private const int WeaponSalt = 901;
    private const int DamageSalt = 907;
    private const int AccuracySalt = 911;
    private const int ArmorSalt = 919;
    private const int ProtectionSalt = 929;

    // Weights for levels 1 to 5 of the old magic scale, by tier.
    private static readonly int[] LowLevels = [90, 10, 0, 0, 0];
    private static readonly int[] ExpertLevels = [80, 20, 0, 0, 0];
    private static readonly int[] AdeptLevels = [50, 40, 10, 0, 0];
    private static readonly int[] MasterLevels = [30, 40, 25, 5, 0];
    private static readonly int[] GrandmasterLevels = [0, 30, 35, 25, 10];

    public static WeaponDamageLevel WeaponDamage(SkillTier tier, PersonWealth wealth, string uniqueId) =>
        HasMagicWeapon(tier, wealth, uniqueId)
            ? (WeaponDamageLevel)Level(tier, uniqueId, DamageSalt)
            : WeaponDamageLevel.Regular;

    public static WeaponAccuracyLevel WeaponAccuracy(SkillTier tier, PersonWealth wealth, string uniqueId) =>
        HasMagicWeapon(tier, wealth, uniqueId)
            ? (WeaponAccuracyLevel)Level(tier, uniqueId, AccuracySalt)
            : WeaponAccuracyLevel.Regular;

    public static ArmorProtectionLevel ArmorProtection(SkillTier tier, PersonWealth wealth, string uniqueId) =>
        HasMagicArmor(tier, wealth, uniqueId)
            ? (ArmorProtectionLevel)Level(tier, uniqueId, ProtectionSalt)
            : ArmorProtectionLevel.Regular;

    /// <summary>The Second Age knew only the old magic levels; item properties came with the Age of Shadows.</summary>
    public static bool UsesItemProperties(EraBand band) => band != EraBand.T2A;

    /// <summary>How many properties a magic piece of this tier carries, and how strong.</summary>
    public static AosMagic AosMagicFor(SkillTier tier) =>
        tier switch
        {
            SkillTier.Grandmaster => GrandmasterAosMagic,
            SkillTier.Master => MasterAosMagic,
            SkillTier.Adept => AdeptAosMagic,
            SkillTier.Expert => ExpertAosMagic,
            _ => LowAosMagic
        };

    public static bool HasMagicArmor(SkillTier tier, PersonWealth wealth, string uniqueId) =>
        PersonDice.Chance(uniqueId, ArmorSalt, Percent(ArmorPercentByTier, tier, wealth));

    public static bool HasMagicWeapon(SkillTier tier, PersonWealth wealth, string uniqueId) =>
        PersonDice.Chance(uniqueId, WeaponSalt, Percent(WeaponPercentByTier, tier, wealth));

    /// <summary>The tier's odds scaled by the purse.</summary>
    public static int Percent(int[] byTier, SkillTier tier, PersonWealth wealth) =>
        byTier[(int)tier] * WealthPercent[(int)wealth] / PercentBase;

    /// <summary>A level from 1 to 5 on the old magic scale.</summary>
    private static int Level(SkillTier tier, string uniqueId, int salt)
    {
        var weights = tier switch
        {
            SkillTier.Grandmaster => GrandmasterLevels,
            SkillTier.Master => MasterLevels,
            SkillTier.Adept => AdeptLevels,
            SkillTier.Expert => ExpertLevels,
            _ => LowLevels
        };

        return PersonDice.Weighted(uniqueId, salt, weights) + 1;
    }
}
