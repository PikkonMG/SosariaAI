using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

/// <summary>
/// ModernUO map names that SosariaAI treats as facets. These strings match Map.Name
/// (Felucca, Trammel, Ilshenar, Malas, Tokuno, TerMur). Internal is not a facet.
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

    public static bool TryCanonical(string name, out string canonical)
    {
        canonical = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();

        for (var i = 0; i < All.Length; i++)
        {
            if (All[i].Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                canonical = All[i];
                return true;
            }
        }

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
