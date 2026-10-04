using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class BegRulesTests
{
    private const int ExpectedReachTiles = 2;
    private const double ExpectedPracticeMin = 0;
    private const double ExpectedPracticeMax = 100;

    [Fact]
    public void BegSkill_Name_IsBeg() =>
        Assert.Equal(SkillKinds.Beg, new BegSkill().Name);

    [Fact]
    public void Reach_IsTwoTiles() =>
        Assert.Equal(ExpectedReachTiles, BegRules.ReachTiles);

    [Fact]
    public void PracticeWindow_MatchesModernUOBeggingCheck()
    {
        Assert.Equal(ExpectedPracticeMin, BegRules.PracticeMin);
        Assert.Equal(ExpectedPracticeMax, BegRules.PracticeMax);
    }

    [Fact]
    public void BegSkill_Begin_NoBeggar_IsNotInTheWorld()
    {
        var beg = new BegSkill();

        Assert.False(beg.Begin(null));
        Assert.Equal(Skill.NotInWorldReason, beg.FailReason);
    }

    [Fact]
    public void IsBegTarget_AHumanNpcOnly()
    {
        Assert.True(BegRules.IsBegTarget(isSelf: false, isPlayer: false, humanBody: true));
        Assert.False(BegRules.IsBegTarget(isSelf: true, isPlayer: false, humanBody: true));
        Assert.False(BegRules.IsBegTarget(isSelf: false, isPlayer: true, humanBody: true));
        Assert.False(BegRules.IsBegTarget(isSelf: false, isPlayer: false, humanBody: false));
    }

    [Fact]
    public void MayBegMounted_OnlyFromMondainsLegacy()
    {
        Assert.True(BegRules.MayBegMounted(mounted: false, mondainsLegacy: false));
        Assert.False(BegRules.MayBegMounted(mounted: true, mondainsLegacy: false));
        Assert.True(BegRules.MayBegMounted(mounted: true, mondainsLegacy: true));
    }
}
