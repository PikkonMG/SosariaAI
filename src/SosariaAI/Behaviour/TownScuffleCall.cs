using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Behaviour;

/// <summary>
/// A call to a town street: the fighters of each side called to one street spot, the gather
/// time, and those of them who dropped out on the way. The scuffle starts with those who came
/// (<see cref="TownScuffleRules.CallState"/>); nobody is added after the call. In memory only: a
/// restart ends every call.
/// </summary>
public sealed class TownScuffleCall
{
    private readonly List<Serial> _order;
    private readonly List<Serial> _chaos;

    public TownScuffleCall(ScuffleTown town, Point3D spot, List<Serial> order, List<Serial> chaos, DateTime ends)
    {
        Town = town;
        Spot = spot;
        _order = order;
        _chaos = chaos;
        Ends = ends;
    }

    public ScuffleTown Town { get; }

    /// <summary>The street spot the fighters gather at: under the guards and clear of every place of peace.</summary>
    public Point3D Spot { get; }

    /// <summary>The Order fighters still called.</summary>
    public IReadOnlyList<Serial> Order => _order;

    /// <summary>The Chaos fighters still called.</summary>
    public IReadOnlyList<Serial> Chaos => _chaos;

    /// <summary>At this time the call starts with those who came, or lapses.</summary>
    public DateTime Ends { get; }

    public bool TimeUp(DateTime now) => now >= Ends;

    /// <summary>Takes a fighter off the call for good.</summary>
    public void Drop(Serial fighter)
    {
        _order.Remove(fighter);
        _chaos.Remove(fighter);
    }
}
