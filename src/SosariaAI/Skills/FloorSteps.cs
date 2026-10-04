using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using Moves = Server.Movement.Movement;

namespace SosariaAI.Skills;

/// <summary>
/// A short step search for a walker that the engine pathfinder does not move: beside its
/// goal but on another floor, or stuck on a leg the engine path will not take. Each step
/// lands at the height the walker's step gives it, so the search follows a stair or a
/// ramp the way the walker's feet do. The walker's step opens doors, as the character's
/// own step does; the engine path treats a shut door as a wall. Pure: the step is passed in.
/// </summary>
public static class FloorSteps
{
    /// <summary>Most spots searched: the stair beside a goal lies within a room or two.</summary>
    public const int MaxSpots = 600;

    /// <summary>Longest step list: past this the stair is a route for the planner, not a step search.</summary>
    public const int MaxSteps = 24;

    /// <summary>
    /// Most spots a stuck leg searches. A leg is at most one pathfinder box long, and a
    /// door or a wall end beside it adds a room's width.
    /// </summary>
    public const int MaxAroundSpots = 4000;

    /// <summary>Longest step list for a stuck leg: a whole leg plus the detour through a door.</summary>
    public const int MaxAroundSteps = NavLimits.MaxLegDistance * 2;

    private const int DirectionCount = 8;

    /// <summary>
    /// Directions from <paramref name="from"/> to a spot in reach of <paramref name="goal"/>
    /// on its floor, stepping with <paramref name="step"/>; <see cref="Standable.Walker"/>
    /// gives the engine's own. Empty when already there; null when no such spot is near.
    /// </summary>
    public static IReadOnlyList<Direction> Find(Point3D from, IPoint3D goal, int range, TileStep step) =>
        Search(from, goal, range, step, MaxSpots, MaxSteps);

    /// <summary>
    /// The same search with room for a whole leg, for a walker the engine path left standing:
    /// a shut door the engine will not plan through, or a floor its search boxes miss.
    /// </summary>
    public static IReadOnlyList<Direction> Around(Point3D from, IPoint3D goal, int range, TileStep step) =>
        Search(from, goal, range, step, MaxAroundSpots, MaxAroundSteps);

    /// <summary>A* in steps: a step costs one and the heuristic is the tiles left, so the list is a shortest one.</summary>
    private static IReadOnlyList<Direction> Search(
        Point3D from,
        IPoint3D goal,
        int range,
        TileStep step,
        int maxSpots,
        int maxSteps
    )
    {
        if (step == null || goal == null)
        {
            return null;
        }

        if (WalkArrival.Arrived(from, goal, range, checkFloor: true))
        {
            return [];
        }

        var target = new Point3D(goal.X, goal.Y, goal.Z);
        var cameFrom = new Dictionary<Point3D, (Point3D Prev, Direction Direction)>();
        var depth = new Dictionary<Point3D, int> { [from] = 0 };
        var open = new PriorityQueue<Point3D, int>();
        open.Enqueue(from, TilesLeft(from, target, range));

        while (open.TryDequeue(out var at, out var priority) && depth.Count < maxSpots)
        {
            var steps = depth[at];

            if (priority != steps + TilesLeft(at, target, range))
            {
                continue;
            }

            if (WalkArrival.Arrived(at, goal, range, checkFloor: true))
            {
                return Unwind(cameFrom, from, at);
            }

            if (steps >= maxSteps)
            {
                continue;
            }

            for (var d = 0; d < DirectionCount; d++)
            {
                var direction = (Direction)d;
                var x = at.X;
                var y = at.Y;
                Moves.Offset(direction, ref x, ref y);

                if (!step(at.X, at.Y, at.Z, x, y, out var z))
                {
                    continue;
                }

                var next = new Point3D(x, y, z);

                if (depth.TryGetValue(next, out var known) && known <= steps + 1)
                {
                    continue;
                }

                depth[next] = steps + 1;
                cameFrom[next] = (at, direction);
                open.Enqueue(next, steps + 1 + TilesLeft(next, target, range));
            }
        }

        return null;
    }

    /// <summary>Steps still needed on open ground to come within range: never more than the walk.</summary>
    private static int TilesLeft(Point3D at, Point3D goal, int range) =>
        Math.Max(0, NavMetric.Chebyshev(at, goal) - range);

    private static List<Direction> Unwind(
        Dictionary<Point3D, (Point3D Prev, Direction Direction)> cameFrom,
        Point3D from,
        Point3D to
    )
    {
        var directions = new List<Direction>();

        for (var at = to; at != from; at = cameFrom[at].Prev)
        {
            directions.Add(cameFrom[at].Direction);
        }

        directions.Reverse();
        return directions;
    }
}
