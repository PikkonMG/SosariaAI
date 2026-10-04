using System;

namespace SosariaAI.Economy;

/// <summary>
/// Raw stock changing hands at the bank, the second market after the station. A crafter still
/// dry after its wait at the station walks to the bank, shouts WTB and waits there; a gatherer
/// who hears of it on its way to the shop brings its load and holds it up with a WTS; the
/// crafter walks over and the two haggle over the market table's price. Only stock the table
/// prices changes hands this way: ingots, ore, wood, cloth and hides. The crafter buys no more
/// than it can store and pay for at the top of the band, so any price it agrees to is covered.
/// A gatherer who heard of a want names the crafter still waiting when it arrives, and sells the
/// rest of its stack to the next one waiting. Pure.
/// </summary>
public static class BankStockRules
{
    /// <summary>A crafter or a gatherer walks at most this far to a bank for a stock deal.</summary>
    public const int BankReach = 100;

    /// <summary>How many banks, nearest first, a dry crafter looks over for one where stock is held up.</summary>
    public const int BanksLooked = 4;

    private const int OneUnit = 1;
    private const int NoneCarried = 0;

    /// <summary>How long a gatherer holds its stock up at the bank before it takes it to the shop.</summary>
    public const int OfferSeconds = 90;

    /// <summary>How long a dry crafter waits at the bank for a gatherer before it gives the stretch up.</summary>
    public const int WantSeconds = 180;

    /// <summary>How often a crafter waiting at the bank looks over the floor for a seller.</summary>
    public const int LookSeconds = 5;

    public static readonly TimeSpan OfferDwell = TimeSpan.FromSeconds(OfferSeconds);
    public static readonly TimeSpan WantDwell = TimeSpan.FromSeconds(WantSeconds);
    public static readonly TimeSpan LookGap = TimeSpan.FromSeconds(LookSeconds);

    /// <summary>Who buys the stock rows: the smith, the tailor and the woodworker.</summary>
    public const TradeAppetite CrafterAppetite = TradeAppetite.Smithing | TradeAppetite.Tailoring | TradeAppetite.Carpentry;

    /// <summary>True for a crafter's raw stock the market table prices by the stack: the bank deals in it.</summary>
    public static bool IsStockRow(GoodsRow row) =>
        row is { IsGear: false } && (row.Appetite & CrafterAppetite) != TradeAppetite.None;

    /// <summary>
    /// Units of a stack of <paramref name="offered"/> the crafter takes at the bank: room under
    /// the stock cap beside what it <paramref name="carried"/>, and what its purse past the
    /// tool reserve pays for at the top of the row's band. Nothing for goods the bank does not
    /// deal in by the stack.
    /// </summary>
    public static int LotUnits(int offered, int carried, int purse, GoodsRow row) =>
        IsStockRow(row) ? CraftMarketRules.UnitsToBuy(offered, carried, purse, row.UnitHigh) : 0;

    /// <summary>
    /// True when <paramref name="purse"/> pays for at least one unit of the row at the bank. Every
    /// bank buy of the first live run was cut short by the buyer's purse, and a crafter with too
    /// little coin stood out its whole wait beside a gatherer holding logs up.
    /// </summary>
    public static bool PaysForAUnit(int purse, GoodsRow row) => LotUnits(OneUnit, NoneCarried, purse, row) > 0;
}
