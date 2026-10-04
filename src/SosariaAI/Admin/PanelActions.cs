using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Admin;

/// <summary>
/// World work behind the staff panel: who is near, and the travel spots for a facet. Game
/// thread only.
/// </summary>
public static class PanelActions
{
    public static List<SosariaCharacter> LiveNear(Mobile staff)
    {
        var near = new List<SosariaCharacter>();

        foreach (var character in FleetStatus.InWorld())
        {
            if (character.Map == staff.Map &&
                NavMetric.Chebyshev(character.Location, staff.Location) <= PanelRules.NearRange)
            {
                near.Add(character);
            }
        }

        return near;
    }

    /// <summary>
    /// The towns this era's world has (<see cref="WorldPlaces"/>), landed near the engine's
    /// own go location.
    /// </summary>
    public static List<TravelSpot> TownSpots(Map map) => LandedPlaces(map, WorldPlaces.For(map).Towns);

    /// <summary>
    /// The dungeons this era's world has, by their real names, each landed beside its real
    /// door: the teleporter pad or cave mouth a player goes in by (<see cref="PlaceRules"/>).
    /// A grouping region such as "Misc Dungeons" is listed as the dungeons it holds. The list
    /// does not ask whether a walk reaches the door: staff must reach every one.
    /// </summary>
    public static List<TravelSpot> DungeonSpots(Map map) => LandedPlaces(map, WorldPlaces.For(map).Dungeons);

    private static List<TravelSpot> LandedPlaces(Map map, IEnumerable<Place> places) =>
        Landed(map, PanelRules.PlaceSpots(places), GatePads(NavWorld.GraphFor(map.Name)));

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
