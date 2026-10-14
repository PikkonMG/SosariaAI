using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

/// <summary>
/// The names SosariaAI treats as facets. The six stock maps keep their constants (Felucca,
/// Trammel, Ilshenar, Malas, Tokuno, TerMur). Any other map the server registers is a facet
/// too, under its own Map.Name, so a custom shard with one map of its own name has a facet.
/// Internal is never a facet.
/// </summary>
public static class FacetNames
{
    public const string Felucca = "Felucca";
    public const string Trammel = "Trammel";
    public const string Ilshenar = "Ilshenar";
    public const string Malas = "Malas";
    public const string Tokuno = "Tokuno";
    public const string TerMur = "TerMur";

    public static readonly string[] All =
    [
        Felucca,
        Trammel,
        Ilshenar,
        Malas,
        Tokuno,
        TerMur
    ];

    /// <summary>True when the name is one of the six stock map names, in any case.</summary>
    public static bool IsStockName(string name) => TryStock(name, out _);

    /// <summary>The stock constant for a stock map name in any case; false for any other name.</summary>
    public static bool TryStock(string name, out string stock)
    {
        stock = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        for (var i = 0; i < All.Length; i++)
        {
            if (All[i].Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                stock = All[i];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The facet's own spelling: a stock name as its constant spells it, else the name of a map
    /// the server registered (<see cref="FacetMaps.TryFind"/>). False for a blank name and for a
    /// name the server has no map for. A tester's one-map shard had a map of its own name, and
    /// the six-name list skipped it at boot, so nobody spawned.
    /// </summary>
    public static bool TryCanonical(string name, out string canonical)
    {
        if (TryStock(name, out canonical))
        {
            return true;
        }

        if (FacetMaps.TryFind(name, out var map))
        {
            canonical = map.Name;
            return true;
        }

        canonical = null;
        return false;
    }

    internal static Dictionary<string, TValue> Copy<TValue>(IDictionary<string, TValue> source)
    {
        var copy = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

        if (source == null)
        {
            return copy;
        }

        foreach (var pair in source)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                copy[pair.Key] = pair.Value;
            }
        }

        return copy;
    }
}
