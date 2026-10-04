using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class FletchRulesTests
{
    private const string ExpectedKind = "Fletch";
    private const string ExpectedShopToken = "vendor:Bowyer";
    private const string ExpectedGoods = "bows and arrows";
    private const int FallbackShopX = 1470;
    private const int FallbackShopY = 1578;
    private const int FallbackShopZ = 20;

    [Fact]
    public void Trade_WorksTheRightSkillAtTheRightShop()
    {
        var trade = FletchRules.Trade;

        Assert.Equal(ExpectedKind, trade.Kind);
        Assert.Equal(SkillName.Fletching, trade.Skill);
        Assert.Equal(ExpectedShopToken, trade.ShopToken);
        Assert.Equal(new Point3D(FallbackShopX, FallbackShopY, FallbackShopZ), trade.FallbackShop);
        Assert.Equal(ExpectedGoods, trade.GoodsNoun);
    }

    [Fact]
    public void Trade_StationAndMaterialShop()
    {
        // The bowyer sells only the tools; boards come from the carpenter.
        Assert.Equal(CraftStation.None, FletchRules.Trade.Station);
        Assert.Equal(ShopFinder.BowyerToken, FletchRules.Trade.ToolShopToken);
        Assert.Equal([ShopFinder.BowyerToken, ShopFinder.CarpenterToken, ShopFinder.TinkerToken], FletchRules.Trade.SupplyShopTokens);
        Assert.Contains(ShopFinder.CarpenterToken, FletchRules.Trade.StationShopTokens);
    }
}
