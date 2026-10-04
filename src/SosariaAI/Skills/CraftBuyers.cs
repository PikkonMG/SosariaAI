using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Economy;

namespace SosariaAI.Skills;

/// <summary>
/// What a finished piece fetches from the buyers in reach (<see cref="CraftTradeRules.PieceWorth"/>):
/// the best price a vendor pays by the engine's own sell tables (the SBInfo lists of the era),
/// else a share of what people pay for goods they want, fighters' gear, arrows, bandages and
/// potions among them. The Britain tailor's table has no oil cloth and the carpenter's no
/// writing table, so a piece like that is worth nothing and is never made. The engine prices a
/// real item, so a plain sample of each product is made once, read and deleted; its prices stay
/// known for the run. World thread only.
/// </summary>
public static class CraftBuyers
{
    private sealed class ProductFacts
    {
        public int PeopleValue;
        public readonly Dictionary<Type, int> TablePrices = new();
    }

    private static readonly Dictionary<Type, ProductFacts> Facts = new();

    /// <summary>The sell tables of <paramref name="vendors"/>: what each pays for the goods a player brings.</summary>
    public static List<IShopSellInfo> TablesOf(IReadOnlyList<BaseVendor> vendors)
    {
        var tables = new List<IShopSellInfo>();

        for (var v = 0; v < (vendors?.Count ?? 0); v++)
        {
            tables.AddRange(vendors[v].GetSellInfo() ?? []);
        }

        return tables;
    }

    /// <summary>
    /// What a piece of <paramref name="product"/> fetches from the vendors' sell
    /// <paramref name="tables"/> or from people; 0 when nobody buys it.
    /// </summary>
    public static int WorthOf(Type product, IReadOnlyList<IShopSellInfo> tables)
    {
        if (product == null)
        {
            return 0;
        }

        var facts = FactsOf(product);
        var best = 0;

        for (var t = 0; t < (tables?.Count ?? 0); t++)
        {
            if (tables[t] is { } table && Array.IndexOf(table.Types, product) >= 0)
            {
                best = Math.Max(best, TablePrice(facts, table, product));
            }
        }

        return CraftTradeRules.PieceWorth(best, facts.PeopleValue);
    }

    /// <summary>
    /// The market table's value of a piece people want, or 0: raw stock goes to crafters and
    /// goods off the table, furniture among them, go only to a counter.
    /// </summary>
    public static int PeopleValueOf(Item piece)
    {
        var row = Appraisal.RowOf(piece);
        return row == Appraisal.Other || (row.Appetite & ~BankStockRules.CrafterAppetite) == TradeAppetite.None
            ? 0
            : Appraisal.Value(piece, Appraisal.MidRoll);
    }

    private static int TablePrice(ProductFacts facts, IShopSellInfo table, Type product)
    {
        var key = table.GetType();

        if (!facts.TablePrices.TryGetValue(key, out var price))
        {
            price = WithSample(product, table.GetSellPriceFor);
            facts.TablePrices[key] = price;
        }

        return price;
    }

    private static ProductFacts FactsOf(Type product)
    {
        if (!Facts.TryGetValue(product, out var facts))
        {
            facts = new ProductFacts { PeopleValue = WithSample(product, PeopleValueOf) };
            Facts[product] = facts;
        }

        return facts;
    }

    /// <summary>
    /// True when <paramref name="product"/> is an item the engine can build with no arguments (every
    /// parameter optional, as in <c>FancyShirt(int hue = 0)</c>). A tinker's trap entry names a
    /// craft type, not an item, and the engine's activator prints a line for each type it cannot build.
    /// </summary>
    public static bool MakesSample(Type product) =>
        typeof(Item).IsAssignableFrom(product) && !product.IsAbstract &&
        Array.Exists(product.GetConstructors(), ctor => Array.TrueForAll(ctor.GetParameters(), parameter => parameter.IsOptional));

    // A plain piece of the product, read and deleted at once; 0 when the type makes none.
    private static int WithSample(Type product, Func<Item, int> read)
    {
        var sample = MakesSample(product) ? product.CreateInstance<Item>() : null;

        if (sample == null)
        {
            return 0;
        }

        try
        {
            return read(sample);
        }
        finally
        {
            sample.Delete();
        }
    }
}
