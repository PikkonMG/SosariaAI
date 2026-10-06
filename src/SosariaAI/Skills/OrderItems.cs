using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using SosariaAI.Economy;

namespace SosariaAI.Skills;

/// <summary>
/// What an order asks a trade to make, and the crafter's chances at it. A type named in the
/// words wins ("plate chest", "katana"); else the first item of the trade's list of the named
/// kind and piece. A full set is the six pieces of the suit, or nothing when the trade lacks one.
/// World thread only.
/// </summary>
public static class OrderItems
{
    private const char Space = ' ';
    private const double Certain = 1.0;
    private const double NoChance = 0.0;
    private const int OnePiece = 1;

    public static List<string> Resolve(CraftTrade trade, IReadOnlyList<string> words, GoodsClaim claim)
    {
        var types = new List<string>();

        if (trade?.System == null || claim.Row == null)
        {
            return types;
        }

        var craftable = Craftable(trade.System, claim.Row);

        if (claim.Piece == ArmorPiece.FullSet)
        {
            foreach (var piece in Appraisal.SuitPieces)
            {
                if (craftable.Find(type => CraftBuyers.ClaimOfType(type).Piece == piece) is { } type)
                {
                    types.Add(type.Name);
                }
            }

            return types.Count == Appraisal.SuitPieces.Count ? types : [];
        }

        var padded = $"{Space}{string.Join(Space, words ?? [])}{Space}";
        Type best = null;
        var length = 0;

        foreach (var type in craftable)
        {
            var name = Appraisal.SplitWords(type.Name);

            if (name.Length > length && padded.Contains($"{Space}{name}{Space}", StringComparison.Ordinal))
            {
                best = type;
                length = name.Length;
            }
        }

        best ??= craftable.Find(type => claim.Piece == ArmorPiece.Whole || CraftBuyers.ClaimOfType(type).Piece == claim.Piece);

        if (best != null)
        {
            types.Add(best.Name);
        }

        return types;
    }

    /// <summary>The crafter's least chance of a success and of an exceptional piece over the types; none for a type it cannot make.</summary>
    public static (double Success, double Exceptional) Chances(Mobile crafter, CraftTrade trade, IReadOnlyList<string> types)
    {
        var success = Certain;
        var exceptional = Certain;

        foreach (var name in types ?? [])
        {
            if (Find(trade, name) is not { } item)
            {
                return (NoChance, NoChance);
            }

            var chance = item.GetSuccessChance(crafter, item.Resources[0].ItemType, trade.System, false, out var allSkills);

            if (!allSkills)
            {
                return (NoChance, NoChance);
            }

            success = Math.Min(success, chance);
            exceptional = Math.Min(exceptional, item.GetExceptionalChance(trade.System, chance, crafter));
        }

        return (success, exceptional);
    }

    /// <summary>The market table's middle for exceptional pieces of every type; a set at the set share.</summary>
    public static int MarketValue(IReadOnlyList<string> types)
    {
        var total = 0;

        foreach (var name in types ?? [])
        {
            total += CraftBuyers.ShopValueOf(AssemblyHandler.FindTypeByName(name));
        }

        return (types?.Count ?? 0) > OnePiece ? total * Appraisal.FullSetSharePercent / Appraisal.PercentScale : total;
    }

    /// <summary>"GM plate chest", or "GM plate suit" for a set; empty for no types.</summary>
    public static string Noun(IReadOnlyList<string> types)
    {
        if (types is not { Count: > 0 })
        {
            return string.Empty;
        }

        var claim = CraftBuyers.ClaimOfType(AssemblyHandler.FindTypeByName(types[0])) with { Exceptional = true };
        return (types.Count > OnePiece ? claim with { Piece = ArmorPiece.FullSet } : claim).Noun;
    }

    public static CraftItem Find(CraftTrade trade, string typeName) =>
        AssemblyHandler.FindTypeByName(typeName) is { } type ? trade?.System?.CraftItems.SearchFor(type) : null;

    // The trade's items of the row an order named, in craft-list order.
    private static List<Type> Craftable(CraftSystem system, GoodsRow row)
    {
        var types = new List<Type>();

        for (var i = 0; i < system.CraftItems.Count; i++)
        {
            var item = system.CraftItems[i];

            if (CraftStationSkill.IsCraftable(item) && CraftBuyers.ClaimOfType(item.ItemType).Row == row)
            {
                types.Add(item.ItemType);
            }
        }

        return types;
    }
}
