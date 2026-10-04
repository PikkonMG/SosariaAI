using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;

namespace SosariaAI.Navigation;

public static class Traveler
{
    public const int StartCandidates = 12;
    public const string WhyNoGraph = "no graph";
    public const string WhyNoStart = "no node within one leg of the walker";
    public const string WhyNoPath = "no graph path from the near nodes";
    public const string WhyFirstHopsBlocked = "the walk to the first node is blocked";
    public const string WhyNoGoalNode = "no node lies within one leg of the goal";
    public const string WhyGoalApart = "the goal's nodes lie in another piece of the graph than the walker's";
    public const string WhyRecentFailure = "a search from here failed moments ago";
    public const string WhySearchRefused = "the path search did not take the job";

    /// <summary>
    /// True when a search ran and found no way from where the walker stands to the goal:
    /// water, a gateless island, a sealed pocket, or a gate the walker may not take. A
    /// goal with no node near it, a cooldown, or a refused search says nothing of the road.
    /// </summary>
    public static bool IsNoRoad(string why) =>
        why is WhyNoPath or WhyGoalApart or WhyNoStart or WhyFirstHopsBlocked;

    /// <summary>
    /// True when the walker's own ground stopped the plan, whatever the goal: no node within
    /// one leg, or no walk to the near nodes that route.
    /// </summary>
    public static bool IsGroundFailure(string why) => why is WhyNoStart or WhyFirstHopsBlocked;

    /// <summary>Nodes a sealed-pocket proof walks in a straight line from the spot.</summary>
    public const int HopsToVerify = 3;

    /// <summary>
    /// Starts whose straight walk from the walker is blocked that may still be proved by a
    /// tile route. Each proof is an A* on the world thread, so only the nearest few try.
    /// </summary>
    public const int FirstHopTileTries = 2;

    /// <summary>
    /// How long every trip from a 16-tile cell rests once no plan started there and no walk
    /// back onto the road was found (<see cref="IsGroundFailure"/>): the ground is at fault,
    /// not the goal. Tancred Ashford planned from the same wild spot 30 times in half an hour,
    /// each time to another goal, and each plan froze the shard for seconds.
    /// </summary>
    public const int GroundRestMs = 120_000;

    /// <summary>The cooldown key of the walker's own ground, which every goal from its cell shares.</summary>
    public const string GroundTarget = "ground";

    /// <summary>
    /// After a search finds no route, the same 16-tile cell does not search that
    /// destination again for this long. Failed bank walks were repeating every think,
    /// and far tavern retries sit about six seconds apart.
    /// </summary>
    public const int FailedRouteCooldownMs = 20000;

    /// <summary>Marks a cooldown key made from a goal tile when no node names the goal.</summary>
    public const string TileTargetPrefix = "tile:";

    /// <summary>Cooldowns held before a sweep drops the lapsed ones.</summary>
    public const int CooldownSweepAbove = 4096;

    private const int FailCellShift = 4;
    private static readonly Dictionary<(NavGraph Graph, int X, int Y, string Dest), long> FailedUntil = new();
    private static int _cooldownSweepAt = CooldownSweepAbove;

    /// <summary>Nodes scanned for a rescue spot and for the escape probe.</summary>
    private const int RescueScanCount = StartCandidates * 4;

    /// <summary>
    /// Cells the tile proofs of one rescue share (<see cref="NearestRoutableStart"/>): two of
    /// the longest world-thread searches. Up to 96 proofs of 80,000 cells each could run.
    /// </summary>
    public const int RescueCells = TileRoute.WorldMaxCells * 2;

    /// <summary>
    /// Street-reach proofs (<see cref="HasClearStart"/>) one rescue may run on the nodes its
    /// tile walks passed. Each runs up to six graph searches on the world thread.
    /// </summary>
    public const int RescueStartProofs = 3;

    /// <summary>
    /// How far the escape probe must sit from the sealed spot. A courtyard wall is
    /// tens of tiles across at most, so a node this far out is past the pocket.
    /// </summary>
    private const int EscapeProbeMinDistance = 48;

    /// <summary>How many probe targets a clear-start proof may try before it gives up.</summary>
    private const int EscapeProbeTries = 6;

    /// <summary>
    /// Plans a route on the world thread and steers round <paramref name="avoid"/>, the
    /// places the traveller recently fled. The route explain command uses it to show the
    /// same verdict the path workers reach.
    /// </summary>
    public static IReadOnlyList<string> PlanNames(
        NavGraph graph,
        Point3D from,
        string destinationNode,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        IReadOnlyList<Point3D> avoid,
        out string whyNone
    )
    {
        if (graph == null || string.IsNullOrWhiteSpace(destinationNode) || walker == null)
        {
            whyNone = WhyNoGraph;
            return [];
        }

        var failKey = FailKey(graph, from, destinationNode);

        if (Cooling(failKey))
        {
            whyNone = WhyRecentFailure;
            return [];
        }

        // With no start in reach the whole-graph search would be wasted.
        if (UsableStarts(graph, from, destinationNode, isIndoor).Count == 0)
        {
            whyNone = WhyNoStart;
            NoteFailedRoute(failKey);
            return [];
        }

        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keep = IndoorKeepFor(graph, from, destinationNode, isIndoor);
        NavSearch.Explore(graph, destinationNode, stopAt: null, GateCost(), avoid, keep, cost, prev);
        var path = FinishPlan(graph, from, destinationNode, prev, walker, isIndoor, avoid, out whyNone);

        if (path.Count > 0)
        {
            FailedUntil.Remove(failKey);
        }
        else
        {
            NoteFailedRoute(failKey);
        }

        return path;
    }

    /// <summary>
    /// The key a failed trip is cooled down under: the goal node, or the goal tile when no
    /// node names it. Null only when the trip has neither.
    /// </summary>
    public static string FailTarget(string destinationNode, Point3D goal)
    {
        if (!string.IsNullOrWhiteSpace(destinationNode))
        {
            return destinationNode;
        }

        return goal == Point3D.Zero ? null : $"{TileTargetPrefix}{goal.X},{goal.Y},{goal.Z}";
    }

    /// <summary>True when a trip from here to <paramref name="failTarget"/> cools down, or every trip from here rests (<see cref="RestsOnGround"/>).</summary>
    public static bool InFailedCooldown(NavGraph graph, Point3D from, string failTarget) =>
        !string.IsNullOrWhiteSpace(failTarget) &&
        (Cooling(FailKey(graph, from, failTarget)) || RestsOnGround(graph, from));

    /// <summary>True while every trip from the cell of <paramref name="from"/> rests (<see cref="GroundRestMs"/>).</summary>
    public static bool RestsOnGround(NavGraph graph, Point3D from) => Cooling(FailKey(graph, from, GroundTarget));

    /// <summary>Every trip from the cell of <paramref name="from"/> rests for <see cref="GroundRestMs"/>.</summary>
    public static void NoteGroundRest(NavGraph graph, Point3D from) =>
        CoolDown(FailKey(graph, from, GroundTarget), GroundRestMs);

    private static bool Cooling((NavGraph Graph, int X, int Y, string Dest) key) =>
        FailedUntil.TryGetValue(key, out var until) && until > Environment.TickCount64;

    /// <summary>
    /// Cools down a failed trip, even one with no graph or no node near its goal: a ghost
    /// whose shrine had no node searched again every ten seconds.
    /// </summary>
    public static void NoteFailedRoute(NavGraph graph, Point3D from, string failTarget)
    {
        if (string.IsNullOrWhiteSpace(failTarget))
        {
            return;
        }

        NoteFailedRoute(FailKey(graph, from, failTarget));
    }

    private static (NavGraph Graph, int X, int Y, string Dest) FailKey(
        NavGraph graph,
        Point3D from,
        string destinationNode
    ) => (graph, from.X >> FailCellShift, from.Y >> FailCellShift, destinationNode);

    private static void NoteFailedRoute((NavGraph Graph, int X, int Y, string Dest) key) =>
        CoolDown(key, FailedRouteCooldownMs);

    private static void CoolDown((NavGraph Graph, int X, int Y, string Dest) key, int milliseconds)
    {
        var now = Environment.TickCount64;
        FailedUntil[key] = now + milliseconds;

        if (FailedUntil.Count > _cooldownSweepAt)
        {
            SweepCooldowns(now);
        }
    }

    /// <summary>
    /// Drops every cooldown that ran out by <paramref name="now"/>; the next sweep waits until the
    /// map doubles. A lapsed cooldown was kept for good, one per cell and goal ever failed.
    /// Returns the cooldowns dropped.
    /// </summary>
    internal static int SweepCooldowns(long now)
    {
        var lapsed = new List<(NavGraph Graph, int X, int Y, string Dest)>();

        foreach (var (key, until) in FailedUntil)
        {
            if (until <= now)
            {
                lapsed.Add(key);
            }
        }

        for (var i = 0; i < lapsed.Count; i++)
        {
            FailedUntil.Remove(lapsed[i]);
        }

        _cooldownSweepAt = Math.Max(CooldownSweepAbove, FailedUntil.Count * 2);
        return lapsed.Count;
    }

    public static NavNode PreferredGoal(NavGraph graph, Point3D from, Point3D goal)
    {
        if (graph == null)
        {
            return null;
        }

        var goals = WithinOneLeg(graph.FindNearest(goal, StartCandidates), goal);
        var starts = WithinOneLeg(graph.FindNearest(from, StartCandidates), from);
        var main = graph.LargestComponent();
        NavNode preferred = null;

        for (var i = 0; i < goals.Count; i++)
        {
            var candidate = goals[i];

            if (!SharesPieceWithAny(graph, starts, candidate))
            {
                continue;
            }

            if (graph.ComponentOf(candidate.Name) == main)
            {
                return candidate;
            }

            preferred ??= candidate;
        }

        return preferred;
    }

    // The single nearest node can sit in a scrap of the graph: at the Britain bank tile it is
    // a two-node piece, and every goal in town was refused from there. A goal counts when any
    // of the start candidates the walker may head for shares its piece.
    private static bool SharesPieceWithAny(NavGraph graph, IReadOnlyList<NavNode> starts, NavNode candidate)
    {
        if (starts == null || starts.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < starts.Count; i++)
        {
            if (graph.SameComponent(starts[i].Name, candidate.Name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Any node within one leg of the goal tile. A goal with none cannot route
    /// at all — that failure belongs to the place, not the traveller's start.
    /// </summary>
    public static bool GoalHasNode(NavGraph graph, Point3D goal) =>
        graph != null && WithinOneLeg(graph.FindNearest(goal, StartCandidates), goal).Count > 0;

    /// <summary>
    /// Why <see cref="PreferredGoal"/> named no goal node: none near the goal, or only
    /// nodes in a piece no start near the walker belongs to. Cove's homes had nodes two
    /// tiles away, and the log still said the goal had none.
    /// </summary>
    public static string WhyNoGoal(NavGraph graph, Point3D goal) =>
        GoalHasNode(graph, goal) ? WhyGoalApart : WhyNoGoalNode;

    /// <summary>
    /// True when a trip from <paramref name="from"/> to <paramref name="goal"/> would end with
    /// <see cref="WhyGoalApart"/>: the goal has nodes near it, and none shares a piece with the
    /// nodes near the walker. The south Jhelom island is such a place once its dead pad went
    /// (<see cref="DeadPads"/>): its 21 shops drew 28 "another piece" walks in one evening.
    /// </summary>
    public static bool GoalApart(NavGraph graph, Point3D from, Point3D goal) =>
        graph != null && PreferredGoal(graph, from, goal) == null && GoalHasNode(graph, goal);

    public static HashSet<string> IndoorKeepFor(
        NavGraph graph,
        Point3D from,
        string destinationNode,
        Func<int, int, int, bool> isIndoor
    )
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { destinationNode };

        if (graph == null)
        {
            return keep;
        }

        var usable = UsableStarts(graph, from, destinationNode, isIndoor);

        for (var i = 0; i < usable.Count; i++)
        {
            if (!usable[i].Indoor)
            {
                keep.Add(usable[i].Name);
            }
        }

        return keep;
    }

    /// <summary>
    /// Turns one search from the destination into the walker's route, with the stage that
    /// stopped it for the activity log. Generated edges are trusted: the world generator
    /// walks each one. Only the walker's own walk to its first node is proved here, by a
    /// straight walk or, for the nearest few starts, by a tile route <paramref name="walker"/>
    /// walks round the obstacle.
    /// </summary>
    public static IReadOnlyList<string> FinishPlan(
        NavGraph graph,
        Point3D from,
        string destinationNode,
        Dictionary<string, string> prev,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        IReadOnlyList<Point3D> avoid,
        out string whyNone
    )
    {
        whyNone = null;

        if (graph == null || prev == null || walker == null)
        {
            whyNone = WhyNoGraph;
            return [];
        }

        var usable = UsableStarts(graph, from, destinationNode, isIndoor);
        var pathsFound = 0;
        var tileTries = 0;

        for (var i = 0; i < usable.Count; i++)
        {
            var start = usable[i];
            var path = start.Indoor
                ? NavSearch.FindPath(graph, start.Name, destinationNode, GateCost(), avoid)
                : PathFromExplore(prev, destinationNode, start.Name);

            if (path.Count == 0)
            {
                continue;
            }

            pathsFound++;

            if (LineWalks(walker, from, start, isIndoor))
            {
                return path;
            }

            if (tileTries < FirstHopTileTries)
            {
                tileTries++;

                if (NearWalk(from, start, walker, isIndoor))
                {
                    return path;
                }
            }
        }

        whyNone = usable.Count == 0 ? WhyNoStart : pathsFound == 0 ? WhyNoPath : WhyFirstHopsBlocked;
        return [];
    }

    /// <summary>
    /// Every node a walk from <paramref name="from"/> reaches, on the roads and gates
    /// <paramref name="bars"/> leaves the walker, each one-way pad only its own way, and never
    /// through a building, with the tiles of the trip to it: the straight tiles to the start the
    /// walk sets out from plus the road search's cost on from there, a gate weighing the
    /// configured gate cost. A start is used only when the walk to it is proved as
    /// <see cref="FinishPlan"/> proves it; one that a used start already reaches adds nothing and
    /// is not searched. A node keeps the trip from the first start that reaches it, nearest first:
    /// the plan sets out from the first start in that order with a road to its goal, so that is
    /// the trip the walk makes. Empty when no start is in reach or none is proved. Ask
    /// <see cref="WalkReaches"/> whether a goal node is reached and <see cref="WalkTrip"/> how far.
    /// The undirected graph pieces say too much: the Spirituality ankh platform shares the main
    /// piece through its exit pads alone, and 572 walks to it found no road.
    /// </summary>
    public static Dictionary<string, double> WalkReach(
        NavGraph graph,
        Point3D from,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        PathSearchBars bars
    )
    {
        var trips = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        if (graph == null || walker == null)
        {
            return trips;
        }

        var starts = UsableStarts(graph, from, keepIndoorStarts: isIndoor?.Invoke(from.X, from.Y, from.Z) == true);
        var tileTries = 0;

        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];

            if (trips.ContainsKey(start.Name))
            {
                continue;
            }

            if (!LineWalks(walker, from, start, isIndoor))
            {
                if (tileTries >= FirstHopTileTries)
                {
                    continue;
                }

                tileTries++;

                if (!NearWalk(from, start, walker, isIndoor))
                {
                    continue;
                }
            }

            var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            NavSearch.Explore(graph, start.Name, stopAt: null, GateCost(), avoid: null, indoorKeep: null, cost, prev, bars, walkAway: true);
            var firstHop = NavMetric.Distance(from, start.Location);

            foreach (var (name, tiles) in cost)
            {
                trips.TryAdd(name, firstHop + tiles);
            }
        }

        return trips;
    }

    /// <summary>True when a walk that reached <paramref name="trips"/> (<see cref="WalkReach"/>) gets to <paramref name="goal"/> (<see cref="WalkTrip"/>).</summary>
    public static bool WalkReaches(NavGraph graph, IReadOnlyDictionary<string, double> trips, NavNode goal, PathSearchBars bars) =>
        WalkTrip(graph, trips, goal, bars) != null;

    /// <summary>
    /// The tiles of the trip a walk that reached <paramref name="trips"/> (<see cref="WalkReach"/>)
    /// makes to <paramref name="goal"/>, or null when it does not get there. A plan keeps its own
    /// goal even under a roof, so an indoor goal, a healer's shop, is reached when a reached node
    /// steps into it, on a gate <paramref name="bars"/> leaves open, over the cheapest such step.
    /// </summary>
    public static double? WalkTrip(NavGraph graph, IReadOnlyDictionary<string, double> trips, NavNode goal, PathSearchBars bars)
    {
        if (graph == null || trips == null || goal == null)
        {
            return null;
        }

        if (trips.TryGetValue(goal.Name, out var direct))
        {
            return direct;
        }

        if (!goal.Indoor || goal.Index < 0)
        {
            return null;
        }

        var neighbors = graph.NeighborIds(goal.Index);
        var gates = graph.NeighborGate(goal.Index);
        var closedIn = graph.NeighborClosed(goal.Index, inbound: true);
        double? best = null;

        for (var i = 0; i < neighbors.Length; i++)
        {
            if (neighbors[i] < 0 || closedIn[i])
            {
                continue;
            }

            var neighbor = graph.NodeAt(neighbors[i]);

            if (!trips.TryGetValue(neighbor.Name, out var toNeighbor) ||
                gates[i] && bars?.BarsGate(graph.GateKind(neighbor.Name, goal.Name), neighbors[i], goal.Index) == true)
            {
                continue;
            }

            var trip = toNeighbor + (gates[i] ? GateCost() : NavMetric.Distance(neighbor.Location, goal.Location));

            if (best is not { } shortest || trip < shortest)
            {
                best = trip;
            }
        }

        return best;
    }

    /// <summary>
    /// The tile walk back onto the road from a spot no plan starts from: to whichever outdoor
    /// node near the walker it reaches first, of those <paramref name="prev"/>, the path
    /// worker's search from the destination, already routes and whose route's first hops a
    /// straight walk follows. One tile search within <see cref="TileRoute.WorldCellBudget"/>
    /// serves them all, and no graph search runs here. A graph search and a full tile route per
    /// node near the goal froze the world thread for five to eight seconds, 186 times in one
    /// night. Empty when no such node is reached within the cells.
    /// </summary>
    /// <param name="routeWalker">The walker the tile walk goes with, when it is not <paramref name="walker"/>: a dungeon walk shies from pads.</param>
    public static IReadOnlyList<Point3D> RoadBack(
        NavGraph graph,
        Point3D from,
        string destinationNode,
        Dictionary<string, string> prev,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        TileWalker routeWalker = null
    )
    {
        if (graph == null || prev == null || walker == null || string.IsNullOrWhiteSpace(destinationNode))
        {
            return [];
        }

        var nearby = graph.FindNearest(from, StartCandidates);
        var ends = new List<Point3D>(nearby.Count);

        for (var i = 0; i < nearby.Count; i++)
        {
            var node = nearby[i];

            if (node.Indoor)
            {
                continue;
            }

            var path = PathFromExplore(prev, destinationNode, node.Name);

            if (path.Count > 0 && FirstHopsClear(graph, node.Location, path, walker, isIndoor))
            {
                ends.Add(node.Location);
            }
        }

        return ends.Count == 0 ? [] : TileRoute.FindAny(from, ends, routeWalker ?? walker, isIndoor, out _);
    }

    public static IReadOnlyList<NavNode> GoalNodes(NavGraph graph, Point3D at)
    {
        if (graph == null)
        {
            return [];
        }

        return WithinOneLeg(graph.FindNearest(at, StartCandidates), at);
    }

    /// <summary>
    /// The first leg runs from the character to a node. It has the same cap as every
    /// other leg: the game pathfinder cannot cross more than <see cref="NavLimits.MaxLegDistance"/>.
    /// </summary>
    private static IReadOnlyList<NavNode> WithinOneLeg(IReadOnlyList<NavNode> nodes, Point3D from)
    {
        var kept = new List<NavNode>(nodes.Count);

        for (var i = 0; i < nodes.Count; i++)
        {
            if (NavMetric.WithinLegCap(from, nodes[i].Location))
            {
                kept.Add(nodes[i]);
            }
        }

        return kept;
    }

    /// <summary>
    /// Nodes within one leg that a walk from here may bind to. Whether the walker can
    /// actually reach one, on its floor, is the first-hop check's question.
    /// </summary>
    private static List<NavNode> UsableStarts(
        NavGraph graph,
        Point3D from,
        string destinationNode,
        Func<int, int, int, bool> isIndoor
    ) =>
        UsableStarts(
            graph,
            from,
            keepIndoorStarts: isIndoor?.Invoke(from.X, from.Y, from.Z) == true ||
                              !graph.TryGetNode(destinationNode, out var goal) ||
                              goal.Indoor
        );

    /// <summary>
    /// Nodes within one leg of <paramref name="from"/>; an indoor one only with
    /// <paramref name="keepIndoorStarts"/>: a walk from open ground to open ground does not
    /// start inside a building.
    /// </summary>
    private static List<NavNode> UsableStarts(NavGraph graph, Point3D from, bool keepIndoorStarts)
    {
        var starts = WithinOneLeg(graph.FindNearest(from, StartCandidates), from);
        var usable = new List<NavNode>(starts.Count);

        for (var i = 0; i < starts.Count; i++)
        {
            if (keepIndoorStarts || !starts[i].Indoor)
            {
                usable.Add(starts[i]);
            }
        }

        return usable;
    }

    private static bool HasLineStart(
        NavGraph graph,
        Point3D from,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        var starts = graph.FindNearest(from, RescueScanCount);

        for (var i = 0; i < starts.Count; i++)
        {
            var node = starts[i];

            if (!node.Indoor &&
                NavMetric.WithinLegCap(from, node.Location) &&
                LineWalks(walker, from, node, isIndoor))
            {
                return true;
            }
        }

        return false;
    }

    private static (Dictionary<string, string> Prev, int Searches) ExploreToProbe(
        NavGraph graph,
        Point3D origin,
        string probe,
        Func<int, int, int, bool> isIndoor
    )
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { probe };
        var starts = UsableStarts(graph, origin, probe, isIndoor);

        for (var i = 0; i < starts.Count; i++)
        {
            keep.Add(starts[i].Name);
        }

        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        NavSearch.Explore(graph, probe, stopAt: null, GateCost(), avoid: null, keep, cost, prev);
        return (prev, 1);
    }

    // Explore builds prev toward its source. Reconstruct(source, target) therefore
    // walks from the target back to the source and the stack enumerator yields
    // source-first. Reverse that into the start-to-destination order the walker uses.
    private static IReadOnlyList<string> PathFromExplore(
        Dictionary<string, string> prev,
        string source,
        string start
    )
    {
        var towardSource = NavSearch.Reconstruct(prev, source, start);

        if (towardSource.Count <= 1)
        {
            return towardSource;
        }

        var path = new string[towardSource.Count];

        for (var i = 0; i < towardSource.Count; i++)
        {
            path[i] = towardSource[towardSource.Count - 1 - i];
        }

        return path;
    }

    /// <summary>
    /// One street-reach proof for a cluster of tiles. A full graph search per open-ground
    /// tile of a building leave cost towns that much on the world thread.
    /// </summary>
    public sealed class ClearStartProof
    {
        private readonly NavGraph _graph;
        private readonly TileWalker _walker;
        private readonly Func<int, int, int, bool> _isIndoor;
        private readonly Dictionary<string, string> _prev;
        private readonly string _probe;
        private readonly bool _lineOnly;
        private readonly bool _sealedPocket;

        internal ClearStartProof(
            NavGraph graph,
            TileWalker walker,
            Func<int, int, int, bool> isIndoor,
            Dictionary<string, string> prev,
            string probe,
            bool lineOnly,
            bool sealedPocket,
            int planSearches
        )
        {
            _graph = graph;
            _walker = walker;
            _isIndoor = isIndoor;
            _prev = prev;
            _probe = probe;
            _lineOnly = lineOnly;
            _sealedPocket = sealedPocket;
            PlanSearches = planSearches;
        }

        /// <summary>Graph searches run to build this proof. One search must cover many tiles.</summary>
        public int PlanSearches { get; }

        public bool At(Point3D from)
        {
            if (_sealedPocket || _graph == null || _walker == null)
            {
                return false;
            }

            if (_lineOnly)
            {
                return HasLineStart(_graph, from, _walker, _isIndoor);
            }

            if (string.IsNullOrWhiteSpace(_probe) || _prev == null)
            {
                return false;
            }

            var starts = UsableStarts(_graph, from, _probe, _isIndoor);

            for (var i = 0; i < starts.Count; i++)
            {
                var path = PathFromExplore(_prev, _probe, starts[i].Name);

                if (path.Count == 0)
                {
                    continue;
                }

                if (FirstHopsClear(_graph, from, path, _walker, _isIndoor))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// True when a plan can leave this spot. A node in the same sealed room is a
    /// straight walk away just fine, so "any node reachable" is not the question — the
    /// proof is a formed route to an outdoor main-component node past the pocket.
    /// Sparse areas with no probe to plan against fall back to the straight walk. A spot
    /// under a roof leaves as a person does, along the floor and out of the door
    /// (<see cref="BuildingExit"/>): the Jhelom and Skara Brae town sites are their bank
    /// tiles, where no straight line reaches a street, and every copy homed there was
    /// refused a spawn.
    /// </summary>
    public static bool HasClearStart(
        NavGraph graph,
        Point3D from,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor = null
    )
    {
        if (PrepareClearStart(graph, from, walker, isIndoor).At(from))
        {
            return true;
        }

        if (isIndoor?.Invoke(from.X, from.Y, from.Z) != true)
        {
            return false;
        }

        // The first open ground past the door is proved as any outdoor spot is.
        var wayOut = BuildingExit.Find(from, walker, isIndoor, static (_, _, _) => true);

        if (wayOut.Count == 0)
        {
            return false;
        }

        var outside = wayOut[^1];
        return PrepareClearStart(graph, outside, walker, isIndoor).At(outside);
    }

    public static ClearStartProof PrepareClearStart(
        NavGraph graph,
        Point3D origin,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor = null
    )
    {
        if (graph == null || walker == null)
        {
            return new ClearStartProof(graph, walker, isIndoor, null, null, lineOnly: false, sealedPocket: true, planSearches: 0);
        }

        var main = graph.LargestComponent();
        var nearest = graph.FindNearest(origin, RescueScanCount);
        var far = new List<NavNode>();

        for (var i = 0; i < nearest.Count && far.Count < EscapeProbeTries; i++)
        {
            var node = nearest[i];

            if (node.Indoor ||
                graph.ComponentOf(node.Name) != main ||
                NavMetric.Chebyshev(origin, node.Location) < EscapeProbeMinDistance)
            {
                continue;
            }

            far.Add(node);
        }

        if (far.Count > 0)
        {
            for (var i = 0; i < far.Count; i++)
            {
                var (prev, searches) = ExploreToProbe(graph, origin, far[i].Name, isIndoor);
                var proof = new ClearStartProof(
                    graph,
                    walker,
                    isIndoor,
                    prev,
                    far[i].Name,
                    lineOnly: false,
                    sealedPocket: false,
                    planSearches: searches
                );

                if (proof.At(origin))
                {
                    return proof;
                }
            }

            return new ClearStartProof(graph, walker, isIndoor, null, null, lineOnly: false, sealedPocket: true, planSearches: far.Count);
        }

        var probe = EscapeProbe(graph, origin, main, int.MaxValue, static _ => true);

        if (probe != null)
        {
            var (prev, searches) = ExploreToProbe(graph, origin, probe.Name, isIndoor);
            return new ClearStartProof(
                graph,
                walker,
                isIndoor,
                prev,
                probe.Name,
                lineOnly: false,
                sealedPocket: false,
                planSearches: searches
            );
        }

        return new ClearStartProof(graph, walker, isIndoor, null, null, lineOnly: true, sealedPocket: false, planSearches: 0);
    }

    /// <summary>
    /// The nearest node far enough from <paramref name="from"/> to sit outside any
    /// sealed pocket, used as the far end of an escape proof. The rescue scan keeps
    /// to nearby nodes, so in a dense town every scanned node can sit inside the
    /// probe radius and the probe must come from the whole component.
    /// </summary>
    private static NavNode EscapeProbe(
        NavGraph graph,
        Point3D from,
        int main,
        int maxDistance,
        Func<NavNode, bool> qualifies
    )
    {
        NavNode best = null;
        var bestDistance = int.MaxValue;

        foreach (var node in graph.Nodes)
        {
            var distance = NavMetric.Chebyshev(from, node.Location);

            if (distance < EscapeProbeMinDistance || distance > maxDistance || distance >= bestDistance)
            {
                continue;
            }

            if (node.Indoor || graph.ComponentOf(node.Name) != main || !qualifies(node))
            {
                continue;
            }

            best = node;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// The nearest node a stranded character can be placed at: outdoor, standable, in
    /// the graph's largest component, and proven unsealed by a real tile walk to
    /// <paramref name="anchor"/> or to an outside probe. A rooftop node with no walk
    /// down fails that walk. All the walks share <see cref="RescueCells"/>, and at most
    /// <see cref="RescueStartProofs"/> nodes are proved to start a plan. Null when nothing
    /// nearby qualifies within them.
    /// </summary>
    public static NavNode NearestRoutableStart(
        NavGraph graph,
        Point3D from,
        Point3D anchor,
        Func<int, int, int, bool> canStand,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor,
        Func<NavNode, bool> nodeFilter = null
    )
    {
        if (graph == null || canStand == null || walker == null)
        {
            return null;
        }

        var main = graph.LargestComponent();
        var candidates = graph.FindNearest(from, RescueScanCount);

        bool Qualifies(NavNode node) =>
            !node.Indoor &&
            graph.ComponentOf(node.Name) == main &&
            canStand(node.X, node.Y, node.Z) &&
            (nodeFilter == null || nodeFilter(node));

        // A home can sit across the map or inside the same sealed pocket, both of
        // which make the anchor proof fail every candidate. The probe is a
        // qualifying node well outside any yard: reaching it on tiles proves the
        // same escape on a walk the router can take.
        var targets = new List<Point3D>(2);
        Point3D? probe = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            if (NavMetric.Chebyshev(from, candidates[i].Location) >= EscapeProbeMinDistance &&
                Qualifies(candidates[i]))
            {
                probe = candidates[i].Location;
                break;
            }
        }

        // A dense town can fill the whole scan with nodes inside the probe radius,
        // so no probe comes from it and the anchor stays the only proof target —
        // even when it sits inside the same sealed pocket as the spawn.
        probe ??= EscapeProbe(graph, from, main, TileRoute.MaxTripTiles, Qualifies)?.Location;

        if (probe != null)
        {
            targets.Add(probe.Value);
        }

        if (anchor != Point3D.Zero && NavMetric.Chebyshev(from, anchor) <= TileRoute.MaxTripTiles)
        {
            targets.Add(anchor);
        }

        // A node inside the same sealed yard as the spawn passes every check
        // above, so the only honest proof is the walk engine itself. The outside
        // probe goes first: a node that shares the spawn's pocket reaches a
        // pocketed anchor trivially and must not count as an escape. Tile escape
        // alone is still not enough: the node must also start a graph plan, or
        // the character lands on ground the first-hops check refuses and the
        // rescue picks the same dead node every time.
        var proofCells = new TileRoute.CellAllowance(RescueCells);
        var startProofs = 0;

        for (var t = 0; t < targets.Count && !proofCells.Spent && startProofs < RescueStartProofs; t++)
        {
            for (var i = 0; i < candidates.Count && !proofCells.Spent && startProofs < RescueStartProofs; i++)
            {
                var node = candidates[i];

                if (!Qualifies(node))
                {
                    continue;
                }

                if (NavMetric.Chebyshev(node.Location, targets[t]) > TileRoute.MaxTripTiles ||
                    TileRoute.Find(node.Location, targets[t], walker, isIndoor, proofCells).Count == 0)
                {
                    continue;
                }

                startProofs++;

                if (HasClearStart(graph, node.Location, walker, isIndoor))
                {
                    return node;
                }
            }
        }

        return null;
    }

    /// <summary>The walker's tile walk from a spot to a node, within <see cref="TileRoute.WorldCellBudget"/>.</summary>
    private static bool NearWalk(Point3D from, NavNode to, TileWalker walker, Func<int, int, int, bool> isIndoor) =>
        TileRoute.FindAny(from, [to.Location], walker, isIndoor, out _).Count > 0;

    /// <summary>
    /// The walker's straight walk from a spot to a node, ending on the node's floor.
    /// </summary>
    private static bool LineWalks(
        TileWalker walker,
        Point3D from,
        NavNode to,
        Func<int, int, int, bool> isIndoor
    ) =>
        LineWalks(walker, from, isIndoor?.Invoke(from.X, from.Y, from.Z) == true, to, isIndoor);

    /// <summary>
    /// The walker's straight walk to a node, ending on its floor. A line between two
    /// outdoor spots must not cut through a building on the way.
    /// </summary>
    private static bool LineWalks(
        TileWalker walker,
        Point3D from,
        bool fromIndoor,
        NavNode to,
        Func<int, int, int, bool> isIndoor
    ) =>
        WalkLine.Reaches(walker, from, to.Location, WalkLine.OutdoorKeep(fromIndoor, to.Indoor, isIndoor));

    /// <summary>
    /// A sealed-pocket proof: the straight walks from the spot through the first
    /// <see cref="HopsToVerify"/> nodes of a route. A node in the same sealed room reaches
    /// a formed route only through its walls.
    /// </summary>
    private static bool FirstHopsClear(
        NavGraph graph,
        Point3D from,
        IReadOnlyList<string> path,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        var prev = from;
        NavNode prevNode = null;
        var hops = path.Count < HopsToVerify ? path.Count : HopsToVerify;
        var prevIndoor = isIndoor?.Invoke(from.X, from.Y, from.Z) == true;

        for (var i = 0; i < hops; i++)
        {
            if (!graph.TryGetNode(path[i], out var node))
            {
                return false;
            }

            // A gate hop is a jump, not a walk. A person on the Britain pad bound for Yew
            // had every route refused because the line from the pad to Yew's pad was
            // "blocked", by the whole world between them.
            var isJump = prevNode != null && NavGates.KindBetween(prevNode, node.Name) != NavGateKind.None;

            if (!isJump && !LineWalks(walker, prev, prevIndoor, node, isIndoor))
            {
                return false;
            }

            prev = node.Location;
            prevNode = node;
            prevIndoor = node.Indoor;
        }

        return true;
    }

    private static double GateCost()
    {
        var cost = SosariaSettings.Characters?.Nav?.GateCost ?? NavSettings.DefaultGateCost;
        return cost < 0 ? NavSettings.DefaultGateCost : cost;
    }
}
