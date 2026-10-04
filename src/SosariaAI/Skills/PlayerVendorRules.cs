using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Systems.FeatureFlags;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// House vendors stay owned when the owner is away. Stock uses the vendor backpack. The owner
/// prices each piece from the market table instead of the engine's flat 999 that nobody paid,
/// collects what the vendor took in, and leaves a few days of upkeep with it. A buyer pays the
/// vendor the way the engine's buy gump makes a player pay: pack gold first, the rest from the
/// bank, and the price into the vendor's held gold. World thread only, except the pure rules.
/// </summary>
public static class PlayerVendorRules
{
    public const int NoVendorSerial = 0;

    /// <summary>
    /// Where in the market band a vendor's price sits, out of a hundred: a little under the
    /// bank-floor middle. The owner is not there to haggle and would rather the piece sold, so a
    /// buyer of fair temper (<see cref="Haggle.CeilingShare"/>) pays it.
    /// </summary>
    public const int VendorPriceRoll = 40;

    /// <summary>Days of the vendor's daily charge its owner leaves with it when it collects.</summary>
    public const int UpkeepDaysKept = 3;

    /// <summary>The engine sells no piece put up less than this many minutes ago.</summary>
    public const int FreshStockMinutes = 1;

    public static readonly TimeSpan FreshStockWait = TimeSpan.FromMinutes(FreshStockMinutes);

    /// <summary>An owner of a live house places a vendor when the house has none yet.</summary>
    public static bool MayPlace(int houseSerial, int vendorCount) =>
        houseSerial > HouseRules.NoHouseSerial && vendorCount == 0;

    public static PlayerVendor OwnedVendor(int vendorSerial)
    {
        if (vendorSerial <= NoVendorSerial)
        {
            return null;
        }

        return World.FindMobile((Serial)(uint)vendorSerial) is PlayerVendor { Deleted: false } vendor
            ? vendor
            : null;
    }

    /// <summary>What the owner asks for a piece on its vendor.</summary>
    public static int PriceFor(Item item) => Appraisal.Value(item, VendorPriceRoll);

    /// <summary>The held gold the owner takes away: all but <see cref="UpkeepDaysKept"/> days of the daily charge.</summary>
    public static int Earnings(int holdGold, int chargePerDay) =>
        Math.Max(0, holdGold - Math.Max(0, chargePerDay) * UpkeepDaysKept);

    /// <summary>A piece a character may buy: a live listing with a price, up for at least a minute.</summary>
    public static bool OnSale(bool valid, int price, DateTime created, DateTime now) =>
        valid && price > 0 && now - created >= FreshStockWait;

    /// <summary>
    /// True when the owner's vendor wants a visit: pack goods to put up, or takings to collect.
    /// Only an owner with a standing vendor has such an errand.
    /// </summary>
    public static bool HasErrand(SosariaCharacter owner)
    {
        if (owner == null || !owner.HouseOwned() || OwnedVendor(owner.VendorSerial) is not { } vendor)
        {
            return false;
        }

        return Earnings(vendor.HoldGold, vendor.ChargePerDay) > 0 || StockIn(owner).Count > 0;
    }

    public static bool Stock(PlayerVendor vendor, Item item)
    {
        if (vendor == null || vendor.Deleted || item == null || item.Deleted)
        {
            return false;
        }

        vendor.Backpack?.DropItem(item);

        if (vendor.GetVendorItem(item) is not { } listing)
        {
            return false;
        }

        listing.Price = PriceFor(item);
        return true;
    }

    /// <summary>
    /// Puts up every piece the owner would sell (<see cref="HawkerGoods.IsForSale"/>) that the
    /// market table knows, except the best pieces a crafter keeps to hawk at the bank.
    /// </summary>
    public static bool RestockFrom(SosariaCharacter owner, PlayerVendor vendor)
    {
        if (owner?.Backpack == null || vendor == null || vendor.Deleted)
        {
            return false;
        }

        var packed = false;

        foreach (var item in StockIn(owner))
        {
            packed |= Stock(vendor, item);
        }

        return packed;
    }

    /// <summary>The owner takes the vendor's takings into its bank, the engine's own way. Returns the gold moved.</summary>
    public static int Collect(SosariaCharacter owner, PlayerVendor vendor)
    {
        if (owner == null || vendor is not { Deleted: false })
        {
            return 0;
        }

        var earnings = Earnings(vendor.HoldGold, vendor.ChargePerDay);
        return earnings > 0 ? vendor.GiveGold(owner, earnings) : 0;
    }

    /// <summary>
    /// The buyer pays the vendor for <paramref name="item"/> as the engine's buy gump makes a
    /// player pay: the whole price must be there, pack and bank together, the piece must fit
    /// the pack, pack gold goes first and the bank pays the rest, and the price joins the
    /// vendor's held gold. False when anything is short; nothing moves then.
    /// </summary>
    public static bool Buy(SosariaCharacter buyer, PlayerVendor vendor, Item item, DateTime now)
    {
        if (!ContentFeatureFlags.PlayerVendors || buyer?.Backpack == null || vendor is not { Deleted: false } ||
            item is not { Deleted: false } || !item.IsChildOf(vendor.Backpack) || vendor.IsOwner(buyer) ||
            !vendor.CanInteractWith(buyer, false) || vendor.GetVendorItem(item) is not { } listing ||
            !OnSale(listing.Valid, listing.Price, listing.Created, now))
        {
            return false;
        }

        var price = listing.Price;

        if (VendorDeal.GoldHeld(buyer) < price || !buyer.PlaceInBackpack(item))
        {
            return false;
        }

        var left = price - buyer.Backpack.ConsumeUpTo(typeof(Gold), price);

        if (left > 0)
        {
            Banker.Withdraw(buyer, left);
        }

        vendor.HoldGold += price;
        vendor.Say(buyer.Name);
        TradeTally.NoteVendorSale(price);
        return true;
    }

    // The pack pieces the owner puts up.
    private static List<Item> StockIn(SosariaCharacter owner)
    {
        var stock = new List<Item>();
        var kept = HawkerGoods.KeptToHawk(owner, CraftMarket.TradeOf(owner));

        foreach (var item in owner.Backpack?.Items ?? [])
        {
            if (HawkerGoods.IsForSale(owner, item) && Appraisal.RowOf(item) != Appraisal.Other && !kept.Contains(item))
            {
                stock.Add(item);
            }
        }

        return stock;
    }
}
