using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Economy;

namespace SosariaAI.Skills;

/// <summary>
/// Buying and selling across a shop counter through the vendor's own buy and sell lists,
/// the path a client's buy and sell gumps take. Only what the vendor stocks can be bought,
/// at the vendor's price, out of the buyer's pack; only what the vendor buys can be sold.
/// </summary>
public static class VendorDeal
{
    /// <summary>A vendor this close to the buyer is across the counter.</summary>
    public const int CounterRange = 8;

    /// <summary>
    /// How long a vendor keeps a piece it bought for resale, the engine's own inventory decay.
    /// The engine clears older pieces only when a client opens the buy list, and a simulated
    /// seller opens none: the resale shelves of the shard grew by 1,200 pieces an hour.
    /// </summary>
    public static readonly TimeSpan ResaleShelfLife = TimeSpan.FromHours(1);

    /// <summary>Vendors within <paramref name="range"/> of the person, nearest first.</summary>
    public static List<BaseVendor> VendorsNear(Mobile person, int range) =>
        person == null ? [] : VendorsNear(person.Map, person.Location, range);

    /// <summary>Vendors within <paramref name="range"/> of a spot, nearest first.</summary>
    public static List<BaseVendor> VendorsNear(Map map, Point3D at, int range)
    {
        var found = new List<(BaseVendor Vendor, double Distance)>();

        if (map == null || map == Map.Internal)
        {
            return [];
        }

        foreach (var mobile in map.GetMobilesInRange(at, range))
        {
            if (mobile is BaseVendor { Deleted: false } vendor && TradeRules.MayBeCounterpart(vendor.Player))
            {
                found.Add((vendor, vendor.GetDistanceToSqrt(at)));
            }
        }

        found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return found.ConvertAll(entry => entry.Vendor);
    }

    /// <summary>The first shelf line of <paramref name="vendor"/> that stocks one of <paramref name="types"/>.</summary>
    public static GenericBuyInfo ShelfLine(BaseVendor vendor, IReadOnlyList<Type> types) =>
        types == null ? null : ShelfLine(vendor, line => IsOneOf(line.Type, types));

    /// <summary>
    /// The first shelf line of <paramref name="vendor"/> with goods left on it that
    /// <paramref name="matches"/>, after the restock the vendor is due. An empty line is no
    /// stock: Trinsic's shelves sold out of sewing kits, and tailors there failed "could not get a tool"
    /// 33 times in half an hour.
    /// </summary>
    public static GenericBuyInfo ShelfLine(BaseVendor vendor, Func<GenericBuyInfo, bool> matches)
    {
        RestockIfDue(vendor);
        var info = vendor?.GetBuyInfo();

        for (var i = 0; i < (info?.Length ?? 0); i++)
        {
            if (info[i] is GenericBuyInfo { Type: not null, Amount: > 0 } line && matches(line))
            {
                return line;
            }
        }

        return null;
    }

    private static bool IsOneOf(Type type, IReadOnlyList<Type> types)
    {
        for (var t = 0; t < types.Count; t++)
        {
            if (types[t] != null && types[t].IsAssignableFrom(type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Units of <paramref name="types"/> on the shelves of <paramref name="vendors"/>.</summary>
    public static int OnShelves(IReadOnlyList<BaseVendor> vendors, IReadOnlyList<Type> types)
    {
        var total = 0;

        for (var i = 0; i < (vendors?.Count ?? 0); i++)
        {
            var line = ShelfLine(vendors[i], types);
            total += line?.Amount ?? 0;
        }

        return total;
    }

    /// <summary>The lowest unit price for <paramref name="types"/> on these shelves, or 0 when none stock it.</summary>
    public static int CheapestPrice(IReadOnlyList<BaseVendor> vendors, IReadOnlyList<Type> types)
    {
        var best = 0;

        for (var i = 0; i < (vendors?.Count ?? 0); i++)
        {
            var line = ShelfLine(vendors[i], types);

            if (line != null && (best == 0 || line.Price < best))
            {
                best = Math.Max(1, line.Price);
            }
        }

        return best;
    }

    /// <summary>
    /// Buys up to <paramref name="amount"/> units of <paramref name="types"/> from the first
    /// vendors that stock them, spending at most <paramref name="maxGold"/>. Returns the units
    /// that reached the buyer's pack.
    /// </summary>
    public static int Buy(Mobile buyer, IReadOnlyList<BaseVendor> vendors, IReadOnlyList<Type> types, int amount, int maxGold) =>
        Buy(buyer, vendors, types, amount, maxGold, out _);

    /// <summary>
    /// <see cref="Buy(Mobile, IReadOnlyList{BaseVendor}, IReadOnlyList{Type}, int, int)"/>, with the
    /// gold the counters took, pack and bank alike, in <paramref name="spent"/>. Goods paid for
    /// that did not reach the pack end the round: the next counter would take gold for the same.
    /// </summary>
    public static int Buy(
        Mobile buyer,
        IReadOnlyList<BaseVendor> vendors,
        IReadOnlyList<Type> types,
        int amount,
        int maxGold,
        out int spent
    )
    {
        var bought = 0;
        spent = 0;

        for (var i = 0; i < (vendors?.Count ?? 0) && bought < amount && spent < maxGold; i++)
        {
            var purchase = BuyFrom(buyer, vendors[i], types, amount - bought, maxGold - spent);
            bought += purchase.Delivered;
            spent += purchase.Spent;

            if (purchase.Lost)
            {
                break;
            }
        }

        return bought;
    }

    /// <summary>
    /// Sells <paramref name="items"/> the vendor buys. Returns the gold that came back into
    /// the seller's pack.
    /// </summary>
    public static int Sell(Mobile seller, BaseVendor vendor, IReadOnlyList<Item> items)
    {
        var info = vendor?.GetSellInfo();
        var pack = seller?.Backpack;

        if (info == null || pack == null || items == null)
        {
            return 0;
        }

        var list = new List<SellItemResponse>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];

            if (item is { Deleted: false } && item.IsChildOf(pack) && IsSellable(info, item))
            {
                list.Add(new SellItemResponse(item, item.Amount));
            }
        }

        if (list.Count == 0)
        {
            return 0;
        }

        var before = pack.GetAmount(typeof(Gold));

        if (!vendor.OnSellItems(seller, list))
        {
            return 0;
        }

        ClearStaleResale(vendor, Core.Now);
        var gold = Math.Max(0, pack.GetAmount(typeof(Gold)) - before);
        TradeTally.NoteShopSale(list.Count, gold);
        return gold;
    }

    /// <summary>
    /// Drops what lay on the vendor's resale shelf past <see cref="ResaleShelfLife"/>, as the
    /// engine does when a player opens the buy list. Call it after each sale. Returns the pieces dropped.
    /// </summary>
    public static int ClearStaleResale(Mobile vendor, DateTime now) =>
        vendor?.FindItemOnLayer(Layer.ShopBuy) is Container shelf ? ClearStaleResale(shelf, now) : 0;

    /// <summary>Drops each piece in <paramref name="shelf"/> that last moved <see cref="ResaleShelfLife"/> or more before <paramref name="now"/>.</summary>
    public static int ClearStaleResale(Container shelf, DateTime now)
    {
        var pieces = shelf.Items;
        var dropped = 0;

        for (var i = pieces.Count - 1; i >= 0; i--)
        {
            if (i < pieces.Count && pieces[i].LastMoved + ResaleShelfLife <= now)
            {
                pieces[i].Delete();
                dropped++;
            }
        }

        return dropped;
    }

    public static bool IsSellable(IShopSellInfo[] info, Item item)
    {
        for (var i = 0; i < (info?.Length ?? 0); i++)
        {
            if (info[i] != null && info[i].IsSellable(item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the vendor buys this item back.</summary>
    public static bool Buys(BaseVendor vendor, Item item) => IsSellable(vendor?.GetSellInfo(), item);

    /// <summary>
    /// The most of <paramref name="units"/> copies of <paramref name="sample"/> the pack holds,
    /// by the engine's own hold rule, the one a vendor's delivery runs: what the pack refuses
    /// the vendor drops at the buyer's feet, paid for. A pile adds its weight only; each tool
    /// is an item and a weight of its own.
    /// </summary>
    public static int UnitsThatFit(Mobile buyer, Container pack, Item sample, int units)
    {
        if (pack == null || sample == null)
        {
            return 0;
        }

        for (var fit = units; fit > 0; fit--)
        {
            var extraCopies = sample.Stackable ? 0 : fit - 1;
            var plusWeight = sample.Stackable
                ? (int)Math.Ceiling(sample.Weight * fit) - sample.PileWeight
                : extraCopies * (sample.PileWeight + sample.TotalWeight);

            if (pack.CheckHold(buyer, sample, false, true, extraCopies * (sample.TotalItems + 1), plusWeight))
            {
                return fit;
            }
        }

        return 0;
    }

    /// <summary>The items of <paramref name="type"/> on the ground under the person.</summary>
    public static HashSet<Serial> ItemsAtFeet(Mobile person, Type type)
    {
        var found = new HashSet<Serial>();

        foreach (var item in GroundAtFeet(person, type))
        {
            found.Add(item.Serial);
        }

        return found;
    }

    /// <summary>
    /// Puts into the pack what the vendor dropped at the buyer's feet: items of
    /// <paramref name="type"/> under the buyer that were not in <paramref name="alreadyThere"/>.
    /// </summary>
    public static void PickUpDropped(Mobile buyer, Container pack, Type type, ISet<Serial> alreadyThere)
    {
        foreach (var item in GroundAtFeet(buyer, type))
        {
            if (!alreadyThere.Contains(item.Serial))
            {
                pack.TryDropItem(buyer, item, false);
            }
        }
    }

    // Collected first: a pick-up takes the item off the map the scan walks.
    private static List<Item> GroundAtFeet(Mobile person, Type type)
    {
        var found = new List<Item>();
        var map = person?.Map;

        if (map == null || map == Map.Internal || type == null)
        {
            return found;
        }

        foreach (var item in map.GetItemsInRange(person.Location, 0))
        {
            if (item is { Deleted: false, Movable: true } && type.IsAssignableFrom(item.GetType()))
            {
                found.Add(item);
            }
        }

        return found;
    }

    private static Purchase BuyFrom(Mobile buyer, BaseVendor vendor, IReadOnlyList<Type> types, int amount, int maxGold)
    {
        var pack = buyer?.Backpack;
        var line = ShelfLine(vendor, types);

        if (pack == null || line?.GetDisplayEntity() is not Item sample)
        {
            return default;
        }

        var unit = Math.Max(1, line.Price);
        var units = UnitsThatFit(buyer, pack, sample, Math.Min(Math.Min(amount, line.Amount), maxGold / unit));

        if (units <= 0 || !PackFunds.FundPack(buyer, unit * units))
        {
            return default;
        }

        var before = CountOf(pack, line.Type);
        var goldBefore = GoldHeld(buyer);
        var alreadyThere = ItemsAtFeet(buyer, line.Type);

        if (!vendor.OnBuyItems(buyer, [new BuyItemResponse(sample.Serial, units)]))
        {
            return default;
        }

        var spent = Math.Max(0, goldBefore - GoldHeld(buyer));
        TradeTally.NoteShopBuy(spent);
        PickUpDropped(buyer, pack, line.Type, alreadyThere);
        var delivered = Math.Max(0, CountOf(pack, line.Type) - before);
        return new Purchase(delivered, spent, spent > 0 && delivered < units);
    }

    /// <summary>
    /// The gold a person can pay with: the pack's and the bank's. An order of
    /// <see cref="PackFunds.VendorBankPayMin"/> or more is drawn from the bank.
    /// </summary>
    public static int GoldHeld(Mobile person) => (person.Backpack?.GetAmount(typeof(Gold)) ?? 0) + Banker.GetBalance(person);

    private static int CountOf(Container pack, Type type) => pack.GetAmount(type, true);

    /// <summary>Restocks the vendor when its restock time is due, as the engine does when a player asks to buy.</summary>
    internal static void RestockIfDue(BaseVendor vendor)
    {
        if (vendor is IVendor trader && Core.Now - trader.LastRestock > trader.RestockDelay)
        {
            trader.Restock();
        }
    }

    /// <summary>One counter's sale: the units that reached the pack, the gold taken, and whether some paid for went missing.</summary>
    private readonly record struct Purchase(int Delivered, int Spent, bool Lost);
}
