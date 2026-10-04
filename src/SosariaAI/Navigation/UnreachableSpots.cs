using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// Places the router just proved a character cannot reach — a work camp or shop
/// with no node near it, or a bound start with no path to it. Remembered briefly
/// and never saved: nav data changes, and a place that frees up gets retried.
/// </summary>
public sealed class UnreachableSpots
{
    private readonly SpotMemory _spots = new();

    public void Note(Point3D location, DateTime now) => _spots.Note(location, now);

    /// <summary>The spots still remembered at this moment.</summary>
    public IReadOnlyList<Point3D> Active(DateTime now) => _spots.Active(now);
}
