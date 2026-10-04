using System;
using System.Collections.Generic;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Picks a place the character has not seen. Pure. No world objects.
/// </summary>
public static class SightseeRules
{
    public const string KindShrine = "Shrine";
    public const string KindDungeon = "Dungeon";
    public const string KindHunt = "Hunt";
    public const string KindBank = "Bank";
    public const string KindTown = "Town";
    public const string KindHealer = "Healer";

    /// <summary>The graph the places come from is the character's own facet.</summary>
    public static bool SameFacet(NavGraph graph, string homeFacet) =>
        graph != null && string.Equals(graph.Facet, homeFacet, StringComparison.OrdinalIgnoreCase);

    /// <summary>A place is reachable when its node shares a graph component with home.</summary>
    public static bool CanRoute(NavGraph graph, string homeNode, string destNode) =>
        graph != null &&
        !string.IsNullOrWhiteSpace(homeNode) &&
        !string.IsNullOrWhiteSpace(destNode) &&
        graph.SameComponent(homeNode, destNode);

    public static string PickUnseen(
        IReadOnlyList<string> names,
        IReadOnlyList<string> seen,
        int seed,
        string prefer = null
    )
    {
        if (names == null || names.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(prefer))
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(names[i]) &&
                    names[i].Contains(prefer, StringComparison.OrdinalIgnoreCase))
                {
                    return names[i];
                }
            }
        }

        var unseen = new List<string>();

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];

            if (string.IsNullOrWhiteSpace(name) || AlreadySeen(seen, name))
            {
                continue;
            }

            unseen.Add(name);
        }

        var pool = unseen.Count > 0 ? unseen : CopyNames(names);

        if (pool.Count == 0)
        {
            return null;
        }

        var index = Math.Abs(seed) % pool.Count;
        return pool[index];
    }

    private static bool AlreadySeen(IReadOnlyList<string> seen, string name)
    {
        if (seen == null)
        {
            return false;
        }

        for (var i = 0; i < seen.Count; i++)
        {
            if (string.Equals(seen[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> CopyNames(IReadOnlyList<string> names)
    {
        var copy = new List<string>();

        for (var i = 0; i < names.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(names[i]))
            {
                copy.Add(names[i]);
            }
        }

        return copy;
    }
}
