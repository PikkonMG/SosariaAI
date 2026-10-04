using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Goods a person can hold up and sell to another player; the market table prices and names
/// them. A worn weapon, the spellbook, a working tool or a kit piece is
/// never for sale; neither is gold, a supply the person burns, or the raw stock of a
/// crafter's own trade: a smith hawks the plate it made, not the ingots it makes it from.
/// What a crafter's own trade makes is goods even when others burn it or work with it: a
/// bowyer hawks its arrows, a scribe its recall scrolls, a tinker the tongs past its own tools.
/// </summary>
public static class HawkerGoods
{
    /// <summary>The goods in the pack worth the most, or null when there is nothing to sell.</summary>
    public static Item BestInPack(SosariaCharacter person) =>
        MostValuable(person?.Backpack?.Items ?? [], item => IsForSale(person, item));

    /// <summary>
    /// The finished goods a crafter of <paramref name="trade"/> holds back from the shop to hawk
    /// at the bank: its best few pieces or stacks of its own make for sale
    /// (<see cref="CraftTradeRules.PiecesToKeep"/>). A stack counts only when the trade makes it:
    /// an alchemist keeps its heal potions for the bank floor, where they fetch the market price
    /// a shop counter never paid. Anyone who lives by no station trade holds nothing back.
    /// </summary>
    public static HashSet<Item> KeptToHawk(SosariaCharacter person, CraftTrade trade)
    {
        var kept = new HashSet<Item>();

        if (trade == null || person?.Backpack == null)
        {
            return kept;
        }

        var pieces = new List<Item>();
        var values = new List<int>();

        foreach (var item in person.Backpack.Items)
        {
            if ((!item.Stackable || MadeToSell(trade, item)) && IsForSale(person, item) && Appraisal.RowOf(item) != Appraisal.Other)
            {
                pieces.Add(item);
                values.Add(Appraisal.Value(item, Appraisal.MidRoll));
            }
        }

        foreach (var index in CraftTradeRules.PiecesToKeep(values))
        {
            kept.Add(pieces[index]);
        }

        return kept;
    }

    /// <summary>
    /// The most valuable piece a crafter holds back to hawk (<see cref="KeptToHawk"/>), or null
    /// for anyone who lives by no station trade or kept nothing.
    /// </summary>
    public static Item BestKept(SosariaCharacter person) =>
        MostValuable(KeptToHawk(person, CraftMarket.TradeOf(person)), _ => true);

    /// <summary>
    /// Goods to hold up and shout about: tradable, and not a supply the person burns unless its
    /// own trade makes it.
    /// </summary>
    public static bool IsForSale(SosariaCharacter person, Item item)
    {
        var trade = CraftMarket.TradeOf(person);
        return IsTradable(person, item, trade) && (!IsSupply(item) || MadeToSell(trade, item)) &&
               trade?.BurnsStock(item.GetType()) != true;
    }

    /// <summary>Goods that may change hands in a deal, supplies included.</summary>
    public static bool IsTradable(SosariaCharacter person, Item item) => IsTradable(person, item, CraftMarket.TradeOf(person));

    /// <summary>
    /// True when <paramref name="trade"/> makes the item for sale: its craft list holds the type,
    /// and it is not the trade's own working tool.
    /// </summary>
    public static bool MadeToSell(CraftTrade trade, Item item) =>
        trade != null && item != null && item.GetType() != trade.OwnToolType &&
        trade.System?.CraftItems.SearchFor(item.GetType()) != null;

    private static bool IsTradable(SosariaCharacter person, Item item, CraftTrade trade)
    {
        if (person == null || item is not { Deleted: false, Movable: true } || item is Gold or Container ||
            item.Parent == person)
        {
            return false;
        }

        if (WorkerTools.IsWorkTool(item) || item is BaseTool && !MadeToSell(trade, item))
        {
            return false;
        }

        // A finished map is worthless; a map the hawker can hunt itself is not for sale.
        if (item is TreasureMap map && (map.Completed || TreasureMaps.MayHunt(person, map)))
        {
            return false;
        }

        return !WorkerTools.IsKitItem(person, item);
    }

    /// <summary>
    /// A supply a person burns: any reagent, or a type the supply check restocks
    /// (<see cref="SupplyCheck.IsRestocked"/>). A thief with its own twenty lockpicks cried them
    /// for sale at the bank, and banked them only for the supply draw to take them back out.
    /// </summary>
    public static bool IsSupply(Item item) => item is BaseReagent || SupplyCheck.IsRestocked(item);

    // The item worth the most at the market table among those included, or null when none is worth anything.
    private static Item MostValuable(IEnumerable<Item> items, Func<Item, bool> include)
    {
        Item best = null;
        var bestValue = 0;

        foreach (var item in items)
        {
            if (!include(item))
            {
                continue;
            }

            var value = Appraisal.Value(item, Appraisal.MidRoll);

            if (value > bestValue)
            {
                bestValue = value;
                best = item;
            }
        }

        return best;
    }
}
