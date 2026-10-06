using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class HaggleTests
{
    private const int Asking = 4000;
    private const int Value = 4000;
    private const int RichPurse = 100000;
    private const int NeverWalks = 99;
    private const int AlwaysWalks = 0;

    [Fact]
    public void TemperOf_GreedIsHardGenerosityIsSoft()
    {
        Assert.Equal(HaggleTemper.Hard, Haggle.TemperOf(greedy: true, generous: false));
        Assert.Equal(HaggleTemper.Soft, Haggle.TemperOf(greedy: false, generous: true));
        Assert.Equal(HaggleTemper.Fair, Haggle.TemperOf(greedy: true, generous: true));
        Assert.Equal(HaggleTemper.Fair, Haggle.TemperOf(greedy: false, generous: false));
    }

    [Fact]
    public void Seller_TakesTheAskingPrice()
    {
        var haggle = Haggle.Selling(Asking, HaggleTemper.Fair);
        var step = haggle.Hear(Asking, NeverWalks);

        Assert.Equal(HaggleMove.Accept, step.Move);
        Assert.Equal(Asking, haggle.Agreed);
    }

    [Fact]
    public void Seller_IsInsultedFarBelowItsFloor()
    {
        var haggle = Haggle.Selling(Asking, HaggleTemper.Fair);
        var step = haggle.Hear(haggle.Limit / Haggle.InsultDivisor - 1, NeverWalks);

        Assert.Equal(HaggleMove.Insulted, step.Move);
        Assert.True(haggle.Ended);
    }

    [Fact]
    public void Seller_CountersComeDownButNeverUnderTheFloor()
    {
        var haggle = Haggle.Selling(Asking, HaggleTemper.Hard);
        var low = haggle.Limit - 1;
        var last = haggle.Standing;

        while (haggle.IsOpen)
        {
            var step = haggle.Hear(low, NeverWalks);

            Assert.NotEqual(HaggleMove.Accept, step.Move);
            Assert.True(haggle.Standing <= last);
            Assert.True(haggle.Standing >= haggle.Limit);
            last = haggle.Standing;
        }

        Assert.True(haggle.Round <= Haggle.MaxRounds);
    }

    [Fact]
    public void Seller_TakesANumberPastItsFloorByTheClosingRound()
    {
        var haggle = Haggle.Selling(Asking, HaggleTemper.Fair);
        var offer = haggle.Limit;

        haggle.Hear(offer, NeverWalks);
        var step = haggle.Hear(offer, NeverWalks);

        Assert.Equal(HaggleMove.Accept, step.Move);
        Assert.Equal(offer, haggle.Agreed);
    }

    [Fact]
    public void Seller_CanWalkAwayWhenTooFarApart()
    {
        var haggle = Haggle.Selling(Asking, HaggleTemper.Hard);
        var step = haggle.Hear(haggle.Limit - 1, AlwaysWalks);

        Assert.Equal(HaggleMove.WalkAway, step.Move);
    }

    [Fact]
    public void Agree_LocksTheStandingNumber()
    {
        var haggle = Haggle.Selling(Asking, HaggleTemper.Fair);
        haggle.Hear(haggle.Limit - 1, NeverWalks);
        var standing = haggle.Standing;
        var step = haggle.Agree();

        Assert.Equal(HaggleMove.Accept, step.Move);
        Assert.Equal(standing, haggle.Agreed);
        Assert.False(haggle.IsOpen);
    }

    [Fact]
    public void Buyer_CeilingIsCappedByThePurse()
    {
        const int purse = 1000;
        var haggle = Haggle.Buying(Value, purse, HaggleTemper.Soft);

        Assert.True(haggle.Limit <= purse);
        Assert.True(haggle.Standing <= haggle.Limit);
    }

    [Fact]
    public void Buyer_NeverGoesPastItsCeiling()
    {
        var haggle = Haggle.Buying(Value, RichPurse, HaggleTemper.Fair);
        var high = haggle.Limit + 1;

        while (haggle.IsOpen)
        {
            var step = haggle.Hear(high, NeverWalks);

            Assert.NotEqual(HaggleMove.Accept, step.Move);
            Assert.True(haggle.Standing <= haggle.Limit);
        }
    }

    [Fact]
    public void Buyer_LaughsAtASillyAsk()
    {
        var haggle = Haggle.Buying(Value, RichPurse, HaggleTemper.Fair);
        var step = haggle.Hear(haggle.Limit * Haggle.SillyMultiple + 1, NeverWalks);

        Assert.Equal(HaggleMove.Insulted, step.Move);
    }

    [Fact]
    public void Buyer_TakesAnAskAtOrUnderItsOffer()
    {
        var haggle = Haggle.Buying(Value, RichPurse, HaggleTemper.Fair);
        var step = haggle.Hear(haggle.Standing, NeverWalks);

        Assert.Equal(HaggleMove.Accept, step.Move);
    }

    [Theory]
    [InlineData(HaggleTemper.Hard, HaggleTemper.Hard)]
    [InlineData(HaggleTemper.Fair, HaggleTemper.Soft)]
    [InlineData(HaggleTemper.Soft, HaggleTemper.Fair)]
    public void TwoSides_MeetBetweenFloorAndCeiling(HaggleTemper sellerTemper, HaggleTemper buyerTemper)
    {
        var seller = Haggle.Selling(Asking, sellerTemper);
        var buyer = Haggle.Buying(Value, RichPurse, buyerTemper);
        var sellerTurn = true;

        while (seller.IsOpen && buyer.IsOpen)
        {
            var step = sellerTurn ? seller.Hear(buyer.Standing, NeverWalks) : buyer.Hear(seller.Standing, NeverWalks);
            sellerTurn = !sellerTurn;

            if (step.Move == HaggleMove.Accept)
            {
                Assert.InRange(step.Price, seller.Limit, buyer.Limit);
                return;
            }
        }

        Assert.True(seller.Limit > buyer.Limit, "only sides whose ranges never overlap fail to meet");
    }

    [Fact]
    public void Fixed_IsAgreedFromTheStart()
    {
        const int deposit = 1650;
        var haggle = Haggle.Fixed(deposit);

        Assert.Equal(deposit, haggle.Agreed);
        Assert.False(haggle.IsOpen);
        Assert.Equal(HaggleSide.Sells, haggle.Side);
    }
}
