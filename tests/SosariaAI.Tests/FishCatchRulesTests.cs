using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class FishCatchRulesTests
{
    private const double Grandmaster = 100.0;
    private const double BelowBottleSkill = 59.9;
    private const double BottleButNotParchmentSkill = 70.0;
    private const int FirstRoll = 0;
    private const int ParchmentRoll = FishCatchRules.BottlePerMille;
    private const int PlainRoll = FishCatchRules.BottlePerMille + FishCatchRules.ParchmentPerMille;
    private const int NegativeFirstRoll = -FishCatchRules.PerMille;

    [Fact]
    public void DeepWater_LeavesItToTheEngine() =>
        Assert.Equal(ShoreFind.None, FishCatchRules.Roll(Grandmaster, deepWater: true, FirstRoll));

    [Fact]
    public void LowSkill_FindsNothing() =>
        Assert.Equal(ShoreFind.None, FishCatchRules.Roll(BelowBottleSkill, deepWater: false, FirstRoll));

    [Fact]
    public void ShoreRolls_BottleThenParchmentThenFish()
    {
        Assert.Equal(ShoreFind.Bottle, FishCatchRules.Roll(Grandmaster, false, FirstRoll));
        Assert.Equal(ShoreFind.Bottle, FishCatchRules.Roll(Grandmaster, false, NegativeFirstRoll));
        Assert.Equal(ShoreFind.Parchment, FishCatchRules.Roll(Grandmaster, false, ParchmentRoll));
        Assert.Equal(ShoreFind.None, FishCatchRules.Roll(BottleButNotParchmentSkill, false, ParchmentRoll));
        Assert.Equal(ShoreFind.None, FishCatchRules.Roll(Grandmaster, false, PlainRoll));
    }

    [Fact]
    public void Finds_StayRare() =>
        Assert.True(FishCatchRules.BottlePerMille + FishCatchRules.ParchmentPerMille < FishCatchRules.PerMille / 100);
}
