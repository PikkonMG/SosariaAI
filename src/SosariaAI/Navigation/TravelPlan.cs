using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

public readonly record struct TravelStep(string Node, Point3D Location, NavGateKind ArrivalGate);

public static class TravelPlan
{
    public static Point3D? ExitToward(IReadOnlyList<TravelStep> steps, int arrivalIndex, Point3D? arrival)
    {
        if (steps != null)
        {
            for (var i = arrivalIndex + 1; i < steps.Count; i++)
            {
                // Another gate pad gives a bad exit direction. Aim at the first
                // walking leg after the gate network instead.
                if (steps[i].ArrivalGate == NavGateKind.None)
                {
                    return steps[i].Location;
                }
            }
        }

        return arrival;
    }

    public static IReadOnlyList<TravelStep> From(NavGraph graph, IReadOnlyList<string> names)
    {
        if (graph == null || names == null || names.Count == 0)
        {
            return [];
        }

        var steps = new List<TravelStep>(names.Count);

        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];

            if (!graph.TryGetNode(name, out var node))
            {
                return [];
            }

            // Only a recorded gate may move a character. A long leg with no gate is a
            // walk that fails honestly, never a teleport from bare ground.
            var arrival = i > 0 ? graph.GateKind(names[i - 1], name) : NavGateKind.None;
            steps.Add(new TravelStep(name, node.Location, arrival));
        }

        return steps;
    }

    /// <summary>
    /// True when the walked road of a plan passes near a place in <paramref name="spots"/>
    /// (<see cref="NavSearch.IsLegNearAny"/>): the walk from <paramref name="from"/> to the first
    /// node and each walked leg after it. A moongate or pad carries the walker over its leg, so
    /// only the spot the hop lands on counts. A walker that stands near a spot already reads
    /// only the road that takes it nearer (<see cref="NavSearch.ComesNearer"/>): a road away
    /// from the place it ran from is no road past it. Pure: the world thread reads a finished plan.
    /// </summary>
    public static bool WalksNear(Point3D from, IReadOnlyList<TravelStep> steps, IReadOnlyList<Point3D> spots)
    {
        if (steps == null || spots is not { Count: > 0 })
        {
            return false;
        }

        var legStart = from;

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var walkedFrom = step.ArrivalGate == NavGateKind.None ? legStart : step.Location;

            if (NavSearch.IsLegNearAny(walkedFrom, step.Location, spots, from))
            {
                return true;
            }

            legStart = step.Location;
        }

        return false;
    }

    /// <summary>A tile route as a plan walked on foot all the way, for <see cref="WalksNear"/>. Pure.</summary>
    public static IReadOnlyList<TravelStep> OnFoot(IReadOnlyList<Point3D> route)
    {
        var steps = new TravelStep[route?.Count ?? 0];

        for (var i = 0; i < steps.Length; i++)
        {
            steps[i] = new TravelStep(null, route[i], NavGateKind.None);
        }

        return steps;
    }

    /// <summary>
    /// The plan without the teleporter pads it only walks across. A pad walked onto carries
    /// the walker off whether the plan takes it or not: the Orc Cave plan from the landing
    /// to its first hall went by way of the exit pad beside the landing, and each walker
    /// stepped on it and stood outside again. A pad the plan takes, the start and the goal
    /// stay.
    /// </summary>
    public static IReadOnlyList<TravelStep> WithoutPassThroughPads(NavGraph graph, IReadOnlyList<TravelStep> steps)
    {
        if (graph == null || steps is not { Count: > 2 })
        {
            return steps;
        }

        var kept = new List<TravelStep>(steps.Count) { steps[0] };

        for (var i = 1; i < steps.Count - 1; i++)
        {
            if (steps[i].ArrivalGate != NavGateKind.None || steps[i + 1].ArrivalGate != NavGateKind.None ||
                !IsPad(graph, steps[i].Node))
            {
                kept.Add(steps[i]);
            }
        }

        kept.Add(steps[^1]);
        return kept;
    }

    /// <summary>True when a teleporter leaves from the node: standing on it carries a person off.</summary>
    public static bool IsPad(NavGraph graph, string node)
    {
        var next = graph?.Neighbors(node) ?? [];

        for (var i = 0; i < next.Count; i++)
        {
            if (graph.IsGate(node, next[i]) && graph.Travels(node, next[i]) &&
                graph.GateKind(node, next[i]) == NavGateKind.Teleporter)
            {
                return true;
            }
        }

        return false;
    }
}
