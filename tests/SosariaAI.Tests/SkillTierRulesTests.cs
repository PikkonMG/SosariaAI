using System;
using System.Linq;
using SosariaAI.Combat;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class SkillTierRulesTests
{
    private const int People = 4000;
    private const int Salt = 17;

    /// <summary>The share of tamers, in percent, the tamer weights put at Expert or better.</summary>
    private const int TamerExpertOrBetterPercent = 85;

    [Fact]
    public void Roll_FallsOnABellCurve_WithFewGrandmasters()
    {
        var counts = new int[Enum.GetValues<SkillTier>().Length];

        for (var i = 0; i < People; i++)
        {
            counts[(int)SkillTierRules.Roll($"Felucca:mira#{i}", Salt, PersonClass.Warrior)]++;
        }

        var grandmasters = counts[(int)SkillTier.Grandmaster];
        var middle = counts[(int)SkillTier.Journeyman] + counts[(int)SkillTier.Expert];

        Assert.All(counts, count => Assert.True(count > 0));
        Assert.True(grandmasters < People / 12, $"{grandmasters} grandmasters");
        Assert.True(middle > People / 3, $"{middle} in the middle");
        Assert.True(counts[(int)SkillTier.Grandmaster] < counts[(int)SkillTier.Novice]);
    }

    [Fact]
    public void Roll_IsStableForOneId() =>
        Assert.Equal(
            SkillTierRules.Roll("Felucca:mira#9", Salt, PersonClass.Warrior),
            SkillTierRules.Roll("Felucca:mira#9", Salt, PersonClass.Warrior));

    [Fact]
    public void TamerWeights_SumToOneHundred_WithTheNamedShareExpertOrBetter()
    {
        Assert.Equal(100, SkillTierRules.TamerWeights.Sum());
        Assert.Equal(
            TamerExpertOrBetterPercent,
            SkillTierRules.TamerWeights.Skip((int)SkillTier.Expert).Sum());
    }

    [Fact]
    public void Roll_Tamers_AreMostlyExpertOrBetter_AndLowTiersRare()
    {
        var counts = new int[Enum.GetValues<SkillTier>().Length];

        for (var i = 0; i < People; i++)
        {
            counts[(int)SkillTierRules.Roll($"Felucca:osric#{i}", Salt, PersonClass.Tamer)]++;
        }

        var high = counts.Skip((int)SkillTier.Expert).Sum();
        var lowShare = (double)counts[(int)SkillTier.Novice] / People;

        // Live run: 163 tamers were 25 novice, 26 apprentice and 39 journeyman; only 9 grandmasters.
        Assert.True(high > People * (TamerExpertOrBetterPercent - 5) / 100, $"{high} expert or better");
        Assert.True(lowShare < 0.06, $"{lowShare:P0} novice");
        Assert.True(counts[(int)SkillTier.Grandmaster] > counts[(int)SkillTier.Journeyman]);
    }

    [Fact]
    public void WeightsFor_OnlyTamersLeanHigh()
    {
        Assert.Same(SkillTierRules.TamerWeights, SkillTierRules.WeightsFor(PersonClass.Tamer));
        Assert.Same(SkillTierRules.Weights, SkillTierRules.WeightsFor(PersonClass.Mage));
    }

    [Fact]
    public void ForFixture_KeepsTheAuthoredVeteranFlag()
    {
        for (var i = 0; i < 20; i++)
        {
            Assert.True(SkillTierRules.IsVeteran(SkillTierRules.ForFixture(true, $"Felucca:bran{i}", Salt)));
            Assert.False(SkillTierRules.IsVeteran(SkillTierRules.ForFixture(false, $"Felucca:tam{i}", Salt)));
        }
    }

    [Fact]
    public void Weights_SumToOneHundred() => Assert.Equal(100, SkillTierRules.Weights.Sum());

    [Fact]
    public void PrimarySkill_RisesWithTier()
    {
        var tiers = Enum.GetValues<SkillTier>();

        for (var i = 1; i < tiers.Length; i++)
        {
            Assert.True(SkillTierRules.PrimarySkill(tiers[i]) > SkillTierRules.PrimarySkill(tiers[i - 1]));
        }

        Assert.Equal(EraBuildCaps.SkillCap, SkillTierRules.PrimarySkill(SkillTier.Grandmaster));
    }
}
