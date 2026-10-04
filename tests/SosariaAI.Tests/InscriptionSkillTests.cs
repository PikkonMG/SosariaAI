using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class InscriptionSkillTests
{
    [Fact]
    public void Skill_IsNamedForItsTrade()
    {
        var skill = new InscriptionSkill();

        Assert.Equal(InscriptionRules.Trade.Kind, skill.Name);
    }
}
