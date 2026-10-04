using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// Finds the way out of a building by following its floor, tile by tile, until the
/// character stands on open ground.
///
/// A straight line to the nearest street is not enough. The Cat's Lair in Britain has a
/// raised floor at height 20 walled on the north and west, while the bank and the forest
/// lie that way. Its only way down is a ramp on the east side, reached by walking away
/// from both. A walk over the floor finds that ramp; a straight line never does.
/// </summary>
public static class BuildingExit
{
    /// <summary>Tiles searched before giving up. A large inn holds well under this.</summary>
    public const int MaxTiles = 4000;

    /// <summary>Tiles around an exit that must also be free of roof.</summary>
    public const int RoofClearance = 1;

    /// <summary>Tiles between waypoints handed to the walker.</summary>
    public const int WaypointSpacing = 6;

    /// <summary>
    /// Returns the waypoints from <paramref name="from"/> to the first open ground tile,
    /// ending on that tile. Returns an empty list when the character is already outside
    /// or no way out is found.
    /// </summary>
    /// <param name="walker">
    /// How the walker stands and steps; <see cref="Standable.Walker"/> on a live map. A
    /// step lands on the floor nearest the height it leaves, so an upper storey is not
    /// mistaken for the ground floor beneath it.
    /// </param>
    /// <param name="isIndoor">True when a roof covers that tile at that height.</param>
    /// <param name="reachesStreet">
    /// True when the route planner can start from this tile. A roofless courtyard still
    /// walled in is open ground, but it is not yet out.
    /// </param>
    public static IReadOnlyList<Point3D> Find(
        Point3D from,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        Func<int, int, int, bool> reachesStreet
    )
    {
        if (walker == null || isIndoor == null || reachesStreet == null)
        {
            return [];
        }

        if (IsOut(from, isIndoor, reachesStreet))
        {
            return [];
        }

        // A spot is a tile at a height: the floor above a room is a different spot from
        // the room, so a walk down the stairs can still enter the room below.
        var seen = new HashSet<Point3D> { from };
        var cameFrom = new Dictionary<Point3D, Point3D>();
        var queue = new Queue<Point3D>();
        queue.Enqueue(from);

        while (queue.Count > 0 && seen.Count < MaxTiles)
        {
            var here = queue.Dequeue();

            if (IsOut(here, isIndoor, reachesStreet))
            {
                return Waypoints(here, cameFrom);
            }

            foreach (var (dx, dy) in TileGrid.Neighbours)
            {
                if (!walker.SafeStep(here.X, here.Y, here.Z, here.X + dx, here.Y + dy, out var z))
                {
                    continue;
                }

                var next = new Point3D(here.X + dx, here.Y + dy, z);

                if (seen.Add(next))
                {
                    cameFrom[next] = here;
                    queue.Enqueue(next);
                }
            }
        }

        return [];
    }

    private static bool IsOut(
        Point3D at,
        Func<int, int, int, bool> isIndoor,
        Func<int, int, int, bool> reachesStreet
    ) =>
        IsOpenGround(at.X, at.Y, at.Z, isIndoor) && reachesStreet(at.X, at.Y, at.Z);

    /// <summary>
    /// Open ground has no roof over it or over any tile beside it. A tile at the edge of
    /// a room often has no roof piece of its own, sitting under the eaves, yet the walls
    /// still close it in. A roofless deck above the street is open ground too: whether
    /// the planner can start from it is <c>reachesStreet</c>'s question.
    /// </summary>
    private static bool IsOpenGround(int x, int y, int z, Func<int, int, int, bool> isIndoor)
    {
        for (var dx = -RoofClearance; dx <= RoofClearance; dx++)
        {
            for (var dy = -RoofClearance; dy <= RoofClearance; dy++)
            {
                if (isIndoor(x + dx, y + dy, z))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IReadOnlyList<Point3D> Waypoints(Point3D exit, Dictionary<Point3D, Point3D> cameFrom)
    {
        var trail = new List<Point3D>();

        for (var at = exit; cameFrom.TryGetValue(at, out var previous); at = previous)
        {
            trail.Add(at);
        }

        trail.Reverse();
        var points = new List<Point3D>();

        for (var i = 0; i < trail.Count; i++)
        {
            if (i == trail.Count - 1 || (i + 1) % WaypointSpacing == 0)
            {
                points.Add(trail[i]);
            }
        }

        return points;
    }
}
