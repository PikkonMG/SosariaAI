using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class FletchSkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new FletchSkill();

        Assert.Equal(FletchRules.Trade.Kind, skill.Name);
    }
}
