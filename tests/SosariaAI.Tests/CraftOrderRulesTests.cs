using System;
using System.Collections.Generic;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class CraftOrderRulesTests
{
    private const int MarketValue = 3000;
    private const int QuotedPrice = 3300;
    private const int HalfOfQuote = 1650;
    private const double EvenChance = 0.5;
    private const double LowChance = 0.3;
    private const double GoodExceptional = 0.4;
    private const double PoorExceptional = 0.02;
    private const double GmPlateChestExceptional = 0.0499;
    private const int Paid = 1650;
    private const int PurseBefore = 5000;
    private const uint Crafter = 0x1234;
    private const uint Buyer = 7;
    private const uint FirstPiece = 0x40000001;
    private const uint SecondPiece = 0x40000002;
    private static readonly DateTime Placed = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Quote_AddsTheMadeToOrderShare_AndTheDepositIsHalf()
    {
        Assert.Equal(QuotedPrice, CraftOrderRules.Quote(MarketValue));
        Assert.Equal(HalfOfQuote, CraftOrderRules.DepositOf(QuotedPrice));
    }

    [Theory]
    [InlineData(false, EvenChance, GoodExceptional, 0, OrderRefusal.CannotMake)]
    [InlineData(true, LowChance, GoodExceptional, 0, OrderRefusal.TooHard)]
    [InlineData(true, EvenChance, PoorExceptional, 0, OrderRefusal.TooHard)]
    [InlineData(true, EvenChance, GoodExceptional, CraftOrderRules.MaxOpenOrders, OrderRefusal.TooBusy)]
    [InlineData(true, EvenChance, GoodExceptional, 0, OrderRefusal.None)]
    public void Refusal_TheCrafterSaysWhy(bool makes, double success, double exceptional, int open, OrderRefusal expected) =>
        Assert.Equal(expected, CraftOrderRules.Refusal(makes, success, exceptional, open));

    [Fact]
    public void Expired_AfterThePickupWindow()
    {
        Assert.False(CraftOrderRules.Expired(Placed, Placed + CraftOrderRules.PickupWindow - TimeSpan.FromMinutes(1)));
        Assert.True(CraftOrderRules.Expired(Placed, Placed + CraftOrderRules.PickupWindow));
    }

    [Fact]
    public void Order_ReadyWhenEveryPieceIsMade()
    {
        var order = new CraftOrder(
            CraftOrderRules.NewId(Crafter, Placed, 0), Buyer, "Ann", ["PlateChest", "PlateLegs"], QuotedPrice, HalfOfQuote, Placed, []
        );

        Assert.False(order.Ready);
        Assert.Equal("PlateChest", order.NextType);
        var half = order.WithPiece(FirstPiece);
        Assert.Equal("PlateLegs", half.NextType);
        Assert.True(half.WithPiece(SecondPiece).Ready);
        Assert.Equal(QuotedPrice - HalfOfQuote, order.Rest);
    }

    [Fact]
    public void Refusal_AGmSmithTakesAPlateChestOrder() =>
        Assert.Equal(OrderRefusal.None, CraftOrderRules.Refusal(true, EvenChance, GmPlateChestExceptional, 0));

    [Fact]
    public void CoinCrossed_OnlyWhenThePayerIsShortByThePrice()
    {
        Assert.True(CraftOrderRules.CoinCrossed(PurseBefore, PurseBefore - Paid, Paid));
        Assert.False(CraftOrderRules.CoinCrossed(PurseBefore, PurseBefore, Paid));
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, false, false)]
    public void PickupDue_OncePerArrival(bool inRange, bool offeredBefore, bool expectedDue, bool rememberedAfter)
    {
        var offered = new HashSet<uint>();

        if (offeredBefore)
        {
            offered.Add(Buyer);
        }

        Assert.Equal(expectedDue, CraftOrderRules.PickupDue(offered, Buyer, inRange));
        Assert.Equal(rememberedAfter, offered.Contains(Buyer));
    }

    [Theory]
    [InlineData(false, false, TradeLineKind.OrderNotReady)]
    [InlineData(false, true, TradeLineKind.OrderNotReady)]
    [InlineData(true, true, TradeLineKind.OrderBusy)]
    public void StatusLine_NeverSaysNotDoneForReadyWork(bool ready, bool busy, TradeLineKind expected) =>
        Assert.Equal(expected, CraftOrderRules.StatusLine(ready, busy));

    [Fact]
    public void StatusLine_ReadyAndFree_HandsOverInstead() => Assert.Null(CraftOrderRules.StatusLine(true, false));
}
