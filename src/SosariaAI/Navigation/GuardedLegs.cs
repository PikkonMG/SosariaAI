using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using SosariaAI.Combat;

namespace SosariaAI.Navigation;

/// <summary>
/// The walked legs of a road net that cross ground the guards cover: a straight line between
/// two nodes that cuts a town's corner or runs through it, though neither end lies in the town.
/// Barring the nodes alone left such a leg open to a red. They are found once per graph from
/// the map regions, each leg read tile by tile (<see cref="Crosses"/>), and learned at run time
/// besides: a red whose walk on a leg found itself under the guards marks that leg for good
/// (<see cref="Learn"/>), since the engine path does not always keep to the straight line. The
/// world thread writes; a set once handed out is never changed (a learned leg publishes a new
/// set), so a path worker reads the set its <see cref="PathSearchBars"/> holds without a lock.
/// A leg's key is the <see cref="EdgeHealthRules.Key"/> of its two ends, one for both ways.
/// </summary>
public static class GuardedLegs
{
    private static readonly ConditionalWeakTable<NavGraph, IReadOnlySet<long>> ByGraph = new();

    /// <summary>The legs of <paramref name="graph"/> known to cross the guards on <paramref name="map"/>. World thread only.</summary>
    public static IReadOnlySet<long> For(NavGraph graph, Map map)
    {
        if (ByGraph.TryGetValue(graph, out var known))
        {
            return known;
        }

        var legs = new HashSet<long>();
        bool Guarded(int x, int y, int z) => GuardCall.IsGuardedPlace(new Point3D(x, y, z), map);

        foreach (var node in graph.Nodes)
        {
            if (node.Index < 0)
            {
                continue;
            }

            var neighbors = graph.NeighborIds(node.Index);
            var gates = graph.NeighborGate(node.Index);

            for (var i = 0; i < neighbors.Length; i++)
            {
                // Each leg once, from its lower end. A gate hop walks no ground between its pads.
                if (neighbors[i] > node.Index && !gates[i] &&
                    Crosses(node.Location, graph.NodeAt(neighbors[i]).Location, Guarded))
                {
                    legs.Add(EdgeHealthRules.Key(node.Index, neighbors[i]));
                }
            }
        }

        ByGraph.AddOrUpdate(graph, legs);
        return legs;
    }

    /// <summary>
    /// Marks the leg between nodes <paramref name="from"/> and <paramref name="to"/> as crossing
    /// the guards from now on. World thread only. False when either name is no node or the leg
    /// was known already.
    /// </summary>
    public static bool Learn(NavGraph graph, Map map, string from, string to)
    {
        var a = graph?.SearchIndex(from) ?? -1;
        var b = graph?.SearchIndex(to) ?? -1;

        if (a < 0 || b < 0 || a == b)
        {
            return false;
        }

        var known = For(graph, map);
        var key = EdgeHealthRules.Key(a, b);

        if (known.Contains(key))
        {
            return false;
        }

        ByGraph.AddOrUpdate(graph, new HashSet<long>(known) { key });
        return true;
    }

    /// <summary>True when a tile strictly between the two ends of the straight leg lies under the guards.</summary>
    public static bool Crosses(Point3D from, Point3D to, Func<int, int, int, bool> guarded)
    {
        var tiles = NavMetric.Chebyshev(from, to);

        for (var step = WalkLine.Step; step < tiles; step += WalkLine.Step)
        {
            if (guarded(
                    WalkLine.Lerp(from.X, to.X, step, tiles),
                    WalkLine.Lerp(from.Y, to.Y, step, tiles),
                    WalkLine.Lerp(from.Z, to.Z, step, tiles)
                ))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when a walk shows that its leg crosses the guards: a living red walks a road planned
    /// clear of them and meets them, standing under them or held at their line by its own feet
    /// (<see cref="SosariaAI.Mobiles.CharacterMotor.HeldAtGuardLine"/>). A road that set out
    /// under the guards, or crosses them on purpose, shows nothing, and a ghost is no guard candidate.
    /// </summary>
    public static bool Shows(bool murderer, bool ghost, bool roadClearOfGuards, bool metGuards) =>
        murderer && !ghost && roadClearOfGuards && metGuards;
}
