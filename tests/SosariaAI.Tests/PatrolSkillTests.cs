using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PatrolSkillTests
{
    [Fact]
    public void FinishStatus_FailsWhenNothingReached()
    {
        Assert.Equal(SkillStatus.Failed, PatrolSkill.FinishStatus(0, 2));
        Assert.Equal(SkillStatus.Failed, PatrolSkill.FinishStatus(0, 0));
    }

    [Fact]
    public void FinishStatus_FailsWhenSkipsDominate()
    {
        // The trapped loop: one point in range, the rest with no route. Done cleared
        // the failure count and the same dead lap restarted every few seconds.
        Assert.Equal(SkillStatus.Failed, PatrolSkill.FinishStatus(1, 2));
        Assert.Equal(SkillStatus.Failed, PatrolSkill.FinishStatus(2, 5));
    }

    [Fact]
    public void FinishStatus_DoneWhenMostPointsReached()
    {
        Assert.Equal(SkillStatus.Done, PatrolSkill.FinishStatus(3, 1));
        Assert.Equal(SkillStatus.Done, PatrolSkill.FinishStatus(2, 2));
        Assert.Equal(SkillStatus.Done, PatrolSkill.FinishStatus(4, 0));
    }
}
