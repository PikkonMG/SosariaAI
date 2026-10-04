using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class TradeParserTests
{
    private const string Seller = "Ulric";
    private const int NoStanding = 0;
    private const int Standing = 3500;

    [Theory]
    [InlineData("what are you selling")]
    [InlineData("what u got")]
    [InlineData("Ulric what u got?")]
    [InlineData("whatcha got")]
    public void Read_AskStock(string line) =>
        Assert.Equal(TradeIntentKind.AskStock, TradeParser.Read(line, Seller, engaged: false, NoStanding).Kind);

    [Theory]
    [InlineData("how much")]
    [InlineData("ulric how much")]
    [InlineData("how much for the hally?")]
    [InlineData("pc?")]
    public void Read_AskPrice(string line) =>
        Assert.Equal(TradeIntentKind.AskPrice, TradeParser.Read(line, Seller, engaged: false, NoStanding).Kind);

    [Theory]
    [InlineData("3500", 3500)]
    [InlineData("4k", 4000)]
    [InlineData("4k?", 4000)]
    [InlineData("ill go 3k", 3000)]
    [InlineData("i'll give 2.5k", 2500)]
    [InlineData("would you do 3", 3000)]
    [InlineData("ok 3k?", 3000)]
    public void Read_OfferInAHaggle(string line, int price)
    {
        var intent = TradeParser.Read(line, Seller, engaged: true, Standing);

        Assert.Equal(TradeIntentKind.Offer, intent.Kind);
        Assert.Equal(price, intent.Price);
    }

    [Fact]
    public void Read_BareNumberFromAStrangerIsNotAnOffer() =>
        Assert.Equal(TradeIntentKind.None, TradeParser.Read("3500", Seller, engaged: false, NoStanding).Kind);

    [Fact]
    public void Read_OfferNamingTheGoods()
    {
        var intent = TradeParser.Read("3k for the halberd", Seller, engaged: false, NoStanding);

        Assert.Equal(TradeIntentKind.Offer, intent.Kind);
        Assert.Equal(3000, intent.Price);
        Assert.Equal("polearm", intent.Goods?.Row.Key);
    }

    [Theory]
    [InlineData("deal", false)]
    [InlineData("ill take it", false)]
    [InlineData("I'll take it!", false)]
    [InlineData("ill buy", false)]
    [InlineData("i want to buy it", false)]
    [InlineData("can i buy that", false)]
    [InlineData("i want it", false)]
    [InlineData("ok", true)]
    [InlineData("k", true)]
    [InlineData("sure", true)]
    public void Read_Accept(string line, bool engaged)
    {
        var intent = TradeParser.Read(line, Seller, engaged, Standing);

        Assert.Equal(TradeIntentKind.Accept, intent.Kind);
        Assert.Equal(Standing, intent.Price);
    }

    [Fact]
    public void Read_OkFromAStrangerIsNotAYes() =>
        Assert.Equal(TradeIntentKind.None, TradeParser.Read("ok", Seller, engaged: false, NoStanding).Kind);

    [Theory]
    [InlineData("nvm")]
    [InlineData("too much")]
    [InlineData("no deal")]
    [InlineData("no thanks")]
    [InlineData("no")]
    public void Read_Decline(string line) =>
        Assert.Equal(TradeIntentKind.Decline, TradeParser.Read(line, Seller, engaged: true, Standing).Kind);

    [Fact]
    public void Read_WtsShoutWithShorthand()
    {
        var intent = TradeParser.Read("WTS GM halberd 5k", null, engaged: false, NoStanding);

        Assert.Equal(TradeIntentKind.Sell, intent.Kind);
        Assert.Equal(5000, intent.Price);
        Assert.True(intent.Goods?.Exceptional);
        Assert.Equal("polearm", intent.Goods?.Row.Key);
    }

    [Fact]
    public void Read_WtsCountIsNotThePrice()
    {
        var intent = TradeParser.Read("wts 200 mandrake 900", null, engaged: false, NoStanding);

        Assert.Equal(900, intent.Price);
        Assert.Equal(200, intent.Goods?.Amount);
    }

    [Fact]
    public void Read_WtsOfUnknownGoodsIsNothing() =>
        Assert.Equal(TradeIntentKind.None, TradeParser.Read("wts my house", null, engaged: false, NoStanding).Kind);

    [Theory]
    [InlineData("i have one", 0)]
    [InlineData("i have one 5k", 5000)]
    [InlineData("i have one, 5k", 5000)]
    [InlineData("got one", 0)]
    public void Read_HaveOne(string line, int price)
    {
        var intent = TradeParser.Read(line, null, engaged: false, NoStanding);

        Assert.Equal(TradeIntentKind.HaveOne, intent.Kind);
        Assert.Equal(price, intent.Price);
    }

    [Fact]
    public void Read_TradeSmellWithoutARuleIsUnsure()
    {
        var intent = TradeParser.Read("any chance cheaper?", Seller, engaged: true, Standing);

        Assert.False(intent.Sure);
        Assert.False(intent.IsTrade);
    }

    [Fact]
    public void Read_SmallTalkIsNothing()
    {
        var intent = TradeParser.Read("nice weather today", Seller, engaged: true, Standing);

        Assert.True(intent.Sure);
        Assert.False(intent.IsTrade);
    }

    [Fact]
    public void Words_DropsTheListenersNameAndPunctuation() =>
        Assert.Equal(["how", "much", "1,200"], TradeParser.Words("Ulric, how much? 1,200!", Seller));
}
