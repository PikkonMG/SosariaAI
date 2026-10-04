using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class CraftTradeTests
{
    private const string WoodKey = "wood";

    [Fact]
    public void SupplyShopTokens_ToolShopThenMaterialShopThenWorkShops()
    {
        Assert.Equal([ShopFinder.TinkerToken, ShopFinder.SmithToken, ShopFinder.ArmorerToken], SmithRules.Trade.SupplyShopTokens);
        Assert.Equal(
            [ShopFinder.TavernToken, ShopFinder.ButcherToken, ShopFinder.BakerToken, ShopFinder.SmithToken],
            CookRules.Trade.SupplyShopTokens
        );
    }

    [Fact]
    public void BurnsStock_OnlyTheTradesOwnStock()
    {
        Assert.True(SmithRules.Trade.BurnsStock(typeof(IronIngot)));
        Assert.False(SmithRules.Trade.BurnsStock(typeof(Log)));
        Assert.True(CarpentryRules.Trade.BurnsStock(typeof(Log)));
        Assert.True(TailorRules.Trade.BurnsStock(typeof(Cloth)));
        Assert.False(TailorRules.Trade.BurnsStock(null));
        Assert.True(CookRules.Trade.BurnsStock(typeof(RawRibs)));
        Assert.True(CartographyRules.Trade.BurnsStock(typeof(BlankMap)));
    }

    [Fact]
    public void PeopleBringStock_OnlyForStockSomeoneGathers()
    {
        // Mapmakers out of blank maps waited three minutes 142 times in one evening for a
        // seller nobody could be: only a shop sells blank maps, scrolls and bottles.
        Assert.False(CartographyRules.Trade.PeopleBringStock);
        Assert.False(InscriptionRules.Trade.PeopleBringStock);
        Assert.False(AlchemyRules.Trade.PeopleBringStock);

        CraftTrade[] gathered = [SmithRules.Trade, TailorRules.Trade, CarpentryRules.Trade, FletchRules.Trade, TinkerRules.Trade, CookRules.Trade];

        foreach (var trade in gathered)
        {
            Assert.True(trade.PeopleBringStock, trade.Kind);
        }
    }

    [Fact]
    public void WoodTrades_AskForTheLogsTheEngineBurns()
    {
        // The T2A carpentry and bowcraft lists take logs; boards only stand in for them.
        Assert.Equal(CarpentryRules.StockNoun, FletchRules.StockNoun);
        Assert.True(CarpentryRules.Trade.BurnsStock(typeof(Board)));
        Assert.True(FletchRules.Trade.BurnsStock(typeof(Log)));
        Assert.Contains(CarpentryRules.StockNoun, Appraisal.RowByKey(WoodKey).Words);
    }

    [Fact]
    public void EveryStationTrade_NamesItsStockAndToolAloud()
    {
        CraftTrade[] trades =
        [
            SmithRules.Trade, TailorRules.Trade, CarpentryRules.Trade, FletchRules.Trade, AlchemyRules.Trade,
            InscriptionRules.Trade, TinkerRules.Trade, CookRules.Trade, CartographyRules.Trade
        ];

        foreach (var trade in trades)
        {
            Assert.False(string.IsNullOrWhiteSpace(trade.StockNoun), trade.Kind);
            Assert.False(string.IsNullOrWhiteSpace(trade.ToolNoun), trade.Kind);
            Assert.NotEmpty(trade.StockTypes);
        }
    }

    [Fact]
    public void CraftNeed_EveryLineNamesWhatIsMissing()
    {
        foreach (var topic in TalkDefaults.All)
        {
            if (topic.Name != TalkCategory.CraftNeed)
            {
                continue;
            }

            Assert.All(topic.Lines, line => Assert.Contains("{item}", line));
        }

        Assert.Contains(TinkerRules.Trade.ToolNoun, Talk.Line(TalkCategory.CraftNeed, 0, new TalkSlots { Item = TinkerRules.Trade.ToolNoun }));
    }

    [Fact]
    public void OwnToolType_OnlyTheTinkerMakesItsOwnTool()
    {
        Assert.Equal(typeof(TinkerTools), TinkerRules.Trade.OwnToolType);
        Assert.Null(SmithRules.Trade.OwnToolType);
        Assert.Null(TailorRules.Trade.OwnToolType);
    }

    [Fact]
    public void EveryCareer_HasAStockAndAShopToWorkAt()
    {
        foreach (var career in CraftCareerRules.Careers)
        {
            var trade = CraftCareerRules.TradeByKind(career.Kind);

            Assert.NotNull(trade);
            Assert.Equal(career.Kind, trade.Kind);
            Assert.NotEmpty(trade.StockTypes);
            Assert.NotEmpty(trade.StationShopTokens);
            Assert.NotEmpty(trade.SupplyShopTokens);
        }
    }
}
