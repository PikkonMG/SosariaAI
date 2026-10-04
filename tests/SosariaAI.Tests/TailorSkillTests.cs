using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TailorSkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new TailorSkill();

        Assert.Equal(TailorRules.Trade.Kind, skill.Name);
    }
}
