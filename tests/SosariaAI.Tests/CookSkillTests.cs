using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CookSkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new CookSkill();

        Assert.Equal(CookRules.Trade.Kind, skill.Name);
    }
}
