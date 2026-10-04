using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Navigation;

public sealed class NavGateLink
{
    private const string KindTeleporter = "teleporter";
    private const string KindMoongate = "moongate";

    [JsonPropertyName("to")]
    public string To { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonIgnore]
    public NavGateKind ParsedKind => ParseKind(Kind);

    public static string KindName(NavGateKind kind) =>
        kind switch
        {
            NavGateKind.Teleporter => KindTeleporter,
            NavGateKind.Moongate => KindMoongate,
            _ => null
        };

    public static NavGateKind ParseKind(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return NavGateKind.None;
        }

        if (kind.Equals(KindTeleporter, StringComparison.OrdinalIgnoreCase))
        {
            return NavGateKind.Teleporter;
        }

        if (kind.Equals(KindMoongate, StringComparison.OrdinalIgnoreCase))
        {
            return NavGateKind.Moongate;
        }

        return NavGateKind.None;
    }
}

/// <summary>
/// The gates of a graph as the nav file stores them. A node's <c>gates</c> list names the
/// nodes a gate on it carries a person to. A two-way gate is listed on both ends; a one-way
/// pad only on the end that sends. The walking links (<c>connects</c>) join both ends
/// either way, so the graph stays one piece and the search reads the direction off the gates.
/// </summary>
public static class NavGates
{
    /// <summary>A gate a person can take both ways: a public moongate, or a pad with a pad back.</summary>
    public static void Add(NavNode from, NavNode to, NavGateKind kind)
    {
        if (!Joinable(from, to))
        {
            return;
        }

        AddGate(from, to.Name, kind);
        AddGate(to, from.Name, kind);
    }

    /// <summary>
    /// A gate that carries a person from <paramref name="from"/> to <paramref name="to"/> and
    /// never back: a one-way teleporter pad. Moonglow's teleporter hub at (4442,1122) is
    /// only a landing, and a plan that stepped "back" through it found no pad there.
    /// </summary>
    public static void AddOneWay(NavNode from, NavNode to, NavGateKind kind)
    {
        if (!Joinable(from, to))
        {
            return;
        }

        AddGate(from, to.Name, kind);
        to.Connects ??= [];
        AddConnect(to.Connects, from.Name);
    }

    /// <summary>The gate between two nodes in either direction, or none.</summary>
    public static NavGateKind KindEitherWay(NavNode a, NavNode b)
    {
        if (a == null || b == null)
        {
            return NavGateKind.None;
        }

        var kind = KindBetween(a, b.Name);
        return kind != NavGateKind.None ? kind : KindBetween(b, a.Name);
    }

    private static bool Joinable(NavNode from, NavNode to) =>
        from != null && to != null && !ReferenceEquals(from, to) &&
        !string.IsNullOrWhiteSpace(from.Name) && !string.IsNullOrWhiteSpace(to.Name) &&
        !from.Name.Equals(to.Name, StringComparison.OrdinalIgnoreCase);

    public static NavGateKind KindBetween(NavNode from, string toName)
    {
        if (from?.Gates == null || string.IsNullOrWhiteSpace(toName))
        {
            return NavGateKind.None;
        }

        var gates = from.Gates;

        for (var i = 0; i < gates.Count; i++)
        {
            var gate = gates[i];

            if (gate?.To != null && gate.To.Equals(toName, StringComparison.OrdinalIgnoreCase))
            {
                return gate.ParsedKind;
            }
        }

        return NavGateKind.None;
    }

    /// <summary>
    /// Takes away the teleporter gate from <paramref name="from"/> to the node named
    /// <paramref name="to"/>, and the link between them unless a gate still leads back from
    /// that node. False when no such gate leaves <paramref name="from"/>.
    /// </summary>
    public static bool RemoveTeleporter(NavNode from, string to, IReadOnlyDictionary<string, NavNode> byName)
    {
        var gates = from?.Gates;

        if (gates == null || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        var removed = gates.RemoveAll(gate =>
            gate?.To != null && gate.To.Equals(to, StringComparison.OrdinalIgnoreCase) &&
            gate.ParsedKind == NavGateKind.Teleporter
        ) > 0;

        if (!removed)
        {
            return false;
        }

        if (gates.Count == 0)
        {
            from.Gates = null;
        }

        if (!byName.TryGetValue(to, out var landing) || KindBetween(landing, from.Name) == NavGateKind.None)
        {
            RemoveConnect(from, to);

            if (landing != null)
            {
                RemoveConnect(landing, from.Name);
            }
        }

        return true;
    }

    private static void RemoveConnect(NavNode node, string other)
    {
        node.Connects?.RemoveAll(name => name != null && name.Equals(other, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Makes every walking link longer than a leg (<see cref="NavMetric.WithinLegCap"/>) a
    /// teleporter: the game pathfinder cannot walk it, so only a pad joins its ends. One scan
    /// both finds such links and gates them; a graph with none is left as it is.
    /// </summary>
    public static void InferLongConnects(IEnumerable<NavNode> nodes)
    {
        if (nodes == null)
        {
            return;
        }

        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Name))
            {
                continue;
            }

            byName.TryAdd(node.Name, node);
        }

        foreach (var node in byName.Values)
        {
            var connects = node.Connects;

            if (connects == null)
            {
                continue;
            }

            for (var i = 0; i < connects.Count; i++)
            {
                var otherName = connects[i];

                if (string.IsNullOrWhiteSpace(otherName))
                {
                    continue;
                }

                if (!byName.TryGetValue(otherName, out var other))
                {
                    continue;
                }

                if (KindEitherWay(node, other) != NavGateKind.None)
                {
                    continue;
                }

                if (NavMetric.WithinLegCap(node.Location, other.Location))
                {
                    continue;
                }

                Add(node, other, NavGateKind.Teleporter);
            }
        }
    }

    private static void AddGate(NavNode from, string toName, NavGateKind kind)
    {
        from.Connects ??= [];
        AddConnect(from.Connects, toName);

        if (HasGateTo(from.Gates, toName))
        {
            return;
        }

        from.Gates ??= [];
        from.Gates.Add(
            new NavGateLink
            {
                To = toName,
                Kind = NavGateLink.KindName(kind)
            }
        );
    }

    private static void AddConnect(List<string> connects, string other)
    {
        for (var i = 0; i < connects.Count; i++)
        {
            if (connects[i] != null && connects[i].Equals(other, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        connects.Add(other);
    }

    private static bool HasGateTo(List<NavGateLink> gates, string toName)
    {
        if (gates == null)
        {
            return false;
        }

        for (var i = 0; i < gates.Count; i++)
        {
            var gate = gates[i];

            if (gate?.To != null && gate.To.Equals(toName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
