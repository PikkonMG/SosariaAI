using System;
using System.Collections.Generic;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Navigation;

/// <summary>
/// Walking links whose straight walk steps onto a teleporter pad between its two ends. The pad
/// carries the walker off halfway: the link from the landing at (1630,3320) west to
/// (1628,3320) crosses the pad at (1629,3320), which sends back up into the Trinsic passage,
/// and walkers sent along it stalled 275 times at (5898,1411) on a leg four thousand tiles
/// off. A pad in the same row as a pad the link ends on, one that lands where that pad
/// lands, carries the walker where the link meant to go and does no harm.
///
/// A node a person stands on and walks away from must walk to a node that is no pad: a node
/// that lost a link here, and every landing. The landings at (5918,1411) and (5918,1412) in
/// the Trinsic passage walked only to the pad at (5918,1410), which sends back down into the
/// passage's lower pocket, so walkers going home stalled 32 times in that pocket on a leg to
/// (5908,1412). A walk over other landings counts only when it gets past them: the landings
/// of the Destard stair up at (5129..5132,908) walk to each other and onto the pads down
/// beside them, and nowhere else, so nobody up from the second level reached the door. Such a
/// node gets the straight walk both ways to the nearest node past those landings that is no
/// pad and whose walk crosses none. When every such walk crosses a pad, it steps off onto a
/// tile beside it first: a new node there walks straight on round the pad. The landing at
/// (1630,3320) has one road node near it, at (1626,3323), and the straight walk there steps
/// onto the pad at (1629,3320); walkers up out of the passage were sent straight back down.
/// </summary>
public static class PadCrossings
{
    /// <summary>Ends the name of a node a relink adds beside the node it steps off (<see cref="StepOff"/>).</summary>
    public const string StepOffSuffix = "-off";

    /// <summary>
    /// Drops every walking link that crosses a pad, then relinks each node a person walks
    /// away from, a landing or a node that lost a link, that has no walk but onto a pad. A
    /// relink that steps off first adds its new node to <paramref name="nodes"/>. World thread
    /// when <paramref name="walker"/> reads a live map.
    /// </summary>
    /// <param name="isIndoor">The roof test; a new link between two outdoor nodes keeps out from under it.</param>
    /// <returns>How many links went, and how many nodes got a new one.</returns>
    public static (int Dropped, int Relinked) Drop(
        List<NavNode> nodes,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        if (nodes == null || walker == null)
        {
            return (0, 0);
        }

        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);
        var pads = new Dictionary<(int X, int Y), NavNode>();
        var landings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Name) || !byName.TryAdd(node.Name, node))
            {
                continue;
            }

            if (Landing(node) != null)
            {
                pads.TryAdd((node.X, node.Y), node);
            }

            foreach (var gate in node.Gates ?? [])
            {
                if (gate?.To != null)
                {
                    landings.Add(gate.To);
                }
            }
        }

        var dropped = 0;
        var touched = new List<NavNode>();

        foreach (var name in landings)
        {
            if (byName.TryGetValue(name, out var landing))
            {
                touched.Add(landing);
            }
        }
        var checkedPairs = new HashSet<(string, string)>();

        foreach (var node in byName.Values)
        {
            var links = node.Connects;

            for (var i = (links?.Count ?? 0) - 1; i >= 0; i--)
            {
                if (!byName.TryGetValue(links[i], out var other) ||
                    NavGates.KindEitherWay(node, other) != NavGateKind.None ||
                    !checkedPairs.Add(GraphConnect.PairKey(node.Name, other.Name)) ||
                    !Crosses(node, other, pads, byName))
                {
                    continue;
                }

                links.RemoveAt(i);
                other.Connects?.RemoveAll(name => name.Equals(node.Name, StringComparison.OrdinalIgnoreCase));
                touched.Add(node);
                touched.Add(other);
                dropped++;
            }
        }

        var relinked = 0;
        var relinkTried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in touched)
        {
            // No walk leaves a pad no gate lands on: whoever steps onto it is carried off.
            var padOnly = pads.ContainsKey((node.X, node.Y)) && !landings.Contains(node.Name);

            if (!padOnly && relinkTried.Add(node.Name) && LandingPocket(node, pads, landings, byName) is { } pocket &&
                Relink(node, pocket, nodes, pads, byName, walker, isIndoor))
            {
                relinked++;
            }
        }

        return (dropped, relinked);
    }

    /// <summary>
    /// True when the straight walk between two nodes steps onto a pad between them that
    /// carries the walker elsewhere than a pad at either end would.
    /// </summary>
    public static bool Crosses(
        NavNode a,
        NavNode b,
        IReadOnlyDictionary<(int X, int Y), NavNode> pads,
        IReadOnlyDictionary<string, NavNode> byName
    )
    {
        var distance = NavMetric.Chebyshev(a.Location, b.Location);

        for (var step = WalkLine.Step; step < distance; step += WalkLine.Step)
        {
            var tile = (WalkLine.Lerp(a.X, b.X, step, distance), WalkLine.Lerp(a.Y, b.Y, step, distance));

            if (pads.TryGetValue(tile, out var pad) &&
                (GatePad.OnPadFloor(a.Location, pad.Location) || GatePad.OnPadFloor(b.Location, pad.Location)) &&
                !SameRow(pad, a, byName) && !SameRow(pad, b, byName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when <paramref name="end"/> is a pad that lands where <paramref name="pad"/> does.</summary>
    private static bool SameRow(NavNode pad, NavNode end, IReadOnlyDictionary<string, NavNode> byName) =>
        Landing(end) is { } endLanding && Landing(pad) is { } padLanding &&
        byName.TryGetValue(endLanding, out var endAt) && byName.TryGetValue(padLanding, out var padAt) &&
        NavMetric.Chebyshev(endAt.Location, padAt.Location) <= GatePad.LandingSlackTiles;

    /// <summary>The node the first teleporter gate of a node lands on, or null for a node that is no pad.</summary>
    private static string Landing(NavNode node)
    {
        foreach (var gate in node.Gates ?? [])
        {
            if (gate?.To != null && gate.ParsedKind == NavGateKind.Teleporter)
            {
                return gate.To;
            }
        }

        return null;
    }

    /// <summary>
    /// The nodes a walk from <paramref name="node"/> reaches without stepping onto a pad, when
    /// every one of them is the node or a landing: the pocket it is stuck in. Null when the walk
    /// gets past the landings to a node that is neither. Walking links only.
    /// </summary>
    private static HashSet<string> LandingPocket(
        NavNode node,
        IReadOnlyDictionary<(int X, int Y), NavNode> pads,
        IReadOnlySet<string> landings,
        IReadOnlyDictionary<string, NavNode> byName
    )
    {
        var pocket = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { node.Name };
        var open = new Stack<NavNode>();
        open.Push(node);

        while (open.Count > 0)
        {
            var here = open.Pop();

            foreach (var name in here.Connects ?? [])
            {
                if (!byName.TryGetValue(name, out var other) || NavGates.KindEitherWay(here, other) != NavGateKind.None ||
                    pads.ContainsKey((other.X, other.Y)) || !pocket.Add(other.Name))
                {
                    continue;
                }

                if (!landings.Contains(other.Name))
                {
                    return null;
                }

                open.Push(other);
            }
        }

        return pocket;
    }

    /// <summary>
    /// Links the node to the nearest outdoor node within a road leg, out of its
    /// <paramref name="pocket"/> and no pad, whose straight walk goes both ways and crosses no
    /// pad. When there is none, the nearest such node a step off onto a tile beside the node
    /// walks to (<see cref="StepOff"/>), through a new node there.
    /// </summary>
    private static bool Relink(
        NavNode node,
        IReadOnlySet<string> pocket,
        List<NavNode> all,
        IReadOnlyDictionary<(int X, int Y), NavNode> pads,
        Dictionary<string, NavNode> byName,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        var candidates = new List<(int Distance, NavNode Node)>();

        foreach (var other in byName.Values)
        {
            var distance = NavMetric.Chebyshev(node.Location, other.Location);

            if (!pocket.Contains(other.Name) && !other.Indoor && distance <= NavLimits.SoftLegDistance &&
                !pads.ContainsKey((other.X, other.Y)))
            {
                candidates.Add((distance, other));
            }
        }

        candidates.Sort(static (a, b) => a.Distance.CompareTo(b.Distance));

        foreach (var (_, other) in candidates)
        {
            var keep = WalkLine.OutdoorKeep(node.Indoor, other.Indoor, isIndoor);

            if (WalksBothWays(walker, node, other, keep) && !Crosses(node, other, pads, byName))
            {
                GraphConnect.LinkForRoad(node, other);
                return true;
            }
        }

        foreach (var (_, other) in candidates)
        {
            if (StepOff(node, other, pads, byName, walker, isIndoor) is not { } step)
            {
                continue;
            }

            all.Add(step);
            byName[step.Name] = step;
            GraphConnect.LinkForRoad(node, step);
            GraphConnect.LinkForRoad(step, other);
            return true;
        }

        return false;
    }

    /// <summary>
    /// A new node on a tile beside <paramref name="node"/>, no pad, that a step from the node
    /// reaches and leaves both ways, and from which the straight walk to <paramref name="other"/>
    /// goes both ways and crosses no pad. Null when no tile beside the node serves.
    /// </summary>
    private static NavNode StepOff(
        NavNode node,
        NavNode other,
        IReadOnlyDictionary<(int X, int Y), NavNode> pads,
        IReadOnlyDictionary<string, NavNode> byName,
        TileWalker walker,
        Func<int, int, int, bool> isIndoor
    )
    {
        foreach (var (dx, dy) in TileGrid.Neighbours)
        {
            var x = node.X + dx;
            var y = node.Y + dy;

            if (pads.ContainsKey((x, y)) || !walker.SafeStep(node.X, node.Y, node.Z, x, y, out var z))
            {
                continue;
            }

            var step = new NavNode
            {
                Name = UnusedName(byName, node.Name + StepOffSuffix),
                X = x,
                Y = y,
                Z = z,
                ArrivalRange = NavLimits.DefaultArrivalRange,
                Indoor = isIndoor?.Invoke(x, y, z) == true,
                Connects = []
            };

            if (WalksBothWays(walker, node, step, WalkLine.OutdoorKeep(node.Indoor, step.Indoor, isIndoor)) &&
                WalksBothWays(walker, step, other, WalkLine.OutdoorKeep(step.Indoor, other.Indoor, isIndoor)) &&
                !Crosses(step, other, pads, byName))
            {
                return step;
            }
        }

        return null;
    }

    private static bool WalksBothWays(TileWalker walker, NavNode a, NavNode b, Func<int, int, int, bool> keep) =>
        WalkLine.Reaches(walker, a.Location, b.Location, keep) && WalkLine.Reaches(walker, b.Location, a.Location, keep);

    /// <summary><paramref name="stem"/>, or the stem with the first number that makes it a name no node has.</summary>
    private static string UnusedName(IReadOnlyDictionary<string, NavNode> byName, string stem)
    {
        var name = stem;

        for (var i = 1; byName.ContainsKey(name); i++)
        {
            name = stem + i;
        }

        return name;
    }
}
