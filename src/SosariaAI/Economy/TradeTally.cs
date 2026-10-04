using System;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;

namespace SosariaAI.Economy;

/// <summary>What changed hands on the shard in one summary window.</summary>
public sealed class TradeWindowCount
{
    public int ShopSales { get; set; }

    public int ShopStacks { get; set; }

    public int ShopGold { get; set; }

    public int ShopBuys { get; set; }

    public int ShopBuyGold { get; set; }

    public int Deals { get; set; }

    public int DealGold { get; set; }

    public int WindowDeals { get; set; }

    public int FloorAnswers { get; set; }

    public int VendorSales { get; set; }

    public int VendorGold { get; set; }

    public int HeldUp { get; set; }

    public void Clear()
    {
        ShopSales = 0;
        ShopStacks = 0;
        ShopGold = 0;
        ShopBuys = 0;
        ShopBuyGold = 0;
        Deals = 0;
        DealGold = 0;
        WindowDeals = 0;
        FloorAnswers = 0;
        VendorSales = 0;
        VendorGold = 0;
        HeldUp = 0;
    }
}

/// <summary>
/// Counts the trade the operator can see: sales over a shop counter and buys from it, deals
/// between two people (a character and a character or a player) with those closed in a secure
/// trade window and those a bystander answered on the bank floor, sales off player vendors, and
/// goods held up at a bank. Every <see cref="CensusText.CensusMinutes"/> minutes it writes one activity
/// line: "Trade in the last 10 minutes: ...". Every count is named, a zero included, so a dead
/// market shows. World thread only.
/// </summary>
public static class TradeTally
{
    private static readonly ILogger logger = SosariaLog.For(typeof(TradeTally));
    private static readonly TradeWindowCount Window = new();
    private static int _minutes;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    /// <summary>A shop counter bought <paramref name="stacks"/> stacks for <paramref name="gold"/>.</summary>
    public static void NoteShopSale(int stacks, int gold)
    {
        Window.ShopSales++;
        Window.ShopStacks += Math.Max(0, stacks);
        Window.ShopGold += Math.Max(0, gold);
    }

    public static void NoteShopBuy(int gold)
    {
        Window.ShopBuys++;
        Window.ShopBuyGold += Math.Max(0, gold);
    }

    /// <summary>Goods went from one person to another for <paramref name="gold"/>.</summary>
    public static void NoteDeal(int gold, bool throughWindow)
    {
        Window.Deals++;
        Window.DealGold += Math.Max(0, gold);

        if (throughWindow)
        {
            Window.WindowDeals++;
        }
    }

    /// <summary>A bystander answered a WTS or a WTB on the bank floor and the deal closed.</summary>
    public static void NoteFloorAnswer() => Window.FloorAnswers++;

    public static void NoteVendorSale(int gold)
    {
        Window.VendorSales++;
        Window.VendorGold += Math.Max(0, gold);
    }

    public static void NoteHeldUp() => Window.HeldUp++;

    /// <summary>The summary line for one window's counts.</summary>
    public static string Line(TradeWindowCount window, int minutes)
    {
        window ??= new TradeWindowCount();

        return $"Trade in the last {minutes} minutes: " +
               $"{window.ShopSales} sales to shops ({window.ShopStacks} stacks for {window.ShopGold} gold); " +
               $"{window.ShopBuys} buys from shops for {window.ShopBuyGold} gold; " +
               $"{window.Deals} deals between people for {window.DealGold} gold " +
               $"({window.WindowDeals} in a trade window, {window.FloorAnswers} answered on the bank floor); " +
               $"{window.VendorSales} player vendor sales for {window.VendorGold} gold; " +
               $"{window.HeldUp} goods held up at banks";
    }

    private static void OnMinute()
    {
        if (!CensusText.Due(++_minutes))
        {
            return;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(Line(Window, CensusText.CensusMinutes));
        }

        Window.Clear();
    }
}
