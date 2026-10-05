using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HuntSkillTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, false, true, false)]
    public void WalksBackToGround_OnlyOffTheGroundWithNoPreyToChaseAndAMiddleNotRunFrom(
        bool onGround,
        bool chasing,
        bool middleAvoided,
        bool walksBack
    ) =>
        Assert.Equal(walksBack, HuntSkill.WalksBackToGround(onGround, chasing, middleAvoided));
}
