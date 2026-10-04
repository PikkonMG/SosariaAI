using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TinkerSkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new TinkerSkill();

        Assert.Equal(TinkerRules.Trade.Kind, skill.Name);
    }
}
