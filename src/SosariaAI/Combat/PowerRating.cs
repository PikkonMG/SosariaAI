using System;

namespace SosariaAI.Combat;

/// <summary>
/// Pure combat power from caller-supplied numbers. No world reads.
/// </summary>
public static class PowerRating
{
    public const int GrowthLogStep = 10;

    public const double WeaponSkillWeight = 0.20;
    public const double TacticsWeight = 0.15;
    public const double AnatomyWeight = 0.10;
    public const double EvalIntWeight = 0.15;
    public const double StrengthWeight = 0.08;
    public const double DexterityWeight = 0.08;
    public const double IntelligenceWeight = 0.04;
    public const double ManaWeight = 0.10;
    public const double GearWeight = 1.50;
    public const double BandageWeight = 0.25;
    public const double PotionWeight = 0.50;
    public const double ReagentWeight = 0.45;
    public const int MaxBandagesCounted = 20;
    public const int MaxPotionsCounted = 10;
    public const int MaxReagentsCounted = 80;
    public const double HitsFloor = 0.4;
    public const double MinHitsFraction = 0.0;
    public const double MaxHitsFraction = 1.0;

    public static int Score(
        int weaponSkill,
        int tactics,
        int anatomy,
        int strength,
        int dexterity,
        int intelligence,
        double hitsFraction,
        int gearScore,
        int bandageCount,
        int potionCount,
        int evalInt = 0,
        int mana = 0,
        int reagentCount = 0
    )
    {
        var skills =
            Math.Max(0, weaponSkill) * WeaponSkillWeight +
            Math.Max(0, tactics) * TacticsWeight +
            Math.Max(0, anatomy) * AnatomyWeight +
            Math.Max(0, evalInt) * EvalIntWeight;

        var stats =
            Math.Max(0, strength) * StrengthWeight +
            Math.Max(0, dexterity) * DexterityWeight +
            Math.Max(0, intelligence) * IntelligenceWeight +
            Math.Max(0, mana) * ManaWeight;

        var gear = Math.Max(0, gearScore) * GearWeight;
        var bandages = Math.Min(Math.Max(0, bandageCount), MaxBandagesCounted) * BandageWeight;
        var potions = Math.Min(Math.Max(0, potionCount), MaxPotionsCounted) * PotionWeight;
        var reagents = Math.Min(Math.Max(0, reagentCount), MaxReagentsCounted) * ReagentWeight;
        var hits = Math.Clamp(hitsFraction, MinHitsFraction, MaxHitsFraction);
        var hitsFactor = HitsFloor + hits * (MaxHitsFraction - HitsFloor);
        var raw = (skills + stats + gear + bandages + potions + reagents) * hitsFactor;
        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    public static bool CrossedThreshold(int previous, int current) =>
        current > previous &&
        (previous == 0 || previous / GrowthLogStep != current / GrowthLogStep);
}
