using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SosariaAI.Navigation;

/// <summary>
/// Shared health of graph edges. A walk that fails on the hop between two nodes marks that
/// edge, and every later search pays extra to use it, so a town stops sending people into
/// the same blocked gap. A mark fades, so a crowd or a shut door that clears is used again.
/// The world thread writes; path workers read the snapshot published with each write. A graph
/// a rebuild or a boot check replaced takes its marks with it: the table holds no graph alive.
/// </summary>
public static class EdgeHealth
{
    private static readonly object WriteGate = new();
    private static readonly ConditionalWeakTable<NavGraph, EdgeHealthSnapshot> Published = new();

    /// <summary>The marks a search on <paramref name="graph"/> pays for, or null when it has none.</summary>
    public static EdgeHealthSnapshot For(NavGraph graph) =>
        graph != null && Published.TryGetValue(graph, out var snapshot) ? snapshot : null;

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

            Published.AddOrUpdate(graph, new EdgeHealthSnapshot(marks, (old?.Version ?? 0) + 1));
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
    }

    public IReadOnlyDictionary<long, EdgeMark> Marks { get; }

    /// <summary>Changes with every new mark, so a cached search knows it is stale.</summary>
    public int Version { get; }

    /// <summary>
    /// The soonest a mark still active at <paramref name="now"/> fades, or <see cref="long.MaxValue"/>
    /// when none is: a search cached at <paramref name="now"/> is stale after it. A mark that
    /// faded before is already left out of the search, and counting it stored every cached
    /// search stale once the snapshot's first mark faded, until the next failed walk.
    /// </summary>
    public long NextExpiryAfter(long now)
    {
        var soonest = long.MaxValue;

        foreach (var mark in Marks.Values)
        {
            if (EdgeHealthRules.Active(now, mark.Until))
            {
                soonest = Math.Min(soonest, mark.Until);
            }
        }

        return soonest;
    }

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
