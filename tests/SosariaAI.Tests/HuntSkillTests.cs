using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HuntSkillTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void WalksBackToGround_OnlyOffTheGroundWithNoPreyInReachToChase(bool onGround, bool chasing, bool walksBack) =>
        Assert.Equal(walksBack, HuntSkill.WalksBackToGround(onGround, chasing));
}
