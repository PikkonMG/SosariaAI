using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Mobiles;

namespace SosariaAI.Economy;

/// <summary>
/// World side of <see cref="BuyerBook{T}"/>: every live vendor of each map and what its own
/// buy list takes, counted at most once a minute the first time someone asks. A seller in
/// Magincia walked to the smith marker for its loot 153 times and found nobody there who
/// bought it; the book knows who really stands and buys. A vendor that died or moved since
/// the count is checked again at the moment of asking. World thread only.
/// </summary>
public static class ShopBuyers
{
    /// <summary>How long one count serves. Vendors respawn on minutes, not seconds.</summary>
    public static readonly TimeSpan CountEvery = TimeSpan.FromMinutes(1);

    private static Dictionary<Map, BuyerBook<BaseVendor>> _books = new();
    private static DateTime _countedAt;

    /// <summary>The book for <paramref name="map"/>, counted again when the last count is a minute old.</summary>
    public static BuyerBook<BaseVendor> For(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return BuyerBook<BaseVendor>.Empty;
        }

        if (_countedAt == default || Core.Now - _countedAt >= CountEvery)
        {
            Count();
        }

        return _books.TryGetValue(map, out var book) ? book : BuyerBook<BaseVendor>.Empty;
    }

    /// <summary>A stall opened between counts joins its map's book at once.</summary>
    public static void Note(BaseVendor vendor)
    {
        if (Trades(vendor, vendor?.Map) && _books.TryGetValue(vendor.Map, out var book))
        {
            book.Add(vendor, vendor.Location, TypesBought(vendor));
        }
    }

    /// <summary>True while the vendor still stands on <paramref name="map"/> and buys over its counter.</summary>
    public static bool Trades(BaseVendor vendor, Map map) =>
        vendor is { Deleted: false, IsActiveBuyer: true } &&
        map != null && map != Map.Internal && vendor.Map == map &&
        TradeRules.MayBeCounterpart(vendor.Player);

    /// <summary>
    /// True when the vendor trades on the seller's map, deals with the seller, and stands where
    /// the seller may walk: a red out of the guards has no counter under them
    /// (<see cref="VendorSellRules.MayWalkToCounter"/>).
    /// </summary>
    public static bool Serves(BaseVendor vendor, SosariaCharacter seller) =>
        seller != null &&
        Trades(vendor, seller.Map) &&
        vendor.CheckVendorAccess(seller) &&
        VendorSellRules.MayWalkToCounter(seller.Motor.KeepsOffGuards, SosariaCharacter.UnderGuards(vendor));

    private static void Count()
    {
        var books = new Dictionary<Map, BuyerBook<BaseVendor>>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not BaseVendor vendor || !Trades(vendor, vendor.Map))
            {
                continue;
            }

            if (!books.TryGetValue(vendor.Map, out var book))
            {
                book = new BuyerBook<BaseVendor>();
                books[vendor.Map] = book;
            }

            book.Add(vendor, vendor.Location, TypesBought(vendor));
        }

        _books = books;
        _countedAt = Core.Now;
    }

    private static IEnumerable<Type> TypesBought(BaseVendor vendor)
    {
        var lists = vendor.GetSellInfo();

        for (var i = 0; i < (lists?.Length ?? 0); i++)
        {
            var types = lists[i]?.Types;

            for (var t = 0; t < (types?.Length ?? 0); t++)
            {
                yield return types[t];
            }
        }
    }
}
