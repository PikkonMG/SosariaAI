using SosariaAI.Combat;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class PowerRatingTests
{
    private const int VeteranSkill = 100;
    private const int VeteranStrength = 100;
    private const int VeteranDexterity = 100;
    private const int VeteranIntelligence = 50;

    private const int NoviceSkill = 40;
    private const int VeteranGearScore = 40;
    private const int VeteranBandages = 10;
    private const int VeteranPotions = 5;
    private const int NoSupplies = 0;
    private const int NoSkill = 0;
    private const int MageArmorPieces = 2;
    private const int ArcherArmorPieces = 5;
    private const double LowHits = 0.25;
    private const int VeteranScoreMin = 110;
    private const int VeteranScoreMax = 140;
    private const int NoviceScoreMax = 70;
    private const int MinGearAdvantage = 30;
    private const int UnsampledPower = 0;
    private const int FirstSampleSameBand = PowerRating.GrowthLogStep - 1;
    private const int BelowThreshold = 99;
    private const int AtThreshold = 100;
    private const int SameBandMax = 109;

    private static int VeteranScore(
        int gearScore = VeteranGearScore,
        double hitsFraction = PowerRating.MaxHitsFraction,
        int bandageCount = VeteranBandages,
        int potionCount = VeteranPotions
    ) =>
        PowerRating.Score(
            VeteranSkill,
            VeteranSkill,
            VeteranSkill,
            VeteranStrength,
            VeteranDexterity,
            VeteranIntelligence,
            hitsFraction,
            gearScore,
            bandageCount,
            potionCount
        );

    private static int NoviceScore() =>
        PowerRating.Score(
            NoviceSkill,
            NoviceSkill,
            NoviceSkill,
            BuildPresets.NoviceStrength,
            BuildPresets.NoviceDexterity,
            BuildPresets.NoviceIntelligence,
            PowerRating.MaxHitsFraction,
            GearScore.UnarmedScore,
            NoSupplies,
            NoSupplies
        );

    [Fact]
    public void Score_VeteranVsNovice_VeteranIsHigher()
    {
        var veteran = VeteranScore();
        var novice = NoviceScore();

        Assert.InRange(veteran, VeteranScoreMin, VeteranScoreMax);
        Assert.True(novice < NoviceScoreMax);
        Assert.True(veteran > novice);
    }

    [Fact]
    public void Score_NakedVsGeared_NakedIsMuchLower()
    {
        var naked = VeteranScore(gearScore: GearScore.UnarmedScore);
        var geared = VeteranScore(gearScore: VeteranGearScore);

        Assert.True(geared - naked >= MinGearAdvantage);
    }

    [Fact]
    public void Score_LowHits_LowersScore()
    {
        var full = VeteranScore(hitsFraction: PowerRating.MaxHitsFraction);
        var low = VeteranScore(hitsFraction: LowHits);

        Assert.True(low < full);
    }

    [Fact]
    public void Score_Bandages_RaiseScore()
    {
        var none = VeteranScore(bandageCount: NoSupplies);
        var some = VeteranScore(bandageCount: VeteranBandages);

        Assert.True(some > none);
    }

    [Fact]
    public void Score_VeteranMage_RatesAtLeastAsHighAsVeteranArcher()
    {
        var mageBuild = BuildPresets.Resolve(new() { Preset = BuildPresets.Mage, Veteran = true });
        var archerBuild = BuildPresets.Resolve(new() { Preset = BuildPresets.Archer, Veteran = true });
        var mageGear = GearScore.WeaponScore + GearScore.ArmorPieceScore * MageArmorPieces;
        var archerGear = GearScore.WeaponScore + GearScore.ArmorPieceScore * ArcherArmorPieces;

        var mage = PowerRating.Score(
            (int)mageBuild.Skills["Magery"],
            NoSkill,
            NoSkill,
            mageBuild.Strength,
            mageBuild.Dexterity,
            mageBuild.Intelligence,
            PowerRating.MaxHitsFraction,
            mageGear,
            NoSupplies,
            NoSupplies,
            (int)mageBuild.Skills["EvalInt"],
            mageBuild.Intelligence,
            PowerRating.MaxReagentsCounted
        );
        var archer = PowerRating.Score(
            (int)archerBuild.Skills["Archery"],
            (int)archerBuild.Skills["Tactics"],
            (int)archerBuild.Skills["Anatomy"],
            archerBuild.Strength,
            archerBuild.Dexterity,
            archerBuild.Intelligence,
            PowerRating.MaxHitsFraction,
            archerGear,
            VeteranBandages,
            NoSupplies
        );

        Assert.True(mage >= archer);
    }

    [Fact]
    public void Score_EvalIntManaAndReagents_RaiseAMage()
    {
        var without = PowerRating.Score(
            VeteranSkill,
            NoSkill,
            NoSkill,
            VeteranStrength,
            VeteranDexterity,
            VeteranIntelligence,
            PowerRating.MaxHitsFraction,
            GearScore.UnarmedScore,
            NoSupplies,
            NoSupplies
        );
        var with = PowerRating.Score(
            VeteranSkill,
            NoSkill,
            NoSkill,
            VeteranStrength,
            VeteranDexterity,
            VeteranIntelligence,
            PowerRating.MaxHitsFraction,
            GearScore.UnarmedScore,
            NoSupplies,
            NoSupplies,
            VeteranSkill,
            VeteranIntelligence,
            PowerRating.MaxReagentsCounted
        );

        Assert.True(with > without);
    }

    [Fact]
    public void CrossedThreshold_FirstSample_IsTrue()
    {
        Assert.True(PowerRating.CrossedThreshold(UnsampledPower, FirstSampleSameBand));
        Assert.True(PowerRating.CrossedThreshold(UnsampledPower, AtThreshold));
    }

    [Fact]
    public void CrossedThreshold_NinetyNineToOneHundred_IsTrue() =>
        Assert.True(PowerRating.CrossedThreshold(BelowThreshold, AtThreshold));

    [Fact]
    public void CrossedThreshold_OneHundredToOneHundredNine_IsFalse() =>
        Assert.False(PowerRating.CrossedThreshold(AtThreshold, SameBandMax));
}
