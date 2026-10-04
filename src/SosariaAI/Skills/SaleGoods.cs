using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// What a seller puts on a shop counter, and whether a shop in reach takes any of it. The
/// planner and the sale read the same list: the planner once said "goods to sell" for a
/// smith's own ingots and for a gem no shop in reach bought, and the sale then walked to a
/// shop marker, found nobody who took it, and failed there 179 times in one run.
/// </summary>
public static class SaleGoods
{
    /// <summary>
    /// The pack items the seller would sell: harvest a shop buys by name and pawn loot, less
    /// the tools, gold, supplies, kit pieces, the stock or spare weapon it keeps, and a
    /// crafter's pieces held back for the bank.
    /// </summary>
    public static List<Item> ForSale(SosariaCharacter seller) => ForSale(seller, seller?.Backpack, harvestOnly: false);

    /// <summary>
    /// True when a live vendor within the leash of the seller buys at least one piece of
    /// the harvest, and at least one piece of the loot, in the pack or on the pack beast.
    /// Iron ore counts as the ingots a forge makes of it.
    /// </summary>
    public static (bool Harvest, bool Loot) BuyableInReach(SosariaCharacter seller)
    {
        var map = seller?.Map;

        if (map == null || map == Map.Internal)
        {
            return (false, false);
        }

        var buyerInReach = BuyerInReach(seller);
        var goods = ForSale(seller);
        goods.AddRange(ForSale(seller, PackAnimals.BeastOf(seller)?.Backpack, harvestOnly: true));

        var harvest = false;
        var loot = false;

        for (var i = 0; i < goods.Count && !(harvest && loot); i++)
        {
            var item = goods[i];
            var isHarvest = HarvestPack.IsSellable(item);

            if ((isHarvest ? harvest : loot) || !buyerInReach(item))
            {
                continue;
            }

            harvest |= isHarvest;
            loot |= !isHarvest;
        }

        return (harvest, loot);
    }

    /// <summary>
    /// The question "does a live vendor within the leash of the seller take this item over
    /// its counter", asked of one count of the map's buyers. Off the map nobody does. The
    /// bank reads it too: harvest no buyer in reach takes is banked, not carried for good.
    /// </summary>
    public static Func<Item, bool> BuyerInReach(SosariaCharacter seller)
    {
        var map = seller?.Map;

        if (map == null || map == Map.Internal)
        {
            return static _ => false;
        }

        var book = ShopBuyers.For(map);
        var reach = HomeLeash.ConfiguredRadius();
        var from = seller.Location;
        bool Deals(BaseVendor vendor) => ShopBuyers.Serves(vendor, seller);

        return item => AnyTakes(book, item, from, reach, Deals);
    }

    /// <summary>
    /// True when a buyer in <paramref name="book"/> within <paramref name="reach"/> tiles of
    /// <paramref name="from"/> pays for the item and still deals, and a counter takes the item at all.
    /// </summary>
    public static bool AnyTakes<T>(BuyerBook<T> book, Item item, Point3D from, int reach, Func<T, bool> deals) =>
        book != null && CounterTakes(item) && book.AnyBuys(SoldAs(item), from, reach, deals);

    /// <summary>The types a buyer must take for the items a counter takes at all, each once.</summary>
    public static List<Type> TypesOf(IReadOnlyList<Item> goods)
    {
        var types = new List<Type>();

        for (var i = 0; i < (goods?.Count ?? 0); i++)
        {
            var type = SoldAs(goods[i]);

            if (CounterTakes(goods[i]) && !types.Contains(type))
            {
                types.Add(type);
            }
        }

        return types;
    }

    /// <summary>The type a buyer pays for: iron ore sells as the iron ingots a forge makes of it.</summary>
    public static Type SoldAs(Item item) => item is IronOre ? typeof(IronIngot) : item?.GetType();

    /// <summary>
    /// A counter takes the item at all: the engine's sale skips an item that is not movable,
    /// blessed or insured, bound to its owner, or a container with something in it.
    /// </summary>
    public static bool CounterTakes(Item item) =>
        item is { Deleted: false, Movable: true, Nontransferable: false } &&
        item.IsStandardLoot() &&
        item is not Container { Items.Count: > 0 };

    /// <summary>
    /// What never goes on the counter: the working tools, the coin purse, the supplies the
    /// carrier burns, kit pieces, the stock its own trade burns or its best spare weapon, the
    /// finished pieces a crafter holds back to hawk at the bank, and the pieces a red keeps for
    /// its spare kit or its back (<see cref="SpareKit.KeptLoot"/>).
    /// </summary>
    private static bool Keeps(SosariaCharacter seller, Item item, CraftTrade trade, Item spareWeapon, HashSet<Item> hawked, HashSet<Item> spareKit) =>
        VendorSellRules.KeepItem(
            WorkerTools.IsWorkTool(item),
            item is Gold,
            HawkerGoods.IsSupply(item),
            WorkerTools.IsKitItem(seller, item),
            trade?.BurnsStock(item.GetType()) == true || item is BaseWeapon && ReferenceEquals(item, spareWeapon) ||
            hawked.Contains(item) || spareKit.Contains(item)
        );

    private static List<Item> ForSale(SosariaCharacter seller, Container pack, bool harvestOnly)
    {
        var goods = new List<Item>();

        if (seller == null || pack == null)
        {
            return goods;
        }

        var trade = CraftMarket.TradeOf(seller);
        var spare = GearEquip.BestPackWeapon(seller, allowRanged: true);
        var hawked = HawkerGoods.KeptToHawk(seller, trade);
        var spareKit = SpareKit.KeptLoot(seller);

        foreach (var item in pack.Items)
        {
            if (item is not { Deleted: false } ||
                !HarvestPack.IsSellable(item) && (harvestOnly || !PawnPack.IsPawnable(item)) ||
                Keeps(seller, item, trade, spare, hawked, spareKit))
            {
                continue;
            }

            goods.Add(item);
        }

        return goods;
    }
}
