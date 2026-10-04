using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TinkerRulesTests
{
    private const string ExpectedKind = "Tinker";
    private const string ExpectedShopToken = "vendor:Tinker";
    private const string ExpectedGoods = "tools";
    private const int FallbackShopX = 1422;
    private const int FallbackShopY = 1654;
    private const int FallbackShopZ = 10;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = TinkerRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Tinkering, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_Station()
    {
        Assert.Equal(CraftStation.None, TinkerRules.Trade.Station);
    }
}
