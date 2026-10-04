using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CarpentryRulesTests
{
    private const string ExpectedKind = "Carpentry";
    private const string ExpectedShopToken = "carpenter";
    private const string ExpectedGoods = "furniture";
    private const int FallbackShopX = 1430;
    private const int FallbackShopY = 1597;
    private const int FallbackShopZ = 20;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = CarpentryRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Carpentry, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_Station() =>
        Assert.Equal(CraftStation.None, CarpentryRules.Trade.Station);
}
