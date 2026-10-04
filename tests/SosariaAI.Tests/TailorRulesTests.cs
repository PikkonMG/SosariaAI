using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TailorRulesTests
{
    private const string ExpectedKind = "Tailor";
    private const string ExpectedShopToken = "vendor:Tailor";
    private const string ExpectedGoods = "clothes";
    private const int FallbackShopX = 1547;
    private const int FallbackShopY = 1659;
    private const int FallbackShopZ = 26;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = TailorRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Tailoring, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_Station()
    {
        Assert.Equal(CraftStation.None, TailorRules.Trade.Station);
    }
}
