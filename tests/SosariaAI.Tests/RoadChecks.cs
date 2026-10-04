using System;
using System.Collections.Generic;
using System.Linq;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>Checks on the walking links a graph build lays.</summary>
internal static class RoadChecks
{
    /// <summary>True when walking links alone lead from one node to the other, no gate on the way.</summary>
    public static bool WalkJoined(IReadOnlyCollection<NavNode> nodes, string from, string to)
    {
        var byName = nodes.ToDictionary(node => node.Name, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { from };
        var queue = new Queue<string>([from]);

        while (queue.Count > 0)
        {
            var node = byName[queue.Dequeue()];

            foreach (var other in node.Connects.Where(name =>
                         byName.TryGetValue(name, out var linked) && NavGates.KindEitherWay(node, linked) == NavGateKind.None))
            {
                if (seen.Add(other))
                {
                    queue.Enqueue(other);
                }
            }
        }

        return seen.Contains(to);
    }

    /// <summary>Every walking link is a straight walk both ways, no longer than a road leg.</summary>
    public static void AssertWalksBothWays(IReadOnlyCollection<NavNode> nodes, TileWalker walker, Func<int, int, int, bool> keep)
    {
        var byName = nodes.ToDictionary(node => node.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            foreach (var other in node.Connects.Select(name => byName[name]))
            {
                if (NavGates.KindEitherWay(node, other) != NavGateKind.None)
                {
                    continue;
                }

                Assert.True(
                    NavMetric.Chebyshev(node.Location, other.Location) <= TerrainRoute.RoadSpacing &&
                    WalkLine.Reaches(walker, node.Location, other.Location, keep) &&
                    WalkLine.Reaches(walker, other.Location, node.Location, keep),
                    $"edge {node.Location} -> {other.Location} is no straight walk both ways within a road leg"
                );
            }
        }
    }
}
