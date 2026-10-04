using Xunit;
using SosariaAI.Skills;

namespace SosariaAI.Tests;

public class IdleWanderSkillTests
{
    [Fact]
    public void MayStayAfterLeaveFailed_InABuilding_IsFalse()
    {
        Assert.False(IdleWanderSkill.MayStayAfterLeaveFailed(isBuilding: true));
    }

    [Fact]
    public void MayStayAfterLeaveFailed_Outdoors_IsTrue()
    {
        Assert.True(IdleWanderSkill.MayStayAfterLeaveFailed(isBuilding: false));
    }
}
