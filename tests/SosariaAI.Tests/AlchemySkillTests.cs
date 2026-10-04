using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class AlchemySkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new AlchemySkill();

        Assert.Equal(AlchemyRules.Trade.Kind, skill.Name);
    }
}
