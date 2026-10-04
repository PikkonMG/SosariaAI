using System;
using System.Collections.Generic;
using System.Linq;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>What the crafters of the shard did in one summary window.</summary>
public sealed class CraftWindow
{
    public Dictionary<string, int> Made { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Sessions { get; set; }

    public int PiecesSold { get; set; }

    public int GoldFromSales { get; set; }

    public int StockDeals { get; set; }

    public int StockUnits { get; set; }

    public int StockGold { get; set; }

    public int Masterworks { get; set; }

    public void Clear()
    {
        Made.Clear();
        Sessions = 0;
        PiecesSold = 0;
        GoldFromSales = 0;
        StockDeals = 0;
        StockUnits = 0;
        StockGold = 0;
        Masterworks = 0;
    }
}

/// <summary>
/// Counts pieces made per trade, sessions begun, pieces sold, stock bought from gatherers and
/// exceptional pieces called out, and every <see cref="CensusText.CensusMinutes"/> minutes writes one
/// activity line: "Crafts in the last 10 minutes: smith 12, tailor 30, ...". Every trade is
/// named, a zero included, so an empty shop shows. World thread only.
/// </summary>
public static class CraftTally
{
    /// <summary>The trades the line names, in its order.</summary>
    public static readonly string[] Trades =
    [
        SmithRules.Trade.Kind, TailorRules.Kind, CarpentryRules.Kind, FletchRules.Kind, AlchemyRules.Name,
        InscriptionRules.Kind, TinkerRules.Kind, CookRules.Kind, CartographyRules.Kind
    ];

    private static readonly ILogger logger = SosariaLog.For(typeof(CraftTally));
    private static readonly CraftWindow Window = new();
    private static int _minutes;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    public static void NoteSession() => Window.Sessions++;

    public static void NoteMade(string trade, int pieces)
    {
        if (!string.IsNullOrWhiteSpace(trade) && pieces > 0)
        {
            Window.Made[trade] = Window.Made.GetValueOrDefault(trade) + pieces;
        }
    }

    public static void NoteSold(int pieces, int gold)
    {
        Window.PiecesSold += Math.Max(0, pieces);
        Window.GoldFromSales += Math.Max(0, gold);
    }

    public static void NoteStockDeal(int units, int gold)
    {
        Window.StockDeals++;
        Window.StockUnits += Math.Max(0, units);
        Window.StockGold += Math.Max(0, gold);
    }

    public static void NoteMasterwork() => Window.Masterworks++;

    /// <summary>The summary line for one window's counts.</summary>
    public static string Line(CraftWindow window, int minutes)
    {
        var made = Trades.Select(trade => $"{trade.ToLowerInvariant()} {window?.Made.GetValueOrDefault(trade) ?? 0}");

        return $"Crafts in the last {minutes} minutes: {string.Join(", ", made)}; " +
               $"{window?.Sessions ?? 0} sessions begun; " +
               $"sold {window?.PiecesSold ?? 0} pieces for {window?.GoldFromSales ?? 0} gold; " +
               $"{window?.StockDeals ?? 0} stock deals with gatherers ({window?.StockUnits ?? 0} units for {window?.StockGold ?? 0} gold); " +
               $"{window?.Masterworks ?? 0} exceptional pieces called out";
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
