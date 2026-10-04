using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class DetectHiddenRulesTests
{
    [Fact]
    public void Reach_IsEightTiles() =>
        Assert.Equal(8, DetectHiddenRules.ReachTiles);

    [Fact]
    public void PracticeWindow_MatchesModernUODetectHiddenCheck()
    {
        Assert.Equal(0, DetectHiddenRules.PracticeMin);
        Assert.Equal(100, DetectHiddenRules.PracticeMax);
    }

    [Fact]
    public void IsHiddenTarget_RequiresHiddenAndNotSelf()
    {
        Assert.False(DetectHiddenRules.IsHiddenTarget(hidden: false, isSelf: false));
        Assert.False(DetectHiddenRules.IsHiddenTarget(hidden: true, isSelf: true));
        Assert.True(DetectHiddenRules.IsHiddenTarget(hidden: true, isSelf: false));
    }

    [Fact]
    public void DetectHiddenSkill_Name_IsDetectHidden() =>
        Assert.Equal(SkillKinds.DetectHidden, new DetectHiddenSkill().Name);

    [Fact]
    public void DetectHiddenSkill_Begin_NoDetector_IsFalse() =>
        Assert.False(new DetectHiddenSkill().Begin(null));

    [Fact]
    public void HasWork_NoDetector_IsFalse() =>
        Assert.False(DetectHiddenSkill.HasWork(null));
}
