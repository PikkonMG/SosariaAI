using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CartographyRulesTests
{
    private const int BritainMapmakerX = 1416;
    private const int BritainMapmakerY = 1754;
    private const int BritainMapmakerZ = 10;
    private const string ExpectedKind = "Cartography";

    [Fact]
    public void Trade_DrawsMapsAtTheBritainMapmaker()
    {
        var mapmaker = new Point3D(BritainMapmakerX, BritainMapmakerY, BritainMapmakerZ);

        Assert.Equal(mapmaker, CartographyRules.BritainMapmaker);
        Assert.Equal(mapmaker, CartographyRules.Trade.FallbackShop);
        Assert.Equal(SkillName.Cartography, CartographyRules.Trade.Skill);
        Assert.Equal(CraftStation.None, CartographyRules.Trade.Station);
        Assert.Equal(CartographyRules.Kind, CartographyRules.Trade.Kind);
    }

    [Fact]
    public void Kind_IsCartography() =>
        Assert.Equal(ExpectedKind, CartographyRules.Kind);
}
