using SosariaAI.Economy;
using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class TradeTallyTests
{
    [Fact]
    public void Line_NamesEveryCount_ZeroIncluded()
    {
        var line = TradeTally.Line(new TradeWindowCount(), CensusText.CensusMinutes);

        Assert.Equal(
            $"Trade in the last {CensusText.CensusMinutes} minutes: 0 sales to shops (0 stacks for 0 gold); " +
            "0 buys from shops for 0 gold; 0 deals between people for 0 gold (0 in a trade window, " +
            "0 answered on the bank floor); 0 player vendor sales for 0 gold; 0 goods held up at banks",
            line
        );
    }

    [Fact]
    public void Line_CountsShopsDealsVendorsAndShouts()
    {
        var window = new TradeWindowCount
        {
            ShopSales = 20, ShopStacks = 37, ShopGold = 6500, ShopBuys = 9, ShopBuyGold = 800, Deals = 8,
            DealGold = 1200, WindowDeals = 7, FloorAnswers = 5, VendorSales = 2, VendorGold = 900, HeldUp = 41
        };

        var line = TradeTally.Line(window, CensusText.CensusMinutes);

        Assert.Contains("20 sales to shops (37 stacks for 6500 gold)", line);
        Assert.Contains("9 buys from shops for 800 gold", line);
        Assert.Contains("8 deals between people for 1200 gold (7 in a trade window, 5 answered on the bank floor)", line);
        Assert.Contains("2 player vendor sales for 900 gold", line);
        Assert.Contains("41 goods held up at banks", line);
    }

    [Fact]
    public void Clear_EmptiesTheWindow()
    {
        var window = new TradeWindowCount { ShopSales = 3, Deals = 2, WindowDeals = 2, VendorGold = 50, HeldUp = 4 };

        window.Clear();

        Assert.Equal(
            TradeTally.Line(new TradeWindowCount(), CensusText.CensusMinutes),
            TradeTally.Line(window, CensusText.CensusMinutes)
        );
    }
}
