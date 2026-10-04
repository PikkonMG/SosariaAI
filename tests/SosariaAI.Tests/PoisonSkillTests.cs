using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PoisonSkillTests
{
    private const string ExpectedName = "Poison";

    [Fact]
    public void Name_IsPoison()
    {
        Assert.Equal(ExpectedName, new PoisonSkill().Name);
        Assert.Equal(PoisonRules.Kind, new PoisonSkill().Name);
    }
}
