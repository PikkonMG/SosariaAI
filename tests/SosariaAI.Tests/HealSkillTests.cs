using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HealSkillTests
{
    [Fact]
    public void Name_IsHeal() =>
        Assert.Equal("Heal", new HealSkill().Name);
}
