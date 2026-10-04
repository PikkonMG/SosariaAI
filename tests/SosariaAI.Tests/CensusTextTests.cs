using System.Collections.Generic;
using System.Text;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class CensusTextTests
{
    private const int Low = 7;
    private const int Live = 20;

    [Theory]
    [InlineData(0, false)]
    [InlineData(CensusText.CensusMinutes, true)]
    [InlineData(CensusText.CensusMinutes + 1, false)]
    public void Due_EveryTenMinutes(int minute, bool due) => Assert.Equal(due, CensusText.Due(minute));

    [Fact]
    public void Counted_BiggestFirst_ThenByName()
    {
        var counts = new Dictionary<string, int> { ["b"] = 2, ["c"] = 5, ["a"] = 2 };

        Assert.Equal("x: c 5, a 2, b 2", CensusText.Counted(new StringBuilder("x: "), counts).ToString());
        Assert.Equal("x: none", CensusText.Counted(new StringBuilder("x: "), null).ToString());
    }

    [Theory]
    [InlineData(false, false, "bandages no gold")]
    [InlineData(false, true, "bandages no gold")]
    [InlineData(true, false, "bandages no shop in reach")]
    [InlineData(true, true, "bandages can refill")]
    public void SupplyReason_NoGoldFirst_ThenTheShop(bool hasGold, bool hasErrand, string reason) =>
        Assert.Equal(reason, SupplyCensus.Reason(SupplyKind.Bandages, hasGold, hasErrand));

    [Fact]
    public void SupplyLine_CountsTheLowOfTheLive()
    {
        var reasons = new Dictionary<string, int>
        {
            [SupplyCensus.Reason(SupplyKind.Reagents, hasGold: true, hasErrand: true)] = 2,
            [SupplyCensus.Reason(SupplyKind.Bandages, hasGold: false, hasErrand: false)] = 5
        };

        Assert.Equal(
            "Supplies low: bandages no gold 5, reagents can refill 2 (7 low of 20 live)",
            SupplyCensus.Line(reasons, Low, Live)
        );
    }
}
