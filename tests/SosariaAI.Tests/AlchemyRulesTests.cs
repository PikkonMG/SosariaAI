using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class AlchemyRulesTests
{
    private const string ExpectedKind = "Alchemy";
    private const string ExpectedShopToken = "vendor:Alchemist";
    private const string ExpectedGoods = "potions";
    private const int FallbackShopX = 1498;
    private const int FallbackShopY = 1659;
    private const int FallbackShopZ = 27;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = AlchemyRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Alchemy, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_Station() =>
        Assert.Equal(CraftStation.None, AlchemyRules.Trade.Station);
}
