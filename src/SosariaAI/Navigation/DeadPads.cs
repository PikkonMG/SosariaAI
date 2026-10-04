using System;
using System.Collections.Generic;

namespace SosariaAI.Navigation;

/// <summary>
/// The teleporter gates a graph must lose when a pad carries nobody. A plan through a pad no
/// step sets off (<see cref="GatePad.NeverFires(Server.Map, TileWalker, int, int)"/>) ends on
/// the pad, so its gate goes. When that pad was the only way back from where it stands, the
/// place is left with no way out, and every one-way pad into it goes too: the south Jhelom
/// island is reached by the pad at (1419,3832) and left only by the dead pad at (1406,3996),
/// and walkers carried there stood on the dead pad for hours. The mainland is the largest
/// piece of walking links. Pure over the nodes.
/// </summary>
public static class DeadPads
{
    /// <summary>
    /// Drops the gates of every pad <paramref name="neverFires"/> calls dead, then the pads
    /// into the places whose only way back to the mainland went through a dead pad.
    /// </summary>
    /// <returns>How many dead pad gates went, and how many gates into a place left with no way out.</returns>
    public static (int Dead, int IntoSealed) Drop(IReadOnlyCollection<NavNode> nodes, Func<NavNode, bool> neverFires)
    {
        if (nodes == null || neverFires == null)
        {
            return (0, 0);
        }

        var byName = ByName(nodes);
        var mainland = Mainland(byName);
        var returnedBefore = WaysBack(byName, mainland);
        var dead = 0;

        foreach (var node in byName.Values)
        {
            var landings = Teleporters(node);

            if (landings.Count > 0 && neverFires(node))
            {
                dead += RemoveAll(node, landings, byName);
            }
        }

        if (dead == 0)
        {
            return (0, 0);
        }

        var returnsNow = WaysBack(byName, mainland);
        var intoSealed = 0;

        foreach (var node in byName.Values)
        {
            if (!returnsNow.Contains(node.Name))
            {
                continue;
            }

            var sealedLandings = Teleporters(node).FindAll(to => returnedBefore.Contains(to) && !returnsNow.Contains(to));
            intoSealed += RemoveAll(node, sealedLandings, byName);
        }

        return (dead, intoSealed);
    }

    private static Dictionary<string, NavNode> ByName(IReadOnlyCollection<NavNode> nodes)
    {
        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node != null && !string.IsNullOrWhiteSpace(node.Name))
            {
                byName.TryAdd(node.Name, node);
            }
        }

        return byName;
    }

    /// <summary>The landings of the teleporter pads that leave a node.</summary>
    private static List<string> Teleporters(NavNode node)
    {
        var landings = new List<string>();

        foreach (var gate in node.Gates ?? [])
        {
            if (gate?.To != null && gate.ParsedKind == NavGateKind.Teleporter)
            {
                landings.Add(gate.To);
            }
        }

        return landings;
    }

    private static int RemoveAll(NavNode node, List<string> landings, Dictionary<string, NavNode> byName)
    {
        var removed = 0;

        foreach (var landing in landings)
        {
            if (NavGates.RemoveTeleporter(node, landing, byName))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>The largest piece joined by walking links alone: no gate of any kind counts.</summary>
    private static HashSet<string> Mainland(Dictionary<string, NavNode> byName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var largest = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start in byName.Values)
        {
            if (seen.Contains(start.Name))
            {
                continue;
            }

            var piece = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start.Name };
            var queue = new Queue<NavNode>();
            queue.Enqueue(start);
            seen.Add(start.Name);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                foreach (var otherName in node.Connects ?? [])
                {
                    if (seen.Contains(otherName) || !byName.TryGetValue(otherName, out var other) ||
                        NavGates.KindEitherWay(node, other) != NavGateKind.None)
                    {
                        continue;
                    }

                    seen.Add(other.Name);
                    piece.Add(other.Name);
                    queue.Enqueue(other);
                }
            }

            if (piece.Count > largest.Count)
            {
                largest = piece;
            }
        }

        return largest;
    }

    /// <summary>
    /// The nodes a person can go from to the mainland: on foot, by a two-way gate, or by a
    /// one-way pad taken its own way.
    /// </summary>
    private static HashSet<string> WaysBack(Dictionary<string, NavNode> byName, HashSet<string> mainland)
    {
        var into = new Dictionary<string, List<NavNode>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in byName.Values)
        {
            foreach (var otherName in node.Connects ?? [])
            {
                if (!byName.TryGetValue(otherName, out var other) || !Goes(node, other))
                {
                    continue;
                }

                if (!into.TryGetValue(other.Name, out var from))
                {
                    into[other.Name] = from = [];
                }

                from.Add(node);
            }
        }

        var back = new HashSet<string>(mainland, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(mainland);

        while (queue.Count > 0)
        {
            foreach (var node in into.GetValueOrDefault(queue.Dequeue()) ?? [])
            {
                if (back.Add(node.Name))
                {
                    queue.Enqueue(node.Name);
                }
            }
        }

        return back;
    }

    /// <summary>True when a person goes from one linked node to the other: every link but a one-way pad walked backwards.</summary>
    private static bool Goes(NavNode from, NavNode to) =>
        NavGates.KindBetween(from, to.Name) != NavGateKind.None || NavGates.KindBetween(to, from.Name) == NavGateKind.None;
}
