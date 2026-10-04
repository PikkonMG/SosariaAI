using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>A piece on a player vendor that a browser wants and would pay the price of.</summary>
public readonly record struct VendorPiece(PlayerVendor Vendor, Item Piece, int Price);

/// <summary>
/// The player vendors of the shard, where a browser looks for a piece before it looks round an
/// NPC shop: houses held vendors, and people walked the houses to see what was up. The vendors
/// are counted at most once a minute, the first time someone asks, as the shop census is
/// (<see cref="ShopBuyers"/>). A browser takes a piece it wants by its class, at a price its
/// purse (bank included) pays and its temper would pay for goods of that worth. World thread only.
/// </summary>
public static class PlayerVendorMall
{
    /// <summary>How far, in tiles, a browser walks to a player vendor.</summary>
    public const int ShopReach = 150;

    /// <summary>Out of a hundred, how often a browse looks at the player vendors in reach first.</summary>
    public const int BrowseVendorPercent = 50;

    /// <summary>A buyer this close to the vendor may buy from it.</summary>
    public const int BuyRange = 3;

    public static readonly TimeSpan CountEvery = TimeSpan.FromMinutes(1);

    private static List<PlayerVendor> _vendors = [];
    private static DateTime _countedAt;

    /// <summary>True when the browse roll sends this browser to the player vendors first.</summary>
    public static bool LooksAtVendors(int roll) => PercentRoll.Under(roll, BrowseVendorPercent);

    /// <summary>
    /// True when a buyer with <paramref name="purse"/> (bank included) and a ceiling of
    /// <paramref name="ceiling"/> for goods of this worth pays <paramref name="price"/>.
    /// </summary>
    public static bool WouldPay(int price, int purse, int ceiling) => price > 0 && price <= purse && price <= ceiling;

    /// <summary>The nearest vendor in reach with a piece the buyer wants and would pay for, or null.</summary>
    public static VendorPiece? PieceFor(SosariaCharacter buyer, int reach, DateTime now)
    {
        if (buyer?.Backpack == null || !People.InWorld(buyer))
        {
            return null;
        }

        var purse = VendorDeal.GoldHeld(buyer);
        var appetite = TradeMarket.AppetiteOf(buyer);
        var temper = TradeSession.TemperOf(buyer);
        VendorPiece? pick = null;
        var best = double.MaxValue;

        foreach (var vendor in Vendors())
        {
            if (vendor.Deleted || vendor.Map != buyer.Map || vendor.IsOwner(buyer) || !buyer.InRange(vendor, reach) ||
                vendor.Backpack == null)
            {
                continue;
            }

            var distance = buyer.GetDistanceToSqrt(vendor);

            if (distance >= best || WantedPiece(vendor, appetite, purse, temper, now) is not { } piece)
            {
                continue;
            }

            best = distance;
            pick = piece;
        }

        return pick;
    }

    private static VendorPiece? WantedPiece(PlayerVendor vendor, TradeAppetite appetite, int purse, HaggleTemper temper, DateTime now)
    {
        foreach (var item in vendor.Backpack.Items)
        {
            if (vendor.GetVendorItem(item) is not { } listing ||
                !PlayerVendorRules.OnSale(listing.Valid, listing.Price, listing.Created, now) ||
                !TradeDemandRules.Wants(appetite, Appraisal.RowOf(item)))
            {
                continue;
            }

            var ceiling = Haggle.Buying(Appraisal.Value(item, Appraisal.MidRoll), purse, temper).Limit;

            if (WouldPay(listing.Price, purse, ceiling))
            {
                return new VendorPiece(vendor, item, listing.Price);
            }
        }

        return null;
    }

    private static List<PlayerVendor> Vendors()
    {
        if (_countedAt != default && Core.Now - _countedAt < CountEvery)
        {
            return _vendors;
        }

        var found = new List<PlayerVendor>();

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is PlayerVendor { Deleted: false } vendor && People.InWorld(vendor))
            {
                found.Add(vendor);
            }
        }

        _vendors = found;
        _countedAt = Core.Now;
        return _vendors;
    }
}
