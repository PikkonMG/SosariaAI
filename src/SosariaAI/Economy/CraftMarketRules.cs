using System;
using SosariaAI.Skills;

namespace SosariaAI.Economy;

/// <summary>
/// A gatherer's load changing hands with a crafter for coin: miners' iron ingots to the smith,
/// lumberjacks' logs to the carpenter and the bowyer. The crafter pays under the shop's shelf
/// price and the gatherer gets more than the shop would pay back, so both take the deal over
/// the counter. The crafter buys no more than it can store and keeps a reserve for its tool.
/// Pure.
/// </summary>
public static class CraftMarketRules
{
    /// <summary>A gatherer and a crafter within this many tiles of each other can deal.</summary>
    public const int MeetRange = 12;

    /// <summary>Units of one stock a crafter keeps at most; past this it turns a load away.</summary>
    public const int StockCap = 200;

    /// <summary>
    /// Units of its own stock a crafter who gathers it keeps for its own station: a few long
    /// stretches of work. What it digs or cuts past this is surplus it sells to other crafters.
    /// </summary>
    public const int OwnStockKeep = StockCap / 2;

    /// <summary>
    /// Below this many units of its stock a crafter who gathers it goes for more, when no shop and
    /// no gatherer at the bank sells it any: about one plate chest's ingots.
    /// </summary>
    public const int OwnStockLow = 25;

    /// <summary>The gatherer's price is this share of the shop's shelf price, in tenths.</summary>
    public const int ShelfShareTenths = 7;

    /// <summary>A unit's price where no shop in reach stocks it.</summary>
    public const int FallbackUnitPrice = 2;

    private const int Tenths = 10;
    private const int MinUnitPrice = 1;

    /// <summary>What a unit fetches between a gatherer and a crafter, from the shop's shelf price (0 when none stocks it).</summary>
    public static int UnitPrice(int shelfPrice) =>
        shelfPrice <= 0 ? FallbackUnitPrice : Math.Max(MinUnitPrice, shelfPrice * ShelfShareTenths / Tenths);

    /// <summary>
    /// What a crafter pays for one unit of a material: the gatherer's price (<see cref="UnitPrice"/>)
    /// when <paramref name="gathererSells"/> it, else the full shelf price. Reagents, empty bottles
    /// and blank scrolls come only off a shelf. Priced at the gatherer's share, a lesser cure potion
    /// (garlic 3 and a bottle 5 at the shelf) looked 2 gold ahead at a counter that pays 7, and
    /// every alchemist spent its purse down to the tool reserve and never brewed again.
    /// </summary>
    public static int MaterialUnitPrice(int shelfPrice, bool gathererSells) =>
        gathererSells || shelfPrice <= 0 ? UnitPrice(shelfPrice) : shelfPrice;

    /// <summary>
    /// What the materials of one piece cost the crafter: the <paramref name="carried"/> units it
    /// holds at <see cref="MaterialUnitPrice"/>, and the rest at the shelf, where it must buy
    /// them. Priced wholly at the gatherer's share, a smith with an empty pack saw a profit in
    /// ingots it then bought at the full shelf price.
    /// </summary>
    public static int PieceMaterialCost(int perPiece, int carried, int shelfPrice, bool gathererSells)
    {
        var need = Math.Max(0, perPiece);
        var fromPack = Math.Min(need, Math.Max(0, carried));
        return fromPack * MaterialUnitPrice(shelfPrice, gathererSells) +
               (need - fromPack) * MaterialUnitPrice(shelfPrice, gathererSells: false);
    }

    /// <summary>
    /// Units the crafter takes of <paramref name="offered"/>: no more than room under the
    /// stock cap beside what it <paramref name="carried"/>, and no more than its gold past the
    /// tool reserve (<see cref="CraftTradeRules.StockBudget"/>) pays for.
    /// </summary>
    public static int UnitsToBuy(int offered, int carried, int gold, int unitPrice)
    {
        if (offered <= 0 || unitPrice <= 0)
        {
            return 0;
        }

        var room = Math.Max(0, StockCap - Math.Max(0, carried));
        var affordable = CraftTradeRules.StockBudget(gold) / unitPrice;
        return Math.Min(offered, Math.Min(room, affordable));
    }

    /// <summary>Units a crafter who gathers its own stock carries past what it keeps for its own craft.</summary>
    public static int Surplus(int carried) => Math.Max(0, carried - OwnStockKeep);

    /// <summary>
    /// True when a crafter who gathers its own stock goes out for it: it runs low and no gatherer
    /// at a bank in reach sells it any it can afford. The shelf is no answer: by the engine's sell
    /// tables only six of 54 smith pieces and one carpenter's piece pay back shelf stock, so a
    /// 1999 smith dug its own ore or bought a miner's, and smiths who bought at the counter ended
    /// "could not get materials" with an empty purse.
    /// </summary>
    public static bool GoesForOwnStock(int carried, bool gathererSells) => carried < OwnStockLow && !gathererSells;
}
