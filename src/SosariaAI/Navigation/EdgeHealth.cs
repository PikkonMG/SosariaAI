using System;
using System.Collections.Generic;

namespace SosariaAI.Navigation;

/// <summary>
/// Shared health of graph edges. A walk that fails on the hop between two nodes marks that
/// edge, and every later search pays extra to use it, so a town stops sending people into
/// the same blocked gap. A mark fades, so a crowd or a shut door that clears is used again.
/// The world thread writes; path workers read the snapshot published with each write.
/// </summary>
public static class EdgeHealth
{
    private static readonly object WriteGate = new();
    private static volatile Dictionary<NavGraph, EdgeHealthSnapshot> _published = new();

    /// <summary>The marks a search on <paramref name="graph"/> pays for, or null when it has none.</summary>
    public static EdgeHealthSnapshot For(NavGraph graph) =>
        graph != null && _published.TryGetValue(graph, out var snapshot) ? snapshot : null;

    /// <summary>A walk from node <paramref name="from"/> to node <paramref name="to"/> failed.</summary>
    public static void NoteFailure(NavGraph graph, string from, string to)
    {
        var a = graph?.SearchIndex(from) ?? -1;
        var b = graph?.SearchIndex(to) ?? -1;

        if (a < 0 || b < 0 || a == b)
        {
            return;
        }

        var now = Environment.TickCount64;
        var key = EdgeHealthRules.Key(a, b);

        lock (WriteGate)
        {
            var old = For(graph);
            var marks = new Dictionary<long, EdgeMark>();

            if (old != null)
            {
                foreach (var (edge, mark) in old.Marks)
                {
                    if (EdgeHealthRules.Active(now, mark.Until))
                    {
                        marks[edge] = mark;
                    }
                }
            }

            var failures = marks.TryGetValue(key, out var prior) ? prior.Failures : 0;
            marks[key] = EdgeHealthRules.After(failures, now);

            var published = new Dictionary<NavGraph, EdgeHealthSnapshot>(_published)
            {
                [graph] = new EdgeHealthSnapshot(marks, (old?.Version ?? 0) + 1)
            };
            _published = published;
        }
    }
}

/// <summary>One marked edge: how often a walk failed on it and when the mark fades.</summary>
public readonly record struct EdgeMark(int Failures, long Until);

/// <summary>A frozen set of marks for one graph. Path workers read it without a lock.</summary>
public sealed class EdgeHealthSnapshot
{
    public EdgeHealthSnapshot(IReadOnlyDictionary<long, EdgeMark> marks, int version)
    {
        Marks = marks;
        Version = version;
        NextExpiry = long.MaxValue;

        foreach (var mark in marks.Values)
        {
            NextExpiry = Math.Min(NextExpiry, mark.Until);
        }
    }

    public IReadOnlyDictionary<long, EdgeMark> Marks { get; }

    /// <summary>Changes with every new mark, so a cached search knows it is stale.</summary>
    public int Version { get; }

    /// <summary>The soonest a mark fades: a cached search is stale after it.</summary>
    public long NextExpiry { get; }

    /// <summary>Extra cost in tiles for the edge between two search indexes at <paramref name="now"/>.</summary>
    public double PenaltyTiles(int a, int b, long now) =>
        Marks.TryGetValue(EdgeHealthRules.Key(a, b), out var mark) && EdgeHealthRules.Active(now, mark.Until)
            ? EdgeHealthRules.PenaltyTiles(mark.Failures)
            : 0;
}

/// <summary>Pure rules behind <see cref="EdgeHealth"/>.</summary>
public static class EdgeHealthRules
{
    /// <summary>Extra tiles one failed walk adds to an edge: a short detour wins, a long one does not.</summary>
    public const double PenaltyTilesPerFailure = 150;

    /// <summary>Failures past this add nothing: with no other way the edge is still used.</summary>
    public const int MaxCountedFailures = 4;

    /// <summary>How long a mark lasts after its latest failure.</summary>
    public const int MarkLifetimeMinutes = 10;

    public static readonly long MarkLifetimeMs = (long)TimeSpan.FromMinutes(MarkLifetimeMinutes).TotalMilliseconds;

    private const int IndexBits = 32;

    /// <summary>One key for both directions of an edge.</summary>
    public static long Key(int a, int b) =>
        a < b ? ((long)a << IndexBits) | (uint)b : ((long)b << IndexBits) | (uint)a;

    public static bool Active(long now, long until) => now < until;

    public static double PenaltyTiles(int failures) =>
        Math.Clamp(failures, 0, MaxCountedFailures) * PenaltyTilesPerFailure;

    /// <summary>The mark after one more failure at <paramref name="now"/>.</summary>
    public static EdgeMark After(int failuresSoFar, long now) =>
        new(Math.Min(failuresSoFar + 1, MaxCountedFailures), now + MarkLifetimeMs);
}
