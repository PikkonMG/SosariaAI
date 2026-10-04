using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>A guarded town a scuffle is fought in: its facet and the guarded region's name.</summary>
public readonly record struct ScuffleTown(Map Map, string Name);

/// <summary>
/// One Order against Chaos scuffle on a town street: the fighters of each side, chosen at the
/// start and never more, and those of them who are out of it. A fighter who is out never comes
/// back in. In memory only: a restart ends every scuffle.
/// </summary>
public sealed class TownScuffle
{
    private readonly HashSet<Serial> _out = [];

    public TownScuffle(ScuffleTown town, Serial[] order, Serial[] chaos, DateTime started, TimeSpan limit)
    {
        Town = town;
        Order = order;
        Chaos = chaos;
        Started = started;
        Limit = limit;
    }

    public ScuffleTown Town { get; }

    public IReadOnlyList<Serial> Order { get; }

    public IReadOnlyList<Serial> Chaos { get; }

    public DateTime Started { get; }

    public TimeSpan Limit { get; }

    public int Fighters => Order.Count + Chaos.Count;

    public bool IsOrder(Serial fighter) => Holds(Order, fighter);

    public bool IsChaos(Serial fighter) => Holds(Chaos, fighter);

    public bool Contains(Serial fighter) => IsOrder(fighter) || IsChaos(fighter);

    public bool IsOut(Serial fighter) => _out.Contains(fighter);

    public void MarkOut(Serial fighter)
    {
        if (Contains(fighter))
        {
            _out.Add(fighter);
        }
    }

    /// <summary>The other side of this fighter.</summary>
    public IReadOnlyList<Serial> FoesOf(Serial fighter) => IsOrder(fighter) ? Chaos : Order;

    /// <summary>True while both are still in and stand on opposite sides: their blows belong to this scuffle.</summary>
    public bool Opposes(Serial first, Serial second) =>
        !IsOut(first) && !IsOut(second) && (IsOrder(first) && IsChaos(second) || IsChaos(first) && IsOrder(second));

    /// <summary>The fighters of a side still in.</summary>
    public int Standing(IReadOnlyList<Serial> side)
    {
        var count = 0;

        for (var i = 0; i < side.Count; i++)
        {
            count += IsOut(side[i]) ? 0 : 1;
        }

        return count;
    }

    public bool TimeUp(DateTime now) => now - Started >= Limit;

    private static bool Holds(IReadOnlyList<Serial> side, Serial fighter)
    {
        for (var i = 0; i < side.Count; i++)
        {
            if (side[i] == fighter)
            {
                return true;
            }
        }

        return false;
    }
}
