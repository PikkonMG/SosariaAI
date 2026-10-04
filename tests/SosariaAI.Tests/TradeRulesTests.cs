using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class TradeRulesTests
{
    [Fact]
    public void MayBeCounterpart_RejectsPlayers()
    {
        Assert.False(TradeRules.MayBeCounterpart(isPlayer: true));
        Assert.True(TradeRules.MayBeCounterpart(isPlayer: false));
    }
}
