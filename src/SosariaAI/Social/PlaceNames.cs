using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Social;

/// <summary>
/// Names a spot the way people tell of it (see <see cref="PlaceNameRules"/>): the journal,
/// the red scream, gossip, party calls, long-term memory and the "where are you" answer all
/// use it. A spot by a public moongate goes by the gate ("the Moonglow gate"). A region
/// goes by the name this era's world gives it (<see cref="WorldPlaces"/>): a dungeon inside
/// a grouping region by its own name, and a place the world does not have by nothing, so
/// the spot is named by a landmark or a town instead. The landmarks and towns of a facet are
/// read once per destination catalog.
/// </summary>
public static class PlaceNames
{
    private sealed record Mark(Point3D At, string Name);

    private sealed class Marks
    {
        public readonly List<Mark> Landmarks = [];
        public readonly List<Mark> Towns = [];
    }

    private static readonly ConditionalWeakTable<DestinationCatalog, Marks> MarksByCatalog = new();

    /// <summary>Where this mobile stands, as people name it.</summary>
    public static string Of(Mobile mobile) =>
        People.InWorld(mobile) ? Of(mobile.Location, mobile.Map, mobile.Region) : PlaceNameRules.Wild;

    /// <summary>
    /// The named region the mobile stands in (a town, a dungeon, the Den), else the wild. Cheap:
    /// no landmark search, for a key asked on every scan.
    /// </summary>
    public static string RegionOf(Mobile mobile) =>
        People.InWorld(mobile)
            ? NamedRegion(mobile.Region, mobile.Map, mobile.Location) ?? PlaceNameRules.Wild
            : PlaceNameRules.Wild;

    /// <summary>This spot, as people name it.</summary>
    public static string Of(Point3D at, Map map) =>
        map == null || map == Map.Internal ? PlaceNameRules.Wild : Of(at, map, Region.Find(at, map));

    // A moongate or a named region settles it without the landmark search.
    private static string Of(Point3D at, Map map, Region region)
    {
        var gate = GateNear(at, map);

        if (gate != null)
        {
            return gate;
        }

        var regionName = NamedRegion(region, map, at);

        if (regionName != null)
        {
            return PlaceNameRules.Spoken(regionName, map.Name, null, int.MaxValue, null, int.MaxValue);
        }

        var marks = MarksOf(NavWorld.DestinationsFor(map.Name), map);
        var landmark = Nearest(marks?.Landmarks, at, out var landmarkDistance);
        var town = Nearest(marks?.Towns, at, out var townDistance);
        return PlaceNameRules.Spoken(null, map.Name, landmark, landmarkDistance, town, townDistance);
    }

    /// <summary>
    /// The name of the first region up the chain that has a name of its own and belongs in
    /// this era's world, as that world names the spot.
    /// </summary>
    private static string NamedRegion(Region region, Map map, Point3D at)
    {
        var places = WorldPlaces.For(map);

        for (; region != null; region = region.Parent)
        {
            if (PlaceNameRules.IsNamedRegion(region.Name, map.Name) && places.Spoken(region.Name, at) is { } spoken)
            {
                return spoken;
            }
        }

        return null;
    }

    // The public moongate in range, by the town it serves, or null.
    private static string GateNear(Point3D at, Map map)
    {
        foreach (var gate in map.GetItemsInRange<PublicMoongate>(at, PlaceNameRules.GateRange))
        {
            return PlaceNameRules.GateName(GateTown(gate.Location, map));
        }

        return null;
    }

    // The town the engine's moongate list gives the pad, else the named region it stands in.
    private static string GateTown(Point3D pad, Map map)
    {
        var lists = PMList.SALists;

        for (var i = 0; i < lists.Length; i++)
        {
            if (lists[i].Map != map)
            {
                continue;
            }

            var entries = lists[i].Entries;

            for (var e = 0; e < entries.Length; e++)
            {
                if (entries[e].Location.X == pad.X && entries[e].Location.Y == pad.Y &&
                    MoongateGuards.TownOf(entries[e].Number) is { } town)
                {
                    return town;
                }
            }
        }

        return NamedRegion(Region.Find(pad, map), map, pad);
    }

    private static string Nearest(List<Mark> marks, Point3D at, out int distance)
    {
        distance = int.MaxValue;
        string name = null;

        for (var i = 0; marks != null && i < marks.Count; i++)
        {
            var tiles = NavMetric.Chebyshev(at, marks[i].At);

            if (tiles < distance)
            {
                distance = tiles;
                name = marks[i].Name;
            }
        }

        return name;
    }

    private static Marks MarksOf(DestinationCatalog catalog, Map map) =>
        catalog == null ? null : MarksByCatalog.GetValue(catalog, source => Read(source, map));

    // Dungeon mouths go by their dungeon, shrines and hunting grounds by a sayable name, and a
    // bank stands for the town it is in.
    private static Marks Read(DestinationCatalog catalog, Map map)
    {
        var marks = new Marks();

        for (var i = 0; i < catalog.All.Count; i++)
        {
            var destination = catalog.All[i];

            switch (destination.ParsedKind)
            {
                case DestinationKind.Bank:
                {
                    var town = NamedRegion(Region.Find(destination.Arrival, map), map, destination.Arrival);

                    if (town != null)
                    {
                        marks.Towns.Add(new Mark(destination.Arrival, town));
                    }

                    break;
                }
                case DestinationKind.Dungeon:
                    AddLandmark(marks, destination.Arrival, PlaceNameRules.IsSayableMarker(destination.Role) ? destination.Role : destination.Name);
                    break;
                case DestinationKind.Shrine:
                case DestinationKind.Hunt:
                    AddLandmark(marks, destination.Arrival, destination.Name);
                    break;
            }
        }

        return marks;
    }

    private static void AddLandmark(Marks marks, Point3D at, string name)
    {
        if (PlaceNameRules.IsSayableMarker(name))
        {
            marks.Landmarks.Add(new Mark(at, name.Trim()));
        }
    }
}
