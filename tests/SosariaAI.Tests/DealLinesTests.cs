using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class DealLinesTests
{
    [Theory]
    [InlineData("bandages 5gp each, cures 15. no haggling heh")]
    [InlineData("50 gold gets u 10 bandages, that's the price")]
    [InlineData("good, 10 bandages then. hand over the 50 gold and theyre urs heh")]
    [InlineData("deal, drop the gold on me")]
    [InlineData("sold!")]
    public void SettlesSale_PricesSalesAndPayments(string line) => Assert.True(DealLines.SettlesSale(line));

    [Theory]
    [InlineData("nah im good, just visiting a friend. u need bandages? fresh stock, fair price heh")]
    [InlineData("lost 500 gold to a pk at the crossroads")]
    [InlineData("the real deal is the lich in despise")]
    [InlineData("")]
    public void SettlesSale_LeavesOtherTalkAlone(string line) => Assert.False(DealLines.SettlesSale(line));

    [Fact]
    public void ASaleLine_IsSaidOnlyInsideADeal()
    {
        const string line = "hand over the 50 gold and theyre urs";

        Assert.True(SpeechFacts.Contradicts(line, new SpeechFacts.Snapshot { IsAlive = true, InDeal = false }));
        Assert.False(SpeechFacts.Contradicts(line, new SpeechFacts.Snapshot { IsAlive = true, InDeal = true }));
        Assert.False(SpeechFacts.ClaimsFree(line));
    }
}
