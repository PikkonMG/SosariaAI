using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class SmithRulesTests
{
    private const string ExpectedKind = "Smith";
    private const string ExpectedShopToken = "blacksmith";
    private const string ExpectedGoods = "arms";
    private const int FallbackShopX = 1507;
    private const int FallbackShopY = 1579;
    private const int FallbackShopZ = 20;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = SmithRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Blacksmith, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_Station()
    {
        Assert.Equal(CraftStation.AnvilAndForge, SmithRules.Trade.Station);
    }
}
