using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CarpentrySkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new CarpentrySkill();

        Assert.Equal(CarpentryRules.Trade.Kind, skill.Name);
    }
}
