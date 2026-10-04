using System;
using System.Buffers;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;

namespace SosariaAI.Navigation;

public static class NavSearch
{
    public const double DefaultGateCost = NavSettings.DefaultGateCost;

    private static readonly object CacheGate = new();
    private static NavGraph _cachedGraph;
    private static PathSearchBars _cachedBars;
    private static string _cachedSource;
    private static double _cachedGateCost;
    private static int _cachedHealthVersion;
    private static long _cachedValidUntil;
    private static Dictionary<string, double> _cachedCost;
    private static Dictionary<string, string> _cachedPrev;

    /// <summary>A node this close to a place the character fled is steered round.</summary>
    public const int AvoidRadiusTiles = 24;

    /// <summary>
    /// The extra cost of a node near a place the character fled, in tiles. Large enough
    /// that any sane way round wins, but not a wall: with no other way the road is used.
    /// </summary>
    public const double AvoidPenaltyTiles = 2000;

    public static IReadOnlyList<string> FindPath(NavGraph graph, string from, string to) =>
        FindPath(graph, from, to, -1);

    public static IReadOnlyList<string> FindPath(NavGraph graph, string from, string to, double gateCost) =>
        FindPath(graph, from, to, gateCost, avoid: null);

    public static IReadOnlyList<string> FindPath(
        NavGraph graph,
        string from,
        string to,
        double gateCost,
        IReadOnlyList<Point3D> avoid
    )
    {
        if (graph == null || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return [];
        }

        if (!graph.TryGetNode(from, out _) || !graph.TryGetNode(to, out _))
        {
            return [];
        }

        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            return [from];
        }

        if (!graph.SameComponent(from, to))
        {
            return [];
        }

        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from, to };
        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Explore(graph, from, to, gateCost, avoid, keep, cost, prev, walkAway: true);
        return Reconstruct(prev, from, to);
    }

    /// <summary>
    /// One Dijkstra from <paramref name="source"/>. <paramref name="prev"/> maps each
    /// reached node to the previous node toward the source. Pass a blank
    /// <paramref name="stopAt"/> to fill the reachable set; otherwise the search
    /// stops when that node is dequeued. An edge a walk recently failed on costs the
    /// extra tiles <see cref="EdgeHealth"/> holds for it. <paramref name="bars"/> closes
    /// nodes, walked legs and gates the traveler will not use at all, or prices the guarded ground of a red
    /// that must cross it (<see cref="PathSearchBars.PenaltyTiles"/>). By default the walkers come toward
    /// the source, as a plan searched from its goal; <paramref name="walkAway"/> searches a
    /// walk that leaves the source. A one-way pad is taken only its own way.
    /// </summary>
    public static void Explore(
        NavGraph graph,
        string source,
        string stopAt,
        double gateCost,
        IReadOnlyList<Point3D> avoid,
        ISet<string> indoorKeep,
        Dictionary<string, double> cost,
        Dictionary<string, string> prev,
        PathSearchBars bars = null,
        bool walkAway = false
    )
    {
        if (graph == null || string.IsNullOrWhiteSpace(source) || cost == null || prev == null)
        {
            return;
        }

        if (!graph.TryGetNode(source, out _))
        {
            return;
        }

        var resolvedGateCost = gateCost < 0 ? DefaultGateCost : gateCost;
        var stop = string.IsNullOrWhiteSpace(stopAt) ? null : stopAt;
        var avoidOn = avoid is { Count: > 0 };
        // Bars on pads alone are shared by every walker of one reach (FloorDrops); a murderer's read its start.
        var cacheable = stop == null && !avoidOn && bars?.KeepsOffGuards != true && !walkAway &&
                        KeepIsOutdoorOnly(graph, source, indoorKeep);
        var health = EdgeHealth.For(graph);
        var now = Environment.TickCount64;
        var healthVersion = health?.Version ?? 0;

        if (cacheable)
        {
            lock (CacheGate)
            {
                if (TryCopyCache(graph, source, resolvedGateCost, bars, healthVersion, now, cost, prev))
                {
                    return;
                }
            }
        }

        var src = graph.SearchIndex(source);
        var stopId = stop == null ? -1 : graph.SearchIndex(stop);

        if (src < 0)
        {
            return;
        }

        var count = graph.NodeCount;
        var best = ArrayPool<double>.Shared.Rent(count);
        var parent = ArrayPool<int>.Shared.Rent(count);

        try
        {
            Array.Fill(best, double.PositiveInfinity, 0, count);
            Array.Fill(parent, -1, 0, count);
            best[src] = 0;
            var open = new PriorityQueue<int, double>();
            open.Enqueue(src, 0);

            while (open.Count > 0)
            {
                var current = open.Dequeue();

                if (current == stopId)
                {
                    break;
                }

                var here = best[current];
                var hereNode = graph.NodeAt(current);
                var neighborIds = graph.NeighborIds(current);
                var neighborGate = graph.NeighborGate(current);
                var closed = graph.NeighborClosed(current, inbound: !walkAway);

                for (var i = 0; i < neighborIds.Length; i++)
                {
                    var next = neighborIds[i];

                    if (next < 0 || closed[i])
                    {
                        continue;
                    }

                    var nextNode = graph.NodeAt(next);

                    if (nextNode.Indoor && indoorKeep?.Contains(nextNode.Name) != true)
                    {
                        continue;
                    }

                    if (bars != null &&
                        (bars.BarsNode(next, nextNode.Location) ||
                         (neighborGate[i]
                             ? bars.BarsGate(graph.GateKind(hereNode.Name, nextNode.Name), current, next)
                             : bars.BarsLeg(current, next, hereNode.Location, nextNode.Location))))
                    {
                        continue;
                    }

                    var step = neighborGate[i]
                        ? resolvedGateCost
                        : NavMetric.Distance(hereNode.Location, nextNode.Location);

                    if (bars != null)
                    {
                        step += bars.PenaltyTiles(current, next, hereNode.Location, nextNode.Location, step, neighborGate[i]);
                    }

                    if (next != stopId && IsNearAny(nextNode.Location, avoid))
                    {
                        step += AvoidPenaltyTiles;
                    }

                    if (health != null)
                    {
                        step += health.PenaltyTiles(current, next, now);
                    }

                    var total = here + step;

                    if (total >= best[next])
                    {
                        continue;
                    }

                    best[next] = total;
                    parent[next] = current;
                    open.Enqueue(next, total);
                }
            }

            cost[source] = 0;

            for (var i = 0; i < count; i++)
            {
                if (parent[i] < 0)
                {
                    continue;
                }

                var node = graph.NodeAt(i);
                cost[node.Name] = best[i];
                prev[node.Name] = graph.NodeAt(parent[i]).Name;
            }
        }
        finally
        {
            ArrayPool<double>.Shared.Return(best);
            ArrayPool<int>.Shared.Return(parent);
        }

        if (cacheable)
        {
            lock (CacheGate)
            {
                StoreCache(graph, source, resolvedGateCost, bars, healthVersion, health?.NextExpiry ?? long.MaxValue, cost, prev);
            }
        }
    }

    private static bool KeepIsOutdoorOnly(NavGraph graph, string source, ISet<string> indoorKeep)
    {
        if (indoorKeep == null || indoorKeep.Count == 0)
        {
            return true;
        }

        foreach (var name in indoorKeep)
        {
            if (name.Equals(source, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (graph.TryGetNode(name, out var node) && node.Indoor)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCopyCache(
        NavGraph graph,
        string source,
        double gateCost,
        PathSearchBars bars,
        int healthVersion,
        long now,
        Dictionary<string, double> cost,
        Dictionary<string, string> prev
    )
    {
        if (!ReferenceEquals(graph, _cachedGraph) ||
            !ReferenceEquals(bars, _cachedBars) ||
            _cachedPrev == null ||
            _cachedCost == null ||
            _cachedGateCost != gateCost ||
            _cachedHealthVersion != healthVersion ||
            now >= _cachedValidUntil ||
            _cachedSource == null ||
            !_cachedSource.Equals(source, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var pair in _cachedCost)
        {
            cost[pair.Key] = pair.Value;
        }

        foreach (var pair in _cachedPrev)
        {
            prev[pair.Key] = pair.Value;
        }

        return true;
    }

    private static void StoreCache(
        NavGraph graph,
        string source,
        double gateCost,
        PathSearchBars bars,
        int healthVersion,
        long validUntil,
        Dictionary<string, double> cost,
        Dictionary<string, string> prev
    )
    {
        _cachedGraph = graph;
        _cachedBars = bars;
        _cachedSource = source;
        _cachedGateCost = gateCost;
        _cachedHealthVersion = healthVersion;
        _cachedValidUntil = validUntil;
        _cachedCost = new Dictionary<string, double>(cost, StringComparer.OrdinalIgnoreCase);
        _cachedPrev = new Dictionary<string, string>(prev, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> Reconstruct(
        Dictionary<string, string> prev,
        string from,
        string to
    )
    {
        if (prev == null || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return [];
        }

        if (from.Equals(to, StringComparison.OrdinalIgnoreCase))
        {
            return [from];
        }

        var stack = new Stack<string>();
        var current = to;
        stack.Push(current);

        while (!current.Equals(from, StringComparison.OrdinalIgnoreCase))
        {
            if (!prev.TryGetValue(current, out current))
            {
                return [];
            }

            stack.Push(current);
        }

        return [.. stack];
    }

    public static bool IsNearAny(Point3D location, IReadOnlyList<Point3D> spots)
    {
        if (spots == null)
        {
            return false;
        }

        for (var i = 0; i < spots.Count; i++)
        {
            if (NavMetric.Chebyshev(location, spots[i]) <= AvoidRadiusTiles)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="location"/> lies near a spot (<see cref="IsNearAny"/>) and
    /// nearer to it than <paramref name="standsAt"/>, where the walker stands. A walker that
    /// got clear of a fight still stands inside the spot's radius, since a run ends
    /// <see cref="SosariaAI.Combat.RetreatRules.ClearTiles"/> from the nearest foe: every road
    /// from there, even one that leads away, read as a road past the danger, and 164 walks
    /// home and 82 walks to a dungeon door ended at the first step after the run.
    /// </summary>
    public static bool ComesNearer(Point3D location, Point3D standsAt, IReadOnlyList<Point3D> spots)
    {
        if (spots == null)
        {
            return false;
        }

        for (var i = 0; i < spots.Count; i++)
        {
            var tiles = NavMetric.Chebyshev(location, spots[i]);

            if (tiles <= AvoidRadiusTiles && tiles < NavMetric.Chebyshev(standsAt, spots[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the straight leg from <paramref name="from"/> to <paramref name="to"/> comes
    /// near a spot and nearer to it than the walker at <paramref name="standsAt"/> stands
    /// (<see cref="ComesNearer"/>). The leg is read every <see cref="AvoidRadiusTiles"/> tiles
    /// and at its end, so no tile of it lies farther than half the radius from a reading.
    /// </summary>
    public static bool IsLegNearAny(Point3D from, Point3D to, IReadOnlyList<Point3D> spots, Point3D standsAt)
    {
        if (spots is not { Count: > 0 })
        {
            return false;
        }

        var tiles = NavMetric.Chebyshev(from, to);

        for (var step = 0; step < tiles; step += AvoidRadiusTiles)
        {
            var at = new Point3D(
                WalkLine.Lerp(from.X, to.X, step, tiles),
                WalkLine.Lerp(from.Y, to.Y, step, tiles),
                WalkLine.Lerp(from.Z, to.Z, step, tiles)
            );

            if (ComesNearer(at, standsAt, spots))
            {
                return true;
            }
        }

        return ComesNearer(to, standsAt, spots);
    }
}
