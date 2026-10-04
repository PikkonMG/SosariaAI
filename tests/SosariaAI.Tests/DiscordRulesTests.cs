using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class DiscordRulesTests
{
    private const double ExpectedPracticeMin = 0;
    private const double ExpectedPracticeMax = 120;
    private const int RawCombatStat = 100;
    private const double NoDiscordance = 0;
    private const double HalfDiscordance = 50;
    private const double FullDiscordance = 100;
    private const int NoStatOffset = 0;
    private const int HalfStatOffset = -10;
    private const int FullStatOffset = -20;

    [Fact]
    public void DiscordSkill_Name_IsDiscord() =>
        Assert.Equal(SkillKinds.Discord, new DiscordSkill().Name);

    [Fact]
    public void DiscordSkill_Begin_NoBard_IsFalse() =>
        Assert.False(new DiscordSkill().Begin(null));

    [Fact]
    public void PracticeWindow_MatchesModernUODiscordanceCheck()
    {
        Assert.Equal(ExpectedPracticeMin, DiscordRules.PracticeMin);
        Assert.Equal(ExpectedPracticeMax, DiscordRules.PracticeMax);
    }

    [Fact]
    public void IsDiscordTarget_MatchesTheEngineTargetCheck()
    {
        Assert.True(DiscordRules.IsDiscordTarget(isSelf: false, isPlayer: false, bardImmune: false, canHarm: true, inDiscord: false));
        Assert.False(DiscordRules.IsDiscordTarget(isSelf: true, isPlayer: false, bardImmune: false, canHarm: true, inDiscord: false));
        Assert.False(DiscordRules.IsDiscordTarget(isSelf: false, isPlayer: true, bardImmune: false, canHarm: true, inDiscord: false));
        Assert.False(DiscordRules.IsDiscordTarget(isSelf: false, isPlayer: false, bardImmune: true, canHarm: true, inDiscord: false));
        Assert.False(DiscordRules.IsDiscordTarget(isSelf: false, isPlayer: false, bardImmune: false, canHarm: false, inDiscord: false));
        Assert.False(DiscordRules.IsDiscordTarget(isSelf: false, isPlayer: false, bardImmune: false, canHarm: true, inDiscord: true));
    }

    [Fact]
    public void CombatStatOffset_LowersStatsFromDiscordance()
    {
        Assert.Equal(NoStatOffset, DiscordRules.CombatStatOffset(RawCombatStat, NoDiscordance));
        Assert.Equal(HalfStatOffset, DiscordRules.CombatStatOffset(RawCombatStat, HalfDiscordance));
        Assert.Equal(FullStatOffset, DiscordRules.CombatStatOffset(RawCombatStat, FullDiscordance));
    }
}
