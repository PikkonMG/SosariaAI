using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class InscriptionRulesTests
{
    private const string ExpectedKind = "Inscription";
    private const string ExpectedShopToken = "vendor:Mage";
    private const string ExpectedGoods = "scrolls";
    private const int FallbackShopX = 1485;
    private const int FallbackShopY = 1550;
    private const int FallbackShopZ = 30;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = InscriptionRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Inscribe, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_Station() =>
        Assert.Equal(CraftStation.None, InscriptionRules.Trade.Station);
}
