using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class HarmlessCreaturesTests
{
    [Fact]
    public void IsHarmless_RatsBirdsSheep()
    {
        Assert.True(HarmlessCreatures.LooksNamed("Rat"));
        Assert.True(HarmlessCreatures.LooksNamed("Bird"));
        Assert.True(HarmlessCreatures.LooksNamed("Sheep"));
        Assert.True(HarmlessCreatures.IsHarmless("GiantRat", 8));
        Assert.False(HarmlessCreatures.LooksNamed("Wraith"));
        Assert.False(HarmlessCreatures.IsHarmless("Wraith", 80));
    }

    [Fact]
    public void LooksNamed_ReadsTheLastWord_SoRatmenAndPiratesAreFoes()
    {
        Assert.True(HarmlessCreatures.LooksNamed("SewerRat"));
        Assert.True(HarmlessCreatures.LooksNamed("TropicalBird"));
        Assert.False(HarmlessCreatures.LooksNamed("Ratman"));
        Assert.False(HarmlessCreatures.LooksNamed("RatmanArcher"));
        Assert.False(HarmlessCreatures.LooksNamed("Pirate"));
    }
}
