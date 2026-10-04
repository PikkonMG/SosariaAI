using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Lays and checks the walking edges of a generated graph. Every edge is walked step by
/// step with the walker's own rule, both ways, before it is kept, so the game pathfinder
/// can follow every hop the graph offers.
/// </summary>
public static class GraphConnect
{
    public const int MaxEdgesPerNode = 6;
    public const int BridgeMax = 192;
    public const int BridgesPerPass = 1500;
    public const int BridgePasses = 8;

    private const int MinJoinChebyshev = 1;
    private const int RouteCandidateTries = 4;
    private const string TempFacet = "connect";
    private const string BridgeNamePrefix = "br";

    /// <summary>An edge laid by the terrain router. Roads must keep their links.</summary>
    public static void LinkForRoad(NavNode left, NavNode right) => AddUndirected(left, right);

    /// <summary>Joins near nodes, bridges the gaps between pieces, then links the shops.</summary>
    /// <param name="walker">How a walker stands and steps; <see cref="Standable.Walker"/> on a live map.</param>
    public static void Connect(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        Join(nodes, walker, isIndoor);

        for (var pass = 0; pass < BridgePasses; pass++)
        {
            var before = nodes.Count;
            Bridge(nodes, walker, isIndoor);
            Join(nodes, walker, isIndoor);

            if (nodes.Count == before)
            {
                break;
            }
        }

        BridgeFar(nodes, walker, isIndoor);
        Join(nodes, walker, isIndoor);
        AttachDoors(nodes, walker, isIndoor);
    }

    /// <summary>Links each outdoor node to its nearest outdoor neighbours a straight walk reaches.</summary>
    public static void Join(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        if (nodes == null || walker == null)
        {
            return;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var from = nodes[i];

            if (!IsNamed(from) || from.Indoor)
            {
                continue;
            }

            from.Connects ??= [];

            var candidates = new List<ScoredNode>();

            for (var j = 0; j < nodes.Count; j++)
            {
                var to = nodes[j];

                if (i == j || !IsNamed(to) || to.Indoor || NamesEqual(from.Name, to.Name))
                {
                    continue;
                }

                var dist = NavMetric.Chebyshev(from.Location, to.Location);

                if (dist >= MinJoinChebyshev && dist <= NavLimits.SoftLegDistance)
                {
                    candidates.Add(new ScoredNode(to, dist));
                }
            }

            candidates.Sort(CompareScored);

            var remaining = MaxEdgesPerNode - from.Connects.Count;

            for (var k = 0; k < candidates.Count && remaining > 0; k++)
            {
                var to = candidates[k].Node;

                if (HasLink(from, to.Name) || !LineWalks(walker, from, to, isIndoor))
                {
                    continue;
                }

                AddUndirected(from, to);
                remaining--;
            }
        }
    }

    /// <summary>Joins pieces whose nearest nodes lie beyond a leg but within <see cref="BridgeMax"/>.</summary>
    public static void Bridge(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        if (Pieces(nodes, walker) is not { } pieces)
        {
            return;
        }

        var pairs = new List<BridgePair>();

        for (var i = 0; i < pieces.Origins.Count; i++)
        {
            var from = pieces.Origins[i];

            for (var j = i + 1; j < pieces.Origins.Count; j++)
            {
                var to = pieces.Origins[j];

                if (pieces.ComponentOf[from.Name] == pieces.ComponentOf[to.Name])
                {
                    continue;
                }

                var dist = NavMetric.Chebyshev(from.Location, to.Location);

                if (dist > NavLimits.SoftLegDistance && dist <= BridgeMax)
                {
                    pairs.Add(new BridgePair(from, to, dist));
                }
            }
        }

        LayBridges(nodes, pieces, pairs, BridgesPerPass, walker, isIndoor);
    }

    /// <summary>Joins pieces of any span through the nearest pair of nodes between them.</summary>
    public static void BridgeFar(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        if (Pieces(nodes, walker) is not { } pieces)
        {
            return;
        }

        var best = new Dictionary<long, BridgePair>();

        for (var i = 0; i < pieces.Origins.Count; i++)
        {
            var from = pieces.Origins[i];
            var fromId = pieces.ComponentOf[from.Name];

            for (var j = i + 1; j < pieces.Origins.Count; j++)
            {
                var to = pieces.Origins[j];
                var toId = pieces.ComponentOf[to.Name];

                if (fromId == toId)
                {
                    continue;
                }

                var dist = NavMetric.Chebyshev(from.Location, to.Location);
                var key = PackComponentPair(fromId, toId);

                if (dist <= NavLimits.SoftLegDistance || best.TryGetValue(key, out var known) && known.Distance <= dist)
                {
                    continue;
                }

                best[key] = new BridgePair(from, to, dist);
            }
        }

        LayBridges(nodes, pieces, [.. best.Values], pieces.Origins.Count, walker, isIndoor);
    }

    /// <summary>
    /// Removes a walking edge between two outdoor nodes when no straight walk follows it
    /// both ways out from under every roof. The join, the bridges and the terrain router
    /// lay each outdoor edge as such a walk, with a road node at every bend, so an edge
    /// that fails it cuts through a building.
    /// </summary>
    public static void DropIndoorShortcuts(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor)
    {
        if (nodes == null || walker == null || isIndoor == null)
        {
            return;
        }

        DropUnwalked(nodes, (a, b) => a.Indoor || b.Indoor || LineWalks(walker, a, b, isIndoor));
    }

    /// <summary>Gives each shop with no edge one to the nearest outdoor node a walk reaches.</summary>
    public static void AttachDoors(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        if (nodes == null || walker == null)
        {
            return;
        }

        var byName = IndexByName(nodes);
        var outdoor = new List<NavNode>();

        foreach (var node in byName.Values)
        {
            if (!node.Indoor)
            {
                outdoor.Add(node);
            }
        }

        foreach (var indoor in byName.Values)
        {
            // Keep the links it has. Search already refuses to pass through an indoor
            // node, so cutting them only splits the town it was holding together.
            if (!indoor.Indoor || indoor.Connects is { Count: > 0 })
            {
                continue;
            }

            var door = NearestReachable(indoor, outdoor, walker, isIndoor);

            if (door != null)
            {
                AddUndirected(indoor, door);
            }
        }
    }

    /// <summary>
    /// Gives each outdoor node with no walking link an edge to the nearest linked node a
    /// real walk reaches: a straight walk first, then a tile route. A node that stands
    /// alone is a goal no plan reaches and a start no plan leaves.
    /// </summary>
    public static void LinkStranded(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        if (nodes == null || walker == null)
        {
            return;
        }

        var linked = new List<NavNode>();
        var stranded = new List<NavNode>();

        var byName = IndexByName(nodes);

        foreach (var node in byName.Values)
        {
            if (!node.Indoor)
            {
                (HasWalkLink(node, byName) ? linked : stranded).Add(node);
            }
        }

        for (var i = 0; i < stranded.Count; i++)
        {
            var node = stranded[i];
            var target = NearestReachable(node, linked, walker, isIndoor);

            if (target != null)
            {
                AddUndirected(node, target);
            }
        }
    }

    /// <summary>
    /// Walks every walking edge both ways and drops the ones no walk follows: a straight
    /// walk first, then a tile route for an edge that bends round trees or through a
    /// door. Gate edges are travel, not a walk, and are left alone.
    /// </summary>
    public static void VerifyEdges(List<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> isIndoor = null)
    {
        if (nodes == null || walker == null)
        {
            return;
        }

        DropUnwalked(nodes, (a, b) => LineWalks(walker, a, b, isIndoor) || RouteWalks(walker, a, b, isIndoor));
    }

    /// <summary>
    /// Removes every walking edge <paramref name="walks"/> refuses, testing each pair once.
    /// Gate edges are travel, not a walk, and are left alone.
    /// </summary>
    private static void DropUnwalked(List<NavNode> nodes, Func<NavNode, NavNode, bool> walks)
    {
        var byName = IndexByName(nodes);
        var checkedPairs = new HashSet<(string, string)>();

        foreach (var node in byName.Values)
        {
            var links = node.Connects;

            if (links == null)
            {
                continue;
            }

            for (var i = links.Count - 1; i >= 0; i--)
            {
                if (!byName.TryGetValue(links[i], out var other) ||
                    NavGates.KindEitherWay(node, other) != NavGateKind.None ||
                    !checkedPairs.Add(PairKey(node.Name, other.Name)))
                {
                    continue;
                }

                if (!walks(node, other))
                {
                    RemoveUndirected(node, other);
                }
            }
        }
    }

    /// <summary>
    /// Removes the nodes left with no edge and no gate after every join. A lone node is a
    /// goal no plan reaches; a marker in a wall or on a roof leaves one behind.
    /// </summary>
    public static void DropUnlinked(List<NavNode> nodes) =>
        nodes?.RemoveAll(node =>
            IsNamed(node) &&
            node.Connects is not { Count: > 0 } &&
            node.Gates is not { Count: > 0 }
        );

    /// <summary>
    /// True when an edge leaves the node on foot, not only through a gate. The landing of a
    /// one-way pad lists the pad among its links though only the pad holds the gate, so
    /// <paramref name="byName"/> lets the pad's own gates be read; without it only the
    /// node's own gates count.
    /// </summary>
    public static bool HasWalkLink(NavNode node, IReadOnlyDictionary<string, NavNode> byName = null)
    {
        var links = node?.Connects;

        if (links == null)
        {
            return false;
        }

        for (var i = 0; i < links.Count; i++)
        {
            var kind = byName != null && byName.TryGetValue(links[i], out var other)
                ? NavGates.KindEitherWay(node, other)
                : NavGates.KindBetween(node, links[i]);

            if (kind == NavGateKind.None)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Outdoor nodes and the piece each belongs to, or null when there are fewer than two
    /// pieces to join.
    /// </summary>
    private static PieceMap Pieces(List<NavNode> nodes, TileWalker walker)
    {
        if (nodes == null || walker == null)
        {
            return null;
        }

        var origins = new List<NavNode>();

        for (var i = 0; i < nodes.Count; i++)
        {
            if (IsNamed(nodes[i]) && !nodes[i].Indoor)
            {
                origins.Add(nodes[i]);
            }
        }

        var graph = new NavGraph(TempFacet, origins);
        var componentOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var maxComponent = -1;

        for (var i = 0; i < origins.Count; i++)
        {
            var name = origins[i].Name;
            var id = graph.ComponentOf(name);
            componentOf[name] = id;
            maxComponent = Math.Max(maxComponent, id);
        }

        if (maxComponent < 1)
        {
            return null;
        }

        var parent = new int[maxComponent + 1];

        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        return new PieceMap(origins, componentOf, parent);
    }

    /// <summary>
    /// Lays a bridge for each pair, nearest first, whose pieces are still apart and whose
    /// straight line walks both ways, piece by piece. At most <paramref name="cap"/>
    /// bridges are laid.
    /// </summary>
    private static void LayBridges(
        List<NavNode> nodes,
        PieceMap pieces,
        List<BridgePair> pairs,
        int cap,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        pairs.Sort(ComparePairs);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            if (IsNamed(nodes[i]))
            {
                usedNames.Add(nodes[i].Name);
            }
        }

        var laid = 0;

        for (var i = 0; i < pairs.Count && laid < cap; i++)
        {
            var pair = pairs[i];
            var fromId = pieces.ComponentOf[pair.From.Name];
            var toId = pieces.ComponentOf[pair.To.Name];

            if (FindRoot(pieces.Parent, fromId) == FindRoot(pieces.Parent, toId) ||
                !InsertBridge(nodes, usedNames, pair, walker, OutdoorKeep(pair.From, pair.To, isIndoor)))
            {
                continue;
            }

            Union(pieces.Parent, fromId, toId);
            laid++;
        }
    }

    private static long PackComponentPair(int left, int right)
    {
        var min = left < right ? left : right;
        var max = left < right ? right : left;
        return ((long)min << 32) | (uint)max;
    }

    /// <summary>
    /// Lays hop nodes a leg apart along a walked line. Each hop stands where the walk
    /// from the previous hop ends, so its height is the floor the walker reached, and
    /// every piece of the chain walks both ways. Nothing is laid when a piece fails.
    /// </summary>
    private static bool InsertBridge(
        List<NavNode> nodes,
        HashSet<string> usedNames,
        BridgePair pair,
        TileWalker walker,
        Func<int, int, int, bool> keep
    )
    {
        var from = pair.From;
        var to = pair.To;
        var points = new List<Point3D> { from.Location };

        for (var step = NavLimits.SoftLegDistance; step < pair.Distance; step += NavLimits.SoftLegDistance)
        {
            var x = WalkLine.Lerp(from.X, to.X, step, pair.Distance);
            var y = WalkLine.Lerp(from.Y, to.Y, step, pair.Distance);
            var last = points[^1];

            if (!WalkLine.TryWalk(walker, last.X, last.Y, last.Z, x, y, keep, out var z))
            {
                return false;
            }

            var hop = new Point3D(x, y, z);

            if (!WalkLine.Reaches(walker, hop, last, keep))
            {
                return false;
            }

            points.Add(hop);
        }

        var tail = points[^1];

        if (!WalkLine.Reaches(walker, tail, to.Location, keep) || !WalkLine.Reaches(walker, to.Location, tail, keep))
        {
            return false;
        }

        var chain = new List<NavNode> { from };

        for (var i = 1; i < points.Count; i++)
        {
            var hop = new NavNode
            {
                Name = UniqueName(usedNames, $"{BridgeNamePrefix}-{from.Name}-{to.Name}", i - 1),
                X = points[i].X,
                Y = points[i].Y,
                Z = points[i].Z,
                ArrivalRange = NavLimits.DefaultArrivalRange,
                Connects = []
            };

            nodes.Add(hop);
            chain.Add(hop);
        }

        chain.Add(to);

        for (var i = 0; i < chain.Count - 1; i++)
        {
            AddUndirected(chain[i], chain[i + 1]);
        }

        return true;
    }

    private static string UniqueName(HashSet<string> usedNames, string stem, int index)
    {
        var name = $"{stem}-{index}";

        while (!usedNames.Add(name))
        {
            index++;
            name = $"{stem}-{index}";
        }

        return name;
    }

    /// <summary>
    /// Closest of <paramref name="targets"/> a walk reaches from the node: a straight walk
    /// to the nearest first, then a tile route to the nearest few.
    /// </summary>
    private static NavNode NearestReachable(
        NavNode from,
        List<NavNode> targets,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        var candidates = new List<ScoredNode>();

        for (var i = 0; i < targets.Count; i++)
        {
            var other = targets[i];
            var dist = NavMetric.Chebyshev(from.Location, other.Location);

            if (dist >= MinJoinChebyshev && dist <= NavLimits.SoftLegDistance)
            {
                candidates.Add(new ScoredNode(other, dist));
            }
        }

        candidates.Sort(CompareScored);

        for (var i = 0; i < candidates.Count; i++)
        {
            if (LineWalks(walker, from, candidates[i].Node, isIndoor))
            {
                return candidates[i].Node;
            }
        }

        var tries = Math.Min(candidates.Count, RouteCandidateTries);

        for (var i = 0; i < tries; i++)
        {
            if (RouteWalks(walker, from, candidates[i].Node, isIndoor))
            {
                return candidates[i].Node;
            }
        }

        return null;
    }

    /// <summary>
    /// True when a tile route runs both ways between the nodes. Only a node standing on a
    /// floor the walker finds at its own height qualifies: a route that starts on another
    /// floor, such as the ground under an upstairs room, would fake a link the game
    /// pathfinder cannot walk.
    /// </summary>
    private static bool RouteWalks(TileWalker walker, NavNode a, NavNode b, Func<int, int, int, bool> isIndoor) =>
        StandsOnOwnFloor(walker, a) &&
        StandsOnOwnFloor(walker, b) &&
        TileRoute.FindForBuild(a.Location, b.Location, walker, isIndoor).Count > 0 &&
        TileRoute.FindForBuild(b.Location, a.Location, walker, isIndoor).Count > 0;

    private static bool StandsOnOwnFloor(TileWalker walker, NavNode node) =>
        walker.FloorNear(node.X, node.Y, node.Z) is { } floor && NavMetric.SameFloor(floor, node.Z);

    /// <summary>
    /// True when the straight line between the nodes walks both ways, step by step. A line
    /// between two outdoor nodes must also stay out from under every roof.
    /// </summary>
    private static bool LineWalks(TileWalker walker, NavNode a, NavNode b, Func<int, int, int, bool> isIndoor)
    {
        var keep = OutdoorKeep(a, b, isIndoor);
        return WalkLine.Reaches(walker, a.Location, b.Location, keep) &&
               WalkLine.Reaches(walker, b.Location, a.Location, keep);
    }

    private static Func<int, int, int, bool> OutdoorKeep(NavNode a, NavNode b, Func<int, int, int, bool> isIndoor) =>
        WalkLine.OutdoorKeep(a.Indoor, b.Indoor, isIndoor);

    /// <summary>One key for a pair of node names, whichever comes first.</summary>
    internal static (string, string) PairKey(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0
            ? (a.ToLowerInvariant(), b.ToLowerInvariant())
            : (b.ToLowerInvariant(), a.ToLowerInvariant());

    private static Dictionary<string, NavNode> IndexByName(List<NavNode> nodes)
    {
        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];

            if (IsNamed(node))
            {
                byName.TryAdd(node.Name, node);
            }
        }

        return byName;
    }

    private static void AddUndirected(NavNode left, NavNode right)
    {
        if (!IsNamed(left) || !IsNamed(right) || NamesEqual(left.Name, right.Name))
        {
            return;
        }

        AddLink(left, right.Name);
        AddLink(right, left.Name);
    }

    private static void RemoveUndirected(NavNode left, NavNode right)
    {
        if (!IsNamed(left) || !IsNamed(right))
        {
            return;
        }

        RemoveLink(left, right.Name);
        RemoveLink(right, left.Name);
    }

    private static void AddLink(NavNode node, string other)
    {
        node.Connects ??= [];

        if (!HasLink(node, other))
        {
            node.Connects.Add(other);
        }
    }

    private static void RemoveLink(NavNode node, string other)
    {
        var links = node.Connects;

        if (links == null)
        {
            return;
        }

        for (var i = links.Count - 1; i >= 0; i--)
        {
            if (NamesEqual(links[i], other))
            {
                links.RemoveAt(i);
            }
        }
    }

    private static bool HasLink(NavNode node, string other)
    {
        var links = node.Connects;

        if (links == null)
        {
            return false;
        }

        for (var i = 0; i < links.Count; i++)
        {
            if (NamesEqual(links[i], other))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNamed(NavNode node) =>
        node != null && !string.IsNullOrWhiteSpace(node.Name);

    private static bool NamesEqual(string a, string b) =>
        a.Equals(b, StringComparison.OrdinalIgnoreCase);

    /// <summary>The set an id belongs to in a union-find table, with the path to it shortened.</summary>
    internal static int FindRoot(int[] parent, int id)
    {
        var root = id;

        while (parent[root] != root)
        {
            root = parent[root];
        }

        while (id != root)
        {
            var next = parent[id];
            parent[id] = root;
            id = next;
        }

        return root;
    }

    /// <summary>Puts the sets of two ids in a union-find table together.</summary>
    internal static void Union(int[] parent, int a, int b)
    {
        var left = FindRoot(parent, a);
        var right = FindRoot(parent, b);

        if (left != right)
        {
            parent[left] = right;
        }
    }

    private static int CompareScored(ScoredNode a, ScoredNode b)
    {
        var byDistance = a.Distance.CompareTo(b.Distance);

        if (byDistance != 0)
        {
            return byDistance;
        }

        return string.Compare(a.Node.Name, b.Node.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static int ComparePairs(BridgePair a, BridgePair b)
    {
        var byDistance = a.Distance.CompareTo(b.Distance);

        if (byDistance != 0)
        {
            return byDistance;
        }

        var byFrom = string.Compare(a.From.Name, b.From.Name, StringComparison.OrdinalIgnoreCase);

        if (byFrom != 0)
        {
            return byFrom;
        }

        return string.Compare(a.To.Name, b.To.Name, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ScoredNode(NavNode Node, int Distance);

    private readonly record struct BridgePair(NavNode From, NavNode To, int Distance);

    private sealed record PieceMap(List<NavNode> Origins, Dictionary<string, int> ComponentOf, int[] Parent);
}
