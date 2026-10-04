using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>A dry crafter waiting at the bank spot <paramref name="Bank"/> for stock of its trade, until it gives up.</summary>
public readonly record struct StockWant(SosariaCharacter Crafter, CraftTrade Trade, Point3D Bank, DateTime Until);

/// <summary>
/// The stock board at the banks (<see cref="BankStockRules"/>). A dry crafter posts what its
/// trade burns while it waits at the bank, and its WTB also stands in <see cref="TradeMarket"/>
/// so a person at a keyboard can answer it. A gatherer on its way to the shop asks the board
/// whether a crafter waits for its load; a crafter at the bank finds a gatherer holding stock up
/// among the bank's offers (<see cref="BankCrowd.HawkersNear"/>). A post counts only while its
/// crafter still stands at the bank it posted from: a gatherer once walked 150 logs to a Britain
/// fletcher who had gone out to cut wood. The board holds a few posts and is read, never the
/// map. World thread only.
/// </summary>
public static class BankStock
{
    private const char Space = ' ';

    private static readonly Dictionary<Serial, StockWant> Wants = new();

    /// <summary>The crafter waits where it stands at the bank for its trade's stock until <paramref name="until"/>.</summary>
    public static void PostWant(SosariaCharacter crafter, CraftTrade trade, DateTime until)
    {
        if (crafter == null || trade == null)
        {
            return;
        }

        Wants[crafter.Serial] = new StockWant(crafter, trade, crafter.Location, until);

        if (ClaimOf(trade) is { } claim)
        {
            TradeMarket.PostWant(crafter, claim, until);
        }
    }

    public static void DropWant(SosariaCharacter crafter)
    {
        if (crafter == null)
        {
            return;
        }

        Wants.Remove(crafter.Serial);
        TradeMarket.DropWant(crafter);
    }

    /// <summary>
    /// The nearest crafter waiting at a bank within <paramref name="reach"/> of the gatherer for
    /// stock it carries, with its trade, the stack to bring and the bank spot it waits at, or null.
    /// </summary>
    public static (SosariaCharacter Crafter, CraftTrade Trade, Item Stock, Point3D Bank)? WantFor(SosariaCharacter gatherer, int reach)
    {
        if (gatherer?.Backpack == null || Wants.Count == 0)
        {
            return null;
        }

        (SosariaCharacter, CraftTrade, Item, Point3D)? pick = null;
        var best = int.MaxValue;

        foreach (var want in LiveWants())
        {
            var crafter = want.Crafter;
            var distance = NavMetric.Chebyshev(gatherer.Location, want.Bank);

            if (crafter == gatherer || crafter.Map != gatherer.Map || distance > reach || distance >= best ||
                StockIn(gatherer, want.Trade) is not { } stock)
            {
                continue;
            }

            best = distance;
            pick = (crafter, want.Trade, stock, want.Bank);
        }

        return pick;
    }

    /// <summary>
    /// The nearest crafter waiting at the bank now within a bank floor's walk of the seller
    /// (<see cref="TradeRanges.WalkOverRange"/>) whose trade burns stock the seller spares, with
    /// its trade and that stack, or null. A gatherer names the buyer who stands there when it
    /// arrives, not the one it heard of on its way.
    /// </summary>
    public static (SosariaCharacter Crafter, CraftTrade Trade, Item Stock)? WaiterFor(SosariaCharacter seller)
    {
        if (seller?.Backpack == null || Wants.Count == 0)
        {
            return null;
        }

        (SosariaCharacter, CraftTrade, Item)? pick = null;
        var best = int.MaxValue;

        foreach (var want in LiveWants())
        {
            var crafter = want.Crafter;
            var distance = NavMetric.Chebyshev(seller.Location, crafter.Location);

            if (crafter == seller || crafter.Map != seller.Map || distance > TradeRanges.WalkOverRange || distance >= best ||
                StockIn(seller, want.Trade) is not { } stock)
            {
                continue;
            }

            best = distance;
            pick = (crafter, want.Trade, stock);
        }

        return pick;
    }

    /// <summary>
    /// True while a want still stands: it has not run out, and its crafter is alive and still
    /// within shouting range of the spot it posted from (<see cref="TradeRanges.ShoutRange"/>):
    /// a crafter crossing the floor to a seller is still at the bank.
    /// </summary>
    public static bool Stands(StockWant want, DateTime now) =>
        now <= want.Until && want.Crafter is { Deleted: false, Alive: true } crafter &&
        NavMetric.Chebyshev(crafter.Location, want.Bank) <= TradeRanges.ShoutRange;

    // The wants that still stand; the rest leave the board.
    private static List<StockWant> LiveWants()
    {
        var now = Core.Now;
        var live = new List<StockWant>(Wants.Count);

        foreach (var want in new List<StockWant>(Wants.Values))
        {
            if (Stands(want, now))
            {
                live.Add(want);
            }
            else
            {
                Wants.Remove(want.Crafter.Serial);
            }
        }

        return live;
    }

    /// <summary>
    /// The biggest stack in the seller's pack of stock the trade burns and the bank deals in, or
    /// null. Stock the seller's own trade burns counts only when it has some to spare
    /// (<see cref="CraftMarket.SpareUnits"/>): a smith who mines sells its surplus ingots.
    /// </summary>
    public static Item StockIn(SosariaCharacter seller, CraftTrade trade)
    {
        if (seller?.Backpack == null || trade == null)
        {
            return null;
        }

        Item best = null;

        foreach (var item in seller.Backpack.Items)
        {
            if (IsStock(item, trade) && CraftMarket.SpareUnits(seller, item) > 0 && item.Amount > (best?.Amount ?? 0))
            {
                best = item;
            }
        }

        return best;
    }

    /// <summary>The nearest bank within <see cref="BankStockRules.BankReach"/> of the character, or null.</summary>
    public static Destination BankInReach(SosariaCharacter character)
    {
        var bank = NavWorld.DestinationsFor(character?.HomeFacet)?.NearestBank(character.Location, PkRules.IsRed(character.Kills));
        return bank == null || NavMetric.Chebyshev(character.Location, bank.Arrival) > BankStockRules.BankReach ? null : bank;
    }

    /// <summary>
    /// The bank a dry crafter goes to wait at: the nearest one in reach where someone holds up
    /// stock the trade burns that <paramref name="purse"/> pays for, else the nearest one in
    /// reach, or null.
    /// </summary>
    public static Destination BankFor(SosariaCharacter crafter, CraftTrade trade, int purse) =>
        BankWithOffer(crafter, trade, purse, Point3D.Zero) ?? BankInReach(crafter);

    /// <summary>
    /// The nearest bank within <see cref="BankStockRules.BankReach"/> of the crafter, other than
    /// the one at <paramref name="except"/>, where someone holds up stock the trade burns that
    /// <paramref name="purse"/> pays for a lot of, or null. A crafter that waited out its want at
    /// one bank walks to the next bank with sellers.
    /// </summary>
    public static Destination BankWithOffer(SosariaCharacter crafter, CraftTrade trade, int purse, Point3D except)
    {
        var banks = NavWorld.DestinationsFor(crafter?.HomeFacet)
            ?.NearestFirst(BankTeller.BankToken, crafter.Location, BankStockRules.BanksLooked);
        var red = PkRules.IsRed(crafter?.Kills ?? 0);

        for (var i = 0; i < (banks?.Count ?? 0); i++)
        {
            var bank = banks[i].Arrival;

            if (NavMetric.Chebyshev(crafter.Location, bank) <= BankStockRules.BankReach &&
                (except == Point3D.Zero || NavMetric.Chebyshev(bank, except) > TradeRanges.ShoutRange) &&
                PkRules.MayBankAt(red, bank.X, bank.Y) && OfferedNear(crafter, trade, bank, purse))
            {
                return banks[i];
            }
        }

        return null;
    }

    /// <summary>
    /// True when a gatherer at a bank in reach holds up stock the trade burns and the crafter
    /// can pay for a lot of it, bank balance counted: a smith goes to buy it instead of digging
    /// its own ore.
    /// </summary>
    public static bool OfferedInReach(SosariaCharacter crafter, CraftTrade trade) =>
        BankWithOffer(crafter, trade, TradeHandOff.PurseAtBank(crafter), Point3D.Zero) != null;

    /// <summary>
    /// True when someone within a bank floor's walk of <paramref name="bank"/> holds up stock the
    /// trade burns and <paramref name="purse"/> pays for a lot of it (<see cref="BankStockRules.LotUnits"/>).
    /// </summary>
    public static bool OfferedNear(SosariaCharacter crafter, CraftTrade trade, Point3D bank, int purse)
    {
        if (crafter?.Map == null || trade == null)
        {
            return false;
        }

        foreach (var (seller, offer) in BankCrowd.HawkersNear(crafter.Map, bank, TradeRanges.WalkOverRange))
        {
            if (HeldUp(crafter, trade, seller, offer) is { } stock &&
                BankStockRules.LotUnits(
                    stock.Amount,
                    crafter.Backpack?.GetAmount(stock.GetType()) ?? 0,
                    purse,
                    Appraisal.RowOf(stock)
                ) > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A gatherer within a bank floor's walk of the crafter holding up stock the trade burns,
    /// with that stack and its asking price, or null. The nearest free one wins; one the crafter
    /// just failed to agree with is not asked again soon.
    /// </summary>
    public static (SosariaCharacter Seller, Item Stock, HawkerOffer Offer)? OfferFor(SosariaCharacter crafter, CraftTrade trade)
    {
        if (crafter?.Map == null || trade == null)
        {
            return null;
        }

        (SosariaCharacter, Item, HawkerOffer)? pick = null;
        var best = double.MaxValue;

        foreach (var (seller, offer) in BankCrowd.HawkersNear(crafter.Map, crafter.Location, TradeRanges.WalkOverRange))
        {
            if (TradeSessions.IsBusy(seller) || TradeMarket.Refused(crafter, seller) ||
                HeldUp(crafter, trade, seller, offer) is not { } stock)
            {
                continue;
            }

            var distance = crafter.GetDistanceToSqrt(seller);

            if (distance < best)
            {
                best = distance;
                pick = (seller, stock, offer);
            }
        }

        return pick;
    }

    /// <summary>
    /// The crafter's visit to a gatherer within a bank floor's walk holding up stock the trade
    /// burns (<see cref="OfferFor"/>): it cuts the lot it can store and pay for, bank balance
    /// counted when a banker hears it, and walks over to haggle for it (<see cref="DealVisit"/>).
    /// Null when nobody holds such stock up, the purse pays for none, or the visit cannot start.
    /// </summary>
    public static DealVisit StartBuy(SosariaCharacter crafter, CraftTrade trade)
    {
        if (OfferFor(crafter, trade) is not { } offer)
        {
            return null;
        }

        var stock = offer.Stock;
        var units = BankStockRules.LotUnits(
            stock.Amount,
            crafter.Backpack?.GetAmount(stock.GetType()) ?? 0,
            TradeHandOff.Purse(crafter),
            Appraisal.RowOf(stock)
        );

        return CutLot(stock, units)
            ? DealVisit.Start(crafter, offer.Seller, stock, Appraisal.Value(stock, Utility.Random(Appraisal.PercentScale)))
            : null;
    }

    /// <summary>
    /// Cuts the lot the buyer takes off the seller's stack: the stack keeps its serial and
    /// <paramref name="units"/>, and the rest stays in the seller's pack as a stack of its own.
    /// False when nothing is taken or the stack will not split; nothing moves then.
    /// </summary>
    public static bool CutLot(Item stock, int units)
    {
        if (stock == null || units <= 0)
        {
            return false;
        }

        return units >= stock.Amount || Mobile.LiftItemDupe(stock, units) != null;
    }

    /// <summary>The WTB the trade's stock reads as on the market table, from its own words: "ingots".</summary>
    public static GoodsClaim? ClaimOf(CraftTrade trade)
    {
        if (string.IsNullOrWhiteSpace(trade?.StockNoun) ||
            !Appraisal.TryRead(trade.StockNoun.Split(Space, StringSplitOptions.RemoveEmptyEntries), out var claim, out _) ||
            !BankStockRules.IsStockRow(claim.Row))
        {
            return null;
        }

        return claim;
    }

    // The stack another person holds up that is stock the trade burns, still in its pack, or null.
    private static Item HeldUp(SosariaCharacter crafter, CraftTrade trade, SosariaCharacter seller, HawkerOffer offer) =>
        seller != crafter && World.FindItem(offer.Item) is { } stock && stock.IsChildOf(seller.Backpack) && IsStock(stock, trade)
            ? stock
            : null;

    private static bool IsStock(Item item, CraftTrade trade) =>
        item is { Deleted: false, Movable: true } && trade.BurnsStock(item.GetType()) &&
        BankStockRules.IsStockRow(Appraisal.RowOf(item));
}
