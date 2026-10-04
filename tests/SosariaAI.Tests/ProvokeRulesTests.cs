using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class ProvokeRulesTests
{
    private const int ExpectedPartnerReachTiles = 8;
    private const double ExpectedPracticeMin = 0;
    private const double ExpectedPracticeMax = 120;

    [Fact]
    public void ProvokeSkill_Name_IsProvoke() =>
        Assert.Equal(SkillKinds.Provoke, new ProvokeSkill().Name);

    [Fact]
    public void ProvokeSkill_Begin_NoBard_IsFalse() =>
        Assert.False(new ProvokeSkill().Begin(null));

    [Fact]
    public void PartnerReach_IsEightTiles() =>
        Assert.Equal(ExpectedPartnerReachTiles, ProvokeRules.PartnerReachTiles);

    [Fact]
    public void PracticeWindow_MatchesModernUOProvocationCheck()
    {
        Assert.Equal(ExpectedPracticeMin, ProvokeRules.PracticeMin);
        Assert.Equal(ExpectedPracticeMax, ProvokeRules.PracticeMax);
    }

    [Fact]
    public void IsProvokeTarget_AWildCreatureTheBardMayHarm()
    {
        Assert.True(Provokable());
        Assert.False(Provokable(isSelf: true));
        Assert.False(Provokable(isCreature: false));
        Assert.False(Provokable(controlled: true));
        Assert.False(Provokable(unprovokable: true));
        Assert.False(Provokable(canHarm: false));
    }

    [Fact]
    public void IsProvokeTarget_NotAnInnocentAtTheBank()
    {
        // The engine scolds a bard who provokes an innocent and takes karma: the bank's
        // vendors and town animals are no targets.
        Assert.False(Provokable(innocent: true));
    }

    private static bool Provokable(
        bool isSelf = false,
        bool isCreature = true,
        bool controlled = false,
        bool unprovokable = false,
        bool canHarm = true,
        bool innocent = false
    ) =>
        ProvokeRules.IsProvokeTarget(isSelf, isCreature, controlled, unprovokable, canHarm, innocent);
}
