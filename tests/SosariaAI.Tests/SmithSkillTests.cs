using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SmithSkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new SmithSkill();

        Assert.Equal(SmithRules.Trade.Kind, skill.Name);
    }
}
