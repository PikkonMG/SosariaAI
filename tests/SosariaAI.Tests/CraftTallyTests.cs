using SosariaAI.Logging;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CraftTallyTests
{
    [Fact]
    public void Line_NamesEveryTrade_ZeroIncluded()
    {
        var line = CraftTally.Line(new CraftWindow(), CensusText.CensusMinutes);

        Assert.StartsWith($"Crafts in the last {CensusText.CensusMinutes} minutes: ", line);

        foreach (var trade in CraftTally.Trades)
        {
            Assert.Contains($"{trade.ToLowerInvariant()} 0", line);
        }
    }

    [Fact]
    public void Line_CountsPiecesSalesAndStockDeals()
    {
        var window = new CraftWindow { Sessions = 3, PiecesSold = 7, GoldFromSales = 140, StockDeals = 2, StockUnits = 90, StockGold = 250, Masterworks = 1 };
        window.Made[TailorRules.Kind] = 12;
        window.Made[SmithRules.Trade.Kind] = 4;

        var line = CraftTally.Line(window, CensusText.CensusMinutes);

        Assert.Contains("smith 4", line);
        Assert.Contains("tailor 12", line);
        Assert.Contains("carpentry 0", line);
        Assert.Contains("3 sessions begun", line);
        Assert.Contains("sold 7 pieces for 140 gold", line);
        Assert.Contains("2 stock deals with gatherers (90 units for 250 gold)", line);
        Assert.Contains("1 exceptional pieces called out", line);
    }

    [Fact]
    public void Clear_EmptiesTheWindow()
    {
        var window = new CraftWindow { Sessions = 3, StockDeals = 1 };
        window.Made[TailorRules.Kind] = 5;

        window.Clear();

        Assert.Empty(window.Made);
        Assert.Equal(0, window.Sessions);
        Assert.Equal(0, window.StockDeals);
    }
}
