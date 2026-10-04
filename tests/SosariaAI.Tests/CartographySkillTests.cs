using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CartographySkillTests
{
    private const string CartographyName = "Cartography";

    [Fact]
    public void Name_IsCartography()
    {
        var skill = new CartographySkill();
        Assert.Equal(CartographyName, skill.Name);
        Assert.Equal(CartographyRules.Kind, skill.Name);
    }

    [Fact]
    public void Begin_NullCharacter_IsFalse() =>
        Assert.False(new CartographySkill().Begin(null));

    [Fact]
    public void EveryPart_CarriesTheCartographyName()
    {
        Assert.Equal(CartographyName, new CartographyCraftSkill().Name);
        Assert.Equal(CartographyName, new TreasureHuntSkill(null).Name);
        Assert.Equal(CartographyName, new TreasureMapSellSkill(null).Name);
        Assert.Equal(CartographyName, new TreasureMapBuySkill().Name);
    }

    [Fact]
    public void TreasureHunt_WithoutAMap_DoesNotBegin() =>
        Assert.False(new TreasureHuntSkill(null).Begin(null));
}
