using System.Collections.Generic;
using SosariaAI.Deliberation;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>Trade lines, the model rewording guard, and the typed Jev layer's payload and reading.</summary>
public class TradeWordsTests
{
    private const int Standing = 3500;

    [Fact]
    public void Lines_FillThePricesTheWayPlayersSayThem()
    {
        var line = TradeLines.For(TradeLineKind.SellerCounter, 0, "GM halberd", 4400, 3500);

        Assert.Equal("cant do 3.5k. 4.4k", line);
    }

    [Fact]
    public void SameNumbers_ARewordingMayNotChangeThePrice()
    {
        Assert.True(TradeLines.SameNumbers("cant do 3500. 4400", "no way at 3500, 4400 is my number"));
        Assert.False(TradeLines.SameNumbers("cant do 3500. 4400", "no way at 3500, 4k is my number"));
        Assert.False(TradeLines.SameNumbers("4k is firm", null));
    }

    [Fact]
    public void Chosen_FallsBackToThePlainLine()
    {
        Assert.Equal("4k is firm", TradeVoice.Chosen("4k is firm", "3k is firm"));
        Assert.Equal("4k, not a coin less", TradeVoice.Chosen("4k is firm", "4k, not a coin less"));
        Assert.Equal("4k is firm", TradeVoice.Chosen("4k is firm", null));
    }

    [Fact]
    public void Chosen_KeepsThePlainLineWhenTheRewordAddsAPromise()
    {
        Assert.Equal("4k is firm", TradeVoice.Chosen("4k is firm", "4k is firm, meet me at the bank"));
        Assert.Equal("4k is firm", TradeVoice.Chosen("4k is firm", "4k is firm, heading to brit after"));
        Assert.Equal("meet me at 4000", TradeVoice.Chosen("meet me at 4000", "meet me at 4000"));
    }

    [Fact]
    public void MayPhrase_OnlyNearAPersonOffCooldownWithAPhraser()
    {
        Assert.True(TradeVoice.MayPhrase(listenerIsHuman: true, inRange: true, cooling: false, phraserReady: true));
        Assert.False(TradeVoice.MayPhrase(listenerIsHuman: false, inRange: true, cooling: false, phraserReady: true));
        Assert.False(TradeVoice.MayPhrase(listenerIsHuman: true, inRange: false, cooling: false, phraserReady: true));
        Assert.False(TradeVoice.MayPhrase(listenerIsHuman: true, inRange: true, cooling: true, phraserReady: true));
        Assert.False(TradeVoice.MayPhrase(listenerIsHuman: true, inRange: true, cooling: false, phraserReady: false));
    }

    [Fact]
    public void PriceOptions_ASmallBareNumberMayMeanThousands() =>
        Assert.Equal([3, 3000], TradeIntentJev.PriceOptions("would you do 3 for it"));

    [Fact]
    public void Build_AsksThePriceOnlyWhenANumberWasHeard()
    {
        var withNumber = TradeIntentJev.Build("would you do 3", "GM halberd", Standing, HaggleSide.Sells, [3, 3000]);
        var without = TradeIntentJev.Build("any chance cheaper", "GM halberd", Standing, HaggleSide.Sells, []);

        Assert.True(withNumber.Questions.ContainsKey(TradeIntentJev.PriceQuestion));
        Assert.True(withNumber.Questions.ContainsKey(TradeIntentJev.IntentQuestion));
        Assert.False(without.Questions.ContainsKey(TradeIntentJev.PriceQuestion));
    }

    [Fact]
    public void Read_AConfidentOfferWithItsPrice()
    {
        var intent = TradeIntentJev.Read(Answers("offer", 0.9, "3000", 0.8), Standing);

        Assert.Equal(TradeIntentKind.Offer, intent.Kind);
        Assert.Equal(3000, intent.Price);
    }

    [Fact]
    public void Read_AShakyAnswerIsNoAnswer() =>
        Assert.False(TradeIntentJev.Read(Answers("offer", 0.3, "3000", 0.9), Standing).IsTrade);

    [Fact]
    public void Read_AnOfferWithoutAPriceIsNothing() =>
        Assert.False(TradeIntentJev.Read(Answers("offer", 0.9, TradeIntentJev.NoPrice, 0.9), Standing).IsTrade);

    [Fact]
    public void Read_AcceptTakesTheStandingNumber()
    {
        var intent = TradeIntentJev.Read(Answers("accept", 0.9, TradeIntentJev.NoPrice, 0.9), Standing);

        Assert.Equal(TradeIntentKind.Accept, intent.Kind);
        Assert.Equal(Standing, intent.Price);
    }

    [Fact]
    public void Read_NoAnswersIsNothing() => Assert.False(TradeIntentJev.Read(null, Standing).IsTrade);

    private static Dictionary<string, SystemOneAnswer> Answers(string intent, double confidence, string price, double priceConfidence) =>
        new()
        {
            [TradeIntentJev.IntentQuestion] = new SystemOneAnswer(intent, confidence, null),
            [TradeIntentJev.PriceQuestion] = new SystemOneAnswer(price, priceConfidence, null)
        };
}
