using System;
using System.Collections.Generic;
using System.Globalization;

namespace SosariaAI.Economy;

/// <summary>Why a crafter turns an order down.</summary>
public enum OrderRefusal
{
    None,
    CannotMake,
    TooHard,
    TooBusy
}

/// <summary>
/// How a crafter takes an order. It takes only work its trade makes, at even odds or better,
/// with a fair chance of an exceptional piece, while it has fewer than
/// <see cref="MaxOpenOrders"/> open. The price is the market table's middle plus a share for
/// made to order; half is paid up front and is not paid back. An order not picked up within
/// <see cref="PickupWindow"/> goes to the crafter's shop stock. Pure.
/// </summary>
public static class CraftOrderRules
{
    public const int MaxOpenOrders = 3;
    public const int OrderPremiumPercent = 10;
    public const int DepositPercent = 50;
    public const int PercentScale = 100;

    /// <summary>Below even odds a crafter burns more stock than it makes.</summary>
    public const double MinSuccessChance = 0.5;

    /// <summary>
    /// The least chance per try of an exceptional piece: below it a crafter would burn a cart of
    /// ingots for one order. A grandmaster smith makes an exceptional plate chest about one try
    /// in twenty, the hardest piece of a suit.
    /// </summary>
    public const double MinExceptionalChance = 0.04;

    public static readonly TimeSpan PickupWindow = TimeSpan.FromDays(3);

    /// <summary>How long a quote waits for a yes.</summary>
    public static readonly TimeSpan QuoteHold = TimeSpan.FromMinutes(2);

    private const string IdFormat = "{0:x}-{1:x}-{2}";

    public static int Quote(int marketValue) =>
        GoldWords.RoundSpoken(Math.Max(0, marketValue) * (PercentScale + OrderPremiumPercent) / PercentScale);

    public static int DepositOf(int price) => GoldWords.RoundSpoken(Math.Max(0, price) * DepositPercent / PercentScale);

    public static OrderRefusal Refusal(bool makes, double successChance, double exceptionalChance, int openOrders) =>
        !makes ? OrderRefusal.CannotMake
        : successChance < MinSuccessChance || exceptionalChance < MinExceptionalChance ? OrderRefusal.TooHard
        : openOrders >= MaxOpenOrders ? OrderRefusal.TooBusy
        : OrderRefusal.None;

    public static bool Expired(DateTime placedAt, DateTime now) => now - placedAt >= PickupWindow;

    /// <summary>
    /// True when the payer's coin went over in a trade window: its purse is short by the price.
    /// The payee's pack is no proof: a full pack drops the coin at its feet.
    /// </summary>
    public static bool CoinCrossed(int payerBefore, int payerNow, int price) => payerBefore - payerNow >= price;

    /// <summary>
    /// True when a ready order is offered to a buyer who came by: once per arrival. A buyer in
    /// range is remembered in <paramref name="offered"/> and forgotten when it walks off.
    /// </summary>
    public static bool PickupDue(ISet<uint> offered, uint buyer, bool inRange)
    {
        if (!inRange)
        {
            offered.Remove(buyer);
            return false;
        }

        return offered.Add(buyer);
    }

    /// <summary>
    /// What a crafter says when asked after an order it does not hand over now: busy with a
    /// customer when the work is ready, else not done yet. Null when the work is ready and the
    /// crafter is free: it hands it over instead.
    /// </summary>
    public static TradeLineKind? StatusLine(bool ready, bool busy) =>
        !ready ? TradeLineKind.OrderNotReady
        : busy ? TradeLineKind.OrderBusy
        : null;

    /// <summary>An order id unique for this crafter: its serial, the time placed, and its count of orders.</summary>
    public static string NewId(uint crafterSerial, DateTime now, int count) =>
        string.Format(CultureInfo.InvariantCulture, IdFormat, crafterSerial, now.Ticks, count);
}
