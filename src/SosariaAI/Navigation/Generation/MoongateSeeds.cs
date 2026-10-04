using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Spawning;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Public moongate pads for one facet. Prefer live <see cref="PMList"/> entries when Map
/// is available; otherwise use the Felucca and Trammel XY from ModernUO PublicMoongate.
/// </summary>
public static class MoongateSeeds
{
    public const string NamePrefix = "moon-";

    /// <summary>The eight Virtue gates both Felucca and Trammel stand, in the server's order.</summary>
    private static readonly Point3D[] SharedGates =
    [
        WorkSites.MoonglowGate,
        WorkSites.BritainGate,
        WorkSites.JhelomGate,
        WorkSites.YewGate,
        WorkSites.MinocGate,
        WorkSites.TrinsicGate,
        WorkSites.SkaraGate,
        WorkSites.MaginciaGate
    ];

    private static readonly Point3D BuccaneersDenGate = new(2711, 2234, 0);
    private static readonly Point3D NewHavenGate = new(3450, 2677, 25);

    private static readonly Point3D[] FeluccaFallback = [.. SharedGates, BuccaneersDenGate];

    private static readonly Point3D[] TrammelFallback = [.. SharedGates, NewHavenGate];

    public static IReadOnlyList<Point3D> LocationsFor(string facet)
    {
        if (string.IsNullOrWhiteSpace(facet))
        {
            return [];
        }

        try
        {
            var live = TryLiveLocations(facet);

            if (live != null)
            {
                return live;
            }
        }
        catch
        {
            // Map may be unavailable outside a running server.
        }

        if (facet.Equals(FacetNames.Felucca, StringComparison.OrdinalIgnoreCase))
        {
            return FeluccaFallback;
        }

        if (facet.Equals(FacetNames.Trammel, StringComparison.OrdinalIgnoreCase))
        {
            return TrammelFallback;
        }

        return [];
    }

    public static void Link(IList<NavNode> nodes, string facet)
    {
        if (nodes == null || string.IsNullOrWhiteSpace(facet))
        {
            return;
        }

        var locations = LocationsFor(facet);

        if (locations.Count == 0)
        {
            return;
        }

        var moonNames = new List<string>(locations.Count);

        for (var i = 0; i < locations.Count; i++)
        {
            var name = SeedAttach.FindOrAttach(nodes, locations[i], NamePrefix);

            if (name != null)
            {
                moonNames.Add(name);
            }
        }

        SeedAttach.JoinComplete(nodes, moonNames, NavGateKind.Moongate);
    }

    private static IReadOnlyList<Point3D> TryLiveLocations(string facet)
    {
        PMList[] lists =
        [
            PMList.Felucca,
            PMList.Trammel,
            PMList.Ilshenar,
            PMList.Malas,
            PMList.Tokuno,
            PMList.TerMur
        ];

        for (var i = 0; i < lists.Length; i++)
        {
            var list = lists[i];

            if (list?.Map == null || list.Entries == null)
            {
                continue;
            }

            if (!SameMapName(list.Map, facet))
            {
                continue;
            }

            var points = new List<Point3D>(list.Entries.Length);

            for (var e = 0; e < list.Entries.Length; e++)
            {
                points.Add(list.Entries[e].Location);
            }

            return points;
        }

        return null;
    }

    private static bool SameMapName(Map map, string facet) =>
        map != null && (WorldDataSeeds.OnFacet(map.Name, facet) || WorldDataSeeds.OnFacet(map.ToString(), facet));
}

/// <summary>
/// Places a gate pad node on its exact tile, named with a caller-chosen prefix
/// (tp-, moon-). A pad is never merged with a neighbour and never snapped: the gate
/// skill moves a character only from the pad, so the node must stop it on the pad.
/// </summary>
public static class SeedAttach
{
    public static string FindOrAttach(IList<NavNode> nodes, Point3D point, string namePrefix)
    {
        if (nodes == null || string.IsNullOrWhiteSpace(namePrefix))
        {
            return null;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node == null || string.IsNullOrWhiteSpace(node.Name))
            {
                continue;
            }

            if (node.X == point.X && node.Y == point.Y)
            {
                node.ArrivalRange = NavLimits.DoorArrivalRange;
                return node.Name;
            }
        }

        var name = namePrefix + point.X + "-" + point.Y;
        nodes.Add(
            new NavNode
            {
                Name = name,
                X = point.X,
                Y = point.Y,
                Z = point.Z,
                ArrivalRange = NavLimits.DoorArrivalRange
            }
        );

        return name;
    }

    public static void JoinComplete(IList<NavNode> nodes, List<string> names, NavGateKind kind)
    {
        if (nodes == null || names == null || names.Count < 2)
        {
            return;
        }

        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (node == null || string.IsNullOrWhiteSpace(node.Name))
            {
                continue;
            }

            byName[node.Name] = node;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (!byName.TryGetValue(names[i], out var left))
            {
                continue;
            }

            for (var j = i + 1; j < names.Count; j++)
            {
                if (!byName.TryGetValue(names[j], out var right))
                {
                    continue;
                }

                NavGates.Add(left, right, kind);
            }
        }
    }
}
