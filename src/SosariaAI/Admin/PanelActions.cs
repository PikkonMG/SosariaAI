using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Admin;

/// <summary>
/// World work behind the staff panel: who is near, and the travel spots for a facet. Game
/// thread only. Landing the spots reads many tiles, so a facet's spots are kept while
/// <see cref="PanelRules.SpotsFresh"/> holds.
/// </summary>
public static class PanelActions
{
    /// <summary>A facet's landed towns and dungeons, and the nav graph they were landed on.</summary>
    public sealed record FacetSpots(NavGraph Graph, DateTime LandedAt, List<TravelSpot> Towns, List<TravelSpot> Dungeons);

    private static readonly Dictionary<Map, FacetSpots> SpotsByMap = new();

    /// <summary>Live characters within <see cref="PanelRules.NearRange"/> tiles of the staff member.</summary>
    public static int CountNear(Mobile staff)
    {
        var near = 0;

        foreach (var character in staff.Map.GetMobilesInRange<SosariaCharacter>(staff.Location, PanelRules.NearRange))
        {
            if (!character.Deleted && People.InWorld(character))
            {
                near++;
            }
        }

        return near;
    }

    /// <summary>
    /// The towns and the dungeons this era's world has (<see cref="WorldPlaces"/>), each landed
    /// beside its door: a town near the engine's own go location, a dungeon by the teleporter
    /// pad or cave mouth a player goes in by (<see cref="PlaceRules"/>). A grouping region such
    /// as "Misc Dungeons" is listed as the dungeons it holds. The list does not ask whether a
    /// walk reaches the door: staff must reach every one.
    /// </summary>
    public static FacetSpots TravelSpots(Map map)
    {
        var graph = NavWorld.GraphFor(map.Name);
        var now = Core.Now;

        if (SpotsByMap.TryGetValue(map, out var kept) &&
            PanelRules.SpotsFresh(ReferenceEquals(kept.Graph, graph), kept.LandedAt, now))
        {
            return kept;
        }

        var pads = GatePads(graph);
        var places = WorldPlaces.For(map);
        kept = new FacetSpots(
            graph,
            now,
            Landed(map, PanelRules.PlaceSpots(places.Towns), pads),
            Landed(map, PanelRules.PlaceSpots(places.Dungeons), pads)
        );
        SpotsByMap[map] = kept;
        return kept;
    }

    /// <summary>
    /// Each spot moved to the tile <see cref="PanelRules.Landing"/> picks, one per name and
    /// capped for the panel. A spot with no such tile in reach is left out, so a button
    /// never drops staff where they cannot walk.
    /// </summary>
    /// <param name="pads">Gate pad tiles, besides the gate items standing on the map.</param>
    public static List<TravelSpot> Landed(Map map, IEnumerable<TravelSpot> spots, IReadOnlySet<Point2D> pads)
    {
        var landed = new List<TravelSpot>();
        var walker = Standable.Walker(map);

        foreach (var spot in spots)
        {
            if (spot.At == Point3D.Zero)
            {
                continue;
            }

            var ground = map.GetAverageZ(spot.At.X, spot.At.Y);

            if (PanelRules.Landing(spot.At, ground, walker, (x, y, z) => IsStepOff(map, x, y, z, pads)) is { } at)
            {
                landed.Add(spot with { At = at });
            }
        }

        return PanelRules.Spots(landed, PanelRules.MaxSpotsPerSection);
    }

    /// <summary>The tiles of the graph's gate pads: every node a teleporter or moongate leaves from.</summary>
    public static HashSet<Point2D> GatePads(NavGraph graph)
    {
        var pads = new HashSet<Point2D>();

        if (graph == null)
        {
            return pads;
        }

        foreach (var node in graph.Nodes)
        {
            if (node.Gates is { Count: > 0 })
            {
                pads.Add(new Point2D(node.X, node.Y));
            }
        }

        return pads;
    }

    /// <summary>
    /// True for a tile a person must not be left on: a gate pad, a teleporter or moongate
    /// item, or a ladder or stair at the floor height.
    /// </summary>
    public static bool IsStepOff(Map map, int x, int y, int z, IReadOnlySet<Point2D> pads)
    {
        if (pads?.Contains(new Point2D(x, y)) == true)
        {
            return true;
        }

        foreach (var item in map.GetItemsAt(x, y))
        {
            if (item is Teleporter or PublicMoongate or Moongate ||
                !item.Movable && IsClimb(item.ItemData, item.Z, z))
            {
                return true;
            }
        }

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            if (IsClimb(TileData.ItemTable[tile.ID & TileData.MaxItemValue], tile.Z, z))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A ladder or stair piece whose top is on the floor at <paramref name="floorZ"/>.</summary>
    private static bool IsClimb(ItemData data, int tileZ, int floorZ) =>
        (data.Flags & (TileFlag.StairBack | TileFlag.StairRight)) != 0 &&
        NavMetric.SameFloor(tileZ + data.CalcHeight, floorZ);
}
