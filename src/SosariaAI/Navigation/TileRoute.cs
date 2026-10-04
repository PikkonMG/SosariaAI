using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// A* over real map tiles, one tile per step, with the walker's own step rule. The
/// height is carried from step to step, so a stair leads to the floor above and a raised
/// floor steps down only where the walker can. Each waypoint carries the height the
/// search reached there, which puts the game pathfinder on the right floor. Doors count
/// as passable because characters open them. A tile an armed trap hurts costs a long
/// detour (<see cref="TrapRules.DetourTiles"/>), so the route goes round a trap and crosses
/// one only where a corridor is trapped wall to wall. A search on the world thread, as a
/// walker's, earns few cells (<see cref="WorldCellBudget"/>); the nav build, which runs once,
/// earns many (<see cref="CellBudget"/>).
/// </summary>
public static class TileRoute
{
    public const int MaxCells = 80_000;
    public const int WaypointSpacing = 12;

    /// <summary>Tiles searched around a goal no one can stand on for the nearest one that stands.</summary>
    public const int SnapRadius = 8;

    /// <summary>
    /// Longest trip this router takes. Anything further is graph travel, so an unreachable
    /// goal across the map cannot burn the whole cell budget on the game thread.
    /// </summary>
    public const int MaxTripTiles = 260;

    /// <summary>
    /// Tiles the nav build searches per tile of straight-line distance. A short trip that
    /// cannot be reached gives up after a few thousand tiles instead of eighty thousand.
    /// </summary>
    public const int CellsPerTile = 1_000;

    /// <summary>
    /// Tiles a world-thread search earns per tile of straight-line distance. A cell costs
    /// about eight microseconds on the live map: 80,000 cells took 0.65 s, a hitch a player
    /// feels, and a trip that could not be reached paid all of them. An open walk needs a
    /// few cells per tile; a trip whose tile walk runs out still has the road graph.
    /// </summary>
    public const int WorldCellsPerTile = 40;

    /// <summary>
    /// The fewest cells a world-thread search earns: a walk round a building to a node one
    /// leg away, or back onto the road. About three hundredths of a second.
    /// </summary>
    public const int WorldMinCells = 4_000;

    /// <summary>The most cells a world-thread search earns, for the longest tile trip: about a tenth of a second.</summary>
    public const int WorldMaxCells = 12_000;

    /// <summary>
    /// How close to the start or the goal a route may run under a roof. A character
    /// standing in a shop must walk out, and one going to a shop must walk in.
    /// </summary>
    public const int IndoorAllowanceTiles = 8;

    /// <summary>
    /// The most cells a search spends to find the building its goal stands in. A walk to a
    /// counter deep in a shop may cross that whole building, as a walk out of one may: the
    /// healer of Buccaneer's Den stands further from the door than the indoor allowance, and
    /// no route reached it. The cells count against the search's own budget.
    /// </summary>
    public const int GoalBuildingCells = 1_500;

    /// <summary>Cost of a straight step. A diagonal costs more, so a straight street is not walked as a zigzag.</summary>
    private const int StraightCost = 10;

    private const int DiagonalCost = 14;

    private const string NoWalkerWhy = "no walker";
    private const string NoGoalWhy = "no goal";

    /// <summary>Extra cost of stepping onto a tile an armed trap hurts: a detour of <see cref="TrapRules.DetourTiles"/> tiles.</summary>
    private const int TrappedStepCost = StraightCost * TrapRules.DetourTiles;

    /// <param name="walker">How the walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    /// <param name="isIndoor">True when a roof covers a tile at a height.</param>
    /// <param name="arrivalRange">Tiles short of <paramref name="to"/> that still count as arrival.</param>
    public static IReadOnlyList<Point3D> Find(
        Point3D from,
        Point3D to,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor = null,
        int arrivalRange = 0
    ) => Find(from, to, walker, isIndoor, arrivalRange, out _);

    /// <param name="reason">Why no route came back, for the route-explain staff command.</param>
    public static IReadOnlyList<Point3D> Find(
        Point3D from,
        Point3D to,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        int arrivalRange,
        out string reason
    ) => Find(from, to, walker, isIndoor, arrivalRange, WorldCellBudget, out reason, out _);

    /// <summary>
    /// A world-thread walk from <paramref name="from"/> to <paramref name="to"/> paid from
    /// <paramref name="allowance"/>, the cells a loop of proofs shares: each search gets what
    /// its trip earns, or what is left. Empty, with no search, once the allowance is spent.
    /// </summary>
    public static IReadOnlyList<Point3D> Find(
        Point3D from,
        Point3D to,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        CellAllowance allowance
    )
    {
        if (allowance == null || allowance.Spent)
        {
            return [];
        }

        var path = Find(
            from,
            to,
            walker,
            isIndoor,
            arrivalRange: 0,
            distance => allowance.Grant(WorldCellBudget(distance)),
            out _,
            out var visited
        );
        allowance.Spend(visited);
        return path;
    }

    /// <summary>
    /// <see cref="Find(Point3D, Point3D, TileWalker, Func{int, int, int, bool}, int)"/> for the nav
    /// build, which runs once and may search <see cref="CellBudget"/> cells: a road it misses is
    /// missing for every walker until the next build.
    /// </summary>
    public static IReadOnlyList<Point3D> FindForBuild(
        Point3D from,
        Point3D to,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    ) => Find(from, to, walker, isIndoor, arrivalRange: 0, CellBudget, out _, out _);

    private static IReadOnlyList<Point3D> Find(
        Point3D from,
        Point3D to,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        int arrivalRange,
        Func<int, int> budget,
        out string reason,
        out int visited
    )
    {
        visited = 0;

        if (!TryPlan(from, to, walker, arrivalRange, out var start, out var goal, out var range, out reason))
        {
            return [];
        }

        // A goal in range on the floor above or below is not arrived at: the search
        // below climbs to it.
        if (NavMetric.Chebyshev(from, to) <= arrivalRange && NavMetric.SameFloor(start, goal))
        {
            return [to];
        }

        var trail = Search(start, goal, range, budget(NavMetric.Chebyshev(start, goal)), walker, isIndoor, out reason, out visited);
        return trail == null ? [] : Waypoints(start, trail, walker);
    }

    /// <summary>
    /// The walk from <paramref name="from"/> to whichever of <paramref name="goals"/> it reaches
    /// first, onto the goal's own tile and floor, within the cells the nearest goal's distance
    /// earns on the world thread (<see cref="WorldCellBudget"/>): a walker off the road must
    /// not freeze the shard while it looks for one. Each goal is a spot someone stands on,
    /// such as a node. Empty with the reason when none is reached within the cells.
    /// </summary>
    public static IReadOnlyList<Point3D> FindAny(
        Point3D from,
        IReadOnlyList<Point3D> goals,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        out string reason
    )
    {
        if (walker == null || goals is not { Count: > 0 })
        {
            reason = walker == null ? NoWalkerWhy : NoGoalWhy;
            return [];
        }

        var start = OnFloor(from, walker);
        var trail = Search(start, goals, range: 0, WorldCellBudget(NavMetric.NearestOf(start, goals)), walker, isIndoor, out reason, out _);
        return trail == null ? [] : Waypoints(start, trail, walker);
    }

    /// <summary>
    /// Every tile of the walk from <paramref name="from"/> to <paramref name="to"/>, one step
    /// apart, without the start tile; a walk of no steps is the goal tile alone. The world
    /// generator lays roads along it where its coarse spread cannot follow a twisting town
    /// gate. Empty when there is no walk. Build only: it searches <see cref="CellBudget"/> cells.
    /// </summary>
    public static IReadOnlyList<Point3D> Trail(
        Point3D from,
        Point3D to,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    ) =>
        TryPlan(from, to, walker, arrivalRange: 0, out var start, out var goal, out var range, out _)
            ? Search(start, goal, range, CellBudget(NavMetric.Chebyshev(start, goal)), walker, isIndoor, out _, out _) ?? []
            : [];

    /// <summary>The start on the walker's floor and the goal the search aims at, or the reason there is none.</summary>
    private static bool TryPlan(
        Point3D from,
        Point3D to,
        TileWalker walker,
        int arrivalRange,
        out Point3D start,
        out Point3D goal,
        out int range,
        out string reason
    )
    {
        start = from;
        goal = to;
        range = arrivalRange;
        reason = null;

        if (walker == null)
        {
            reason = NoWalkerWhy;
            return false;
        }

        var distance = NavMetric.Chebyshev(from, to);

        if (distance > MaxTripTiles)
        {
            reason = $"trip of {distance} tiles is over the {MaxTripTiles} tile limit";
            return false;
        }

        if (!TryGoal(to, walker, arrivalRange, out goal, out range))
        {
            reason = $"no standable tile within {SnapRadius} tiles of the goal";
            return false;
        }

        start = OnFloor(from, walker);
        return true;
    }

    /// <summary>The spot on the floor the walker stands on there, or the spot itself when it finds none.</summary>
    private static Point3D OnFloor(Point3D at, TileWalker walker) =>
        new(at.X, at.Y, walker.FloorNear(at.X, at.Y, at.Z) ?? at.Z);

    /// <summary>A* to one goal over at most <paramref name="budget"/> cells.</summary>
    private static List<Point3D> Search(
        Point3D start,
        Point3D goal,
        int range,
        int budget,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        out string reason,
        out int visited
    ) =>
        Search(start, [goal], range, budget, walker, isIndoor, out reason, out visited);

    /// <summary>
    /// A* from the start to a tile within <paramref name="range"/> of any of the goals on its
    /// floor, over at most <paramref name="budget"/> cells. The tiles walked, start excluded,
    /// or null with the reason.
    /// </summary>
    private static List<Point3D> Search(
        Point3D start,
        IReadOnlyList<Point3D> goals,
        int range,
        int budget,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        out string reason,
        out int visited
    )
    {
        reason = null;
        var open = new PriorityQueue<Point3D, int>();
        var cameFrom = new Dictionary<Point3D, Point3D>();
        var cost = new Dictionary<Point3D, int> { [start] = 0 };
        // The way out of the building the walk starts in, and the way into the building its
        // goal stands in, are never a shortcut through a house. A large inn has its door
        // further from the bed than the indoor allowance.
        var startBuilding = new HashSet<Point3D>();

        if (isIndoor?.Invoke(start.X, start.Y, start.Z) == true)
        {
            startBuilding.Add(start);
        }

        var goalBuilding = GoalBuilding(goals, walker, isIndoor, Math.Min(GoalBuildingCells, budget), out visited);
        open.Enqueue(start, Heuristic(start, goals));

        while (open.TryDequeue(out var current, out var priority))
        {
            var currentCost = cost[current];

            // A spot queued again at a lower cost leaves its older entry behind.
            if (priority != currentCost + Heuristic(current, goals))
            {
                continue;
            }

            if (Arrived(current, goals, range))
            {
                return Unwind(current, cameFrom);
            }

            if (visited >= budget)
            {
                reason = $"cell budget of {budget} used up";
                return null;
            }

            visited++;

            foreach (var (dx, dy) in TileGrid.Neighbours)
            {
                if (!walker.Step(current.X, current.Y, current.Z, current.X + dx, current.Y + dy, out var z))
                {
                    continue;
                }

                var next = new Point3D(current.X + dx, current.Y + dy, z);
                var nextIndoor = isIndoor?.Invoke(next.X, next.Y, next.Z) == true;
                var stillInStartBuilding = nextIndoor && startBuilding.Contains(current);

                if (nextIndoor &&
                    !stillInStartBuilding &&
                    !goalBuilding.Contains(next) &&
                    NavMetric.Chebyshev(next, start) > IndoorAllowanceTiles &&
                    NavMetric.NearestOf(next, goals) > IndoorAllowanceTiles)
                {
                    continue;
                }

                var tent = currentCost + (dx != 0 && dy != 0 ? DiagonalCost : StraightCost) +
                           (walker.IsHarmed(next.X, next.Y, next.Z) ? TrappedStepCost : 0);

                if (cost.TryGetValue(next, out var old) && tent >= old)
                {
                    continue;
                }

                cameFrom[next] = current;
                cost[next] = tent;

                if (stillInStartBuilding)
                {
                    startBuilding.Add(next);
                }

                open.Enqueue(next, tent + Heuristic(next, goals));
            }
        }

        reason = $"closed in after {visited} cells: no walkable way out of the start area";
        return null;
    }

    /// <summary>
    /// The roofed tiles a walker steps across from any goal under a roof without stepping out
    /// from under it: the building the goal stands in. At most <paramref name="cells"/> tiles,
    /// counted in <paramref name="visited"/>. Empty when no goal is under a roof.
    /// </summary>
    private static HashSet<Point3D> GoalBuilding(
        IReadOnlyList<Point3D> goals,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        int cells,
        out int visited
    )
    {
        var building = new HashSet<Point3D>();
        var frontier = new Queue<Point3D>();
        visited = 0;

        if (isIndoor == null)
        {
            return building;
        }

        for (var i = 0; i < goals.Count; i++)
        {
            if (isIndoor(goals[i].X, goals[i].Y, goals[i].Z) && building.Add(goals[i]))
            {
                frontier.Enqueue(goals[i]);
            }
        }

        while (visited < cells && frontier.TryDequeue(out var current))
        {
            visited++;

            foreach (var (dx, dy) in TileGrid.Neighbours)
            {
                if (walker.Step(current.X, current.Y, current.Z, current.X + dx, current.Y + dy, out var z) &&
                    isIndoor(current.X + dx, current.Y + dy, z))
                {
                    var next = new Point3D(current.X + dx, current.Y + dy, z);

                    if (building.Add(next))
                    {
                        frontier.Enqueue(next);
                    }
                }
            }
        }

        return building;
    }

    /// <summary>The tiles from the start to <paramref name="end"/>, start excluded; the end alone when the start is the end.</summary>
    private static List<Point3D> Unwind(Point3D end, Dictionary<Point3D, Point3D> cameFrom)
    {
        var trail = new List<Point3D>();

        for (var at = end; cameFrom.TryGetValue(at, out var previous); at = previous)
        {
            trail.Add(at);
        }

        trail.Reverse();

        if (trail.Count == 0)
        {
            trail.Add(end);
        }

        return trail;
    }

    /// <summary>
    /// Cells several world-thread searches share, so a loop of proofs stays within one bound
    /// however many it tries (<see cref="Find(Point3D, Point3D, TileWalker, Func{int, int, int, bool}, CellAllowance)"/>).
    /// </summary>
    public sealed class CellAllowance(int cells)
    {
        public int Remaining { get; private set; } = cells;

        public bool Spent => Remaining <= 0;

        internal int Grant(int wanted) => Math.Min(wanted, Remaining);

        internal void Spend(int cells) => Remaining = Math.Max(0, Remaining - cells);
    }

    /// <summary>The cells the nav build searches for a trip <paramref name="distance"/> tiles long.</summary>
    public static int CellBudget(int distance) =>
        Math.Clamp(distance * CellsPerTile, CellsPerTile, MaxCells);

    /// <summary>The cells a world-thread search earns for a trip <paramref name="distance"/> tiles long.</summary>
    public static int WorldCellBudget(int distance) =>
        Math.Clamp(distance * WorldCellsPerTile, WorldMinCells, WorldMaxCells);

    /// <summary>
    /// The spot the search aims at, on the goal's floor. A goal no one can stand on, such
    /// as a tile inside a wall, gives way to the nearest tile that stands, unless a tile
    /// within the arrival range stands and the walk may stop there. A range with no tile
    /// that stands, such as a shrine's heart, is searched to its last cell for nothing.
    /// </summary>
    private static bool TryGoal(Point3D to, TileWalker walker, int arrivalRange, out Point3D goal, out int range)
    {
        range = arrivalRange;

        if (walker.FloorNear(to.X, to.Y, to.Z) is { } floor)
        {
            goal = new Point3D(to.X, to.Y, floor);
            return true;
        }

        goal = to;

        if (arrivalRange > 0 && TryNearestStanding(to, walker, arrivalRange, out _))
        {
            return true;
        }

        range = 0;
        return TryNearestStanding(to, walker, SnapRadius, out goal);
    }

    /// <summary>The nearest tile within <paramref name="radius"/> of a spot that stands near its height, ring by ring.</summary>
    public static bool TryNearestStanding(Point3D to, TileWalker walker, int radius, out Point3D standing)
    {
        for (var ring = 1; ring <= radius; ring++)
        {
            for (var dx = -ring; dx <= ring; dx++)
            {
                for (var dy = -ring; dy <= ring; dy++)
                {
                    var x = to.X + dx;
                    var y = to.Y + dy;

                    if (Math.Abs(dx) != ring && Math.Abs(dy) != ring || x < 0 || y < 0)
                    {
                        continue;
                    }

                    if (walker.FloorNear(x, y, to.Z) is { } near)
                    {
                        standing = new Point3D(x, y, near);
                        return true;
                    }
                }
            }
        }

        standing = to;
        return false;
    }

    /// <summary>
    /// Every <see cref="WaypointSpacing"/> tiles of the walk, plus each tile where the
    /// floor changes, so every leg handed to the game pathfinder stays short and aimed at
    /// the floor the search found. The last waypoint is the tile the search reached: when
    /// the goal itself stands nowhere, that is the nearest tile that does. The Spirituality
    /// shrine's arrival sits inside the shrine, and a last leg aimed at it stalled every
    /// ghost that walked there. Every tile beside one the walker must not step on is a
    /// waypoint too: the game pathfinder knows no trap or pad, and a leg past one cut
    /// across it.
    /// </summary>
    private static IReadOnlyList<Point3D> Waypoints(Point3D start, List<Point3D> trail, TileWalker walker)
    {
        var points = new List<Point3D>();
        var lastKept = start;

        for (var i = 0; i < trail.Count; i++)
        {
            var tile = trail[i];

            if (i == trail.Count - 1 ||
                NavMetric.Chebyshev(lastKept, tile) >= WaypointSpacing ||
                !NavMetric.SameFloor(lastKept, tile) ||
                BesideHarm(walker, tile))
            {
                points.Add(tile);
                lastKept = tile;
            }
        }

        return points;
    }

    /// <summary>True when a tile round <paramref name="tile"/> at its height hurts the walker, and the tile itself does not.</summary>
    private static bool BesideHarm(TileWalker walker, Point3D tile)
    {
        if (walker.Harms == null || walker.IsHarmed(tile.X, tile.Y, tile.Z))
        {
            return false;
        }

        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            if (walker.IsHarmed(tile.X + dx, tile.Y + dy, tile.Z))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when <paramref name="at"/> lies within <paramref name="range"/> of a goal, on its floor.</summary>
    private static bool Arrived(Point3D at, IReadOnlyList<Point3D> goals, int range)
    {
        for (var i = 0; i < goals.Count; i++)
        {
            if (NavMetric.Chebyshev(at, goals[i]) <= range && NavMetric.SameFloor(at, goals[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The octile distance to the nearest goal: never more than the cheapest walk to any of them.</summary>
    private static int Heuristic(Point3D from, IReadOnlyList<Point3D> goals)
    {
        var best = int.MaxValue;

        for (var i = 0; i < goals.Count; i++)
        {
            best = Math.Min(best, Heuristic(from, goals[i]));
        }

        return best;
    }

    /// <summary>Octile distance in step costs: never more than the cheapest walk on open ground.</summary>
    private static int Heuristic(Point3D from, Point3D to)
    {
        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        return StraightCost * Math.Max(dx, dy) + (DiagonalCost - StraightCost) * Math.Min(dx, dy);
    }
}
