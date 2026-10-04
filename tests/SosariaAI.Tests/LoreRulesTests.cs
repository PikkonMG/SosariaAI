using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class LoreRulesTests
{
    private const string ExpectedKind = "Lore";
    private const int ExpectedReachTiles = 8;
    private const double ExpectedPracticeMin = 0;
    private const double ExpectedPracticeMax = 120;
    private const double LowLore = 50;
    private const uint LoneSerial = 0x8B01;

    [Fact]
    public void Kind_IsLore()
    {
        Assert.Equal(ExpectedKind, LoreRules.Kind);
        Assert.Equal(ExpectedKind, new LoreSkill().Name);
    }

    [Fact]
    public void Reach_IsEightTiles() =>
        Assert.Equal(ExpectedReachTiles, LoreRules.ReachTiles);

    [Fact]
    public void PracticeWindow_MatchesModernUOAnimalLoreCheck()
    {
        Assert.Equal(ExpectedPracticeMin, LoreRules.PracticeMin);
        Assert.Equal(ExpectedPracticeMax, LoreRules.PracticeMax);
    }

    [Fact]
    public void MayLore_TheEnginesTarget_ATamePetAtAnySkill()
    {
        Assert.True(LoreRules.MayLore(beastBody: true, deadPet: false, controlled: true, tamable: true, LowLore));
        Assert.False(LoreRules.MayLore(beastBody: false, deadPet: false, controlled: true, tamable: true, LowLore));
        Assert.False(LoreRules.MayLore(beastBody: true, deadPet: true, controlled: true, tamable: true, LowLore));
    }

    [Fact]
    public void MayLore_AWildBeastOnlyForAMasterOfLore()
    {
        Assert.False(LoreRules.MayLore(true, false, controlled: false, tamable: true, LowLore));
        Assert.True(LoreRules.MayLore(true, false, controlled: false, tamable: true, LoreRules.WildLoreSkill));
        Assert.False(LoreRules.MayLore(true, false, controlled: false, tamable: false, LoreRules.WildLoreSkill));
        Assert.True(LoreRules.MayLore(true, false, controlled: false, tamable: false, LoreRules.UntamableLoreSkill));
    }

    [Fact]
    public void Begin_NoBeastInReach_SaysSo()
    {
        TestMap.EnsureInternal();
        var skill = new LoreSkill();

        Assert.False(skill.Begin(new SosariaAI.Mobiles.SosariaCharacter((Serial)LoneSerial)));
        Assert.Equal(LoreRules.NoSubjectWhy, skill.FailReason);
    }
}
