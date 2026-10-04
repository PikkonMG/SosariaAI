using SosariaAI.Skills;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class TreasureLinesTests
{
    private const int NegativeRoll = -7;
    private const int Asking = 500;
    private const string ItemToken = "{item}";
    private const string PriceToken = "{price}";
    private const string MapNoun = "lvl 3 tmap";

    [Fact]
    public void SaleLines_NameTheGoodsAndPrice()
    {
        foreach (var topic in TalkDefaults.All)
        {
            if (topic.Name is not (TalkCategory.SosHawk or TalkCategory.TreasureCannotRead))
            {
                continue;
            }

            foreach (var line in topic.Lines)
            {
                Assert.Contains(ItemToken, line);
                Assert.Contains(PriceToken, line);
            }
        }
    }

    [Fact]
    public void Sale_SaysThePriceThe1999Way()
    {
        Assert.Equal("500", TreasureLines.Sale(null, Asking).Price);
        Assert.Null(TreasureLines.Sale(null, Asking).Item);
    }

    [Fact]
    public void SaleLine_FillsEveryToken()
    {
        var slots = TreasureLines.Sale(null, Asking) with { Item = MapNoun };
        var line = Talk.Line(TalkCategory.TreasureCannotRead, NegativeRoll, slots);

        Assert.Contains(MapNoun, line);
        Assert.DoesNotContain(ItemToken, line);
        Assert.DoesNotContain(PriceToken, line);
    }

    [Fact]
    public void SaleLine_WithoutGoods_SaysNothing() =>
        Assert.Null(Talk.Line(TalkCategory.SosHawk, NegativeRoll, TreasureLines.Sale(null, Asking)));
}
