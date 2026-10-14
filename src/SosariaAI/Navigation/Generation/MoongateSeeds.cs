using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Spawning;

namespace SosariaAI.Navigation.Generation;

/// <summary>One public moongate pad, with the engine's name number for its gate (0 when it has none).</summary>
public readonly record struct PublicGate(Point3D Location, int Number);

/// <summary>
/// Public moongate pads for one facet. A stock map takes the engine's <see cref="PMList"/>
/// entries, else the Felucca and Trammel pads of ModernUO's PublicMoongate. The engine builds
/// every list at once and reads Map.Trammel to do it, so on a shard without Trammel the first
/// touch throws; it is read here and nowhere else, once, and a list it cannot build is none.
/// A map the stock tables do not fit takes only the public moongate items standing on it:
/// Britannia's pads there would be gates in the sea.
/// </summary>
public static class MoongateSeeds
{
    public const string NamePrefix = "moon-";

    /// <summary>A pad the engine's lists do not name.</summary>
    public const int NoNumber = 0;

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

    private static readonly object GatesLock = new();
    private static readonly Dictionary<string, IReadOnlyList<PublicGate>> GatesByFacet = new(StringComparer.OrdinalIgnoreCase);
    private static IReadOnlyList<PMList> _engineLists;

    /// <summary>
    /// The pads of this facet. A custom map's pads are read from the world's items once, until
    /// <see cref="ForgetGates"/>: the scan walks every item.
    /// </summary>
    public static IReadOnlyList<PublicGate> GatesFor(string facet)
    {
        if (string.IsNullOrWhiteSpace(facet))
        {
            return [];
        }

        if (FacetMaps.IsStock(facet))
        {
            return StockGates(facet);
        }

        lock (GatesLock)
        {
            if (!GatesByFacet.TryGetValue(facet, out var gates))
            {
                gates = WorldGates(facet);
                GatesByFacet[facet] = gates;
            }

            return gates;
        }
    }

    public static IReadOnlyList<Point3D> LocationsFor(string facet)
    {
        var gates = GatesFor(facet);
        var locations = new Point3D[gates.Count];

        for (var i = 0; i < gates.Count; i++)
        {
            locations[i] = gates[i].Location;
        }

        return locations;
    }

    /// <summary>
    /// The engine's pads of a stock facet, with their name numbers; empty when the engine has no
    /// list for it or cannot build its lists.
    /// </summary>
    public static IReadOnlyList<PublicGate> EngineGates(string facet)
    {
        var lists = EngineLists();

        for (var i = 0; i < lists.Count; i++)
        {
            var list = lists[i];

            if (list?.Map == null || list.Entries == null || !SameMapName(list.Map, facet))
            {
                continue;
            }

            var gates = new PublicGate[list.Entries.Length];

            for (var e = 0; e < list.Entries.Length; e++)
            {
                gates[e] = new PublicGate(list.Entries[e].Location, list.Entries[e].Number);
            }

            return gates;
        }

        return [];
    }

    /// <summary>
    /// Drops the custom maps' pads read so far. The nav load calls it before each build or check:
    /// First Time Setup places a custom map's gates after the boot read none.
    /// </summary>
    public static void ForgetGates()
    {
        lock (GatesLock)
        {
            GatesByFacet.Clear();
        }
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

    private static IReadOnlyList<PublicGate> StockGates(string facet)
    {
        var engine = EngineGates(facet);

        if (engine.Count > 0)
        {
            return engine;
        }

        if (facet.Equals(FacetNames.Felucca, StringComparison.OrdinalIgnoreCase))
        {
            return Unnumbered(FeluccaFallback);
        }

        if (facet.Equals(FacetNames.Trammel, StringComparison.OrdinalIgnoreCase))
        {
            return Unnumbered(TrammelFallback);
        }

        return [];
    }

    private static PublicGate[] Unnumbered(Point3D[] pads)
    {
        var gates = new PublicGate[pads.Length];

        for (var i = 0; i < pads.Length; i++)
        {
            gates[i] = new PublicGate(pads[i], NoNumber);
        }

        return gates;
    }

    // The public moongate items standing on the facet's map.
    private static IReadOnlyList<PublicGate> WorldGates(string facet)
    {
        if (!FacetMaps.TryFind(facet, out var map))
        {
            return [];
        }

        var gates = new List<PublicGate>();

        foreach (var item in World.Items.Values)
        {
            if (item is PublicMoongate { Deleted: false } gate && gate.Map == map)
            {
                gates.Add(new PublicGate(gate.Location, NoNumber));
            }
        }

        return gates;
    }

    private static IReadOnlyList<PMList> EngineLists()
    {
        lock (GatesLock)
        {
            return _engineLists ??= ReadEngineLists();
        }
    }

    private static PMList[] ReadEngineLists()
    {
        try
        {
            return
            [
                PMList.Felucca,
                PMList.Trammel,
                PMList.Ilshenar,
                PMList.Malas,
                PMList.Tokuno,
                PMList.TerMur
            ];
        }
        catch (TypeInitializationException)
        {
            // The lists read a map this server does not have.
            return [];
        }
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
