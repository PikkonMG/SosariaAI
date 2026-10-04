using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// A short memory of places. A note that lands near an old spot refreshes it
/// instead of adding one, old spots expire, and only the newest few are kept.
/// Shared by danger spots and unreachable goals.
/// </summary>
public sealed class SpotMemory
{
    private const int MemoryMinutes = 10;

    /// <summary>How long a place is remembered.</summary>
    public static readonly TimeSpan Memory = TimeSpan.FromMinutes(MemoryMinutes);

    /// <summary>Only the newest places are kept. Older ones are the least useful.</summary>
    public const int MaxSpots = 4;

    /// <summary>A new spot this close to an old one refreshes the old one instead.</summary>
    public const int SameSpotTiles = 8;

    private readonly List<(Point3D Location, DateTime Until)> _spots = [];

    /// <summary>Remember a place, refreshing an old spot that sits close to it.</summary>
    public void Note(Point3D location, DateTime now)
    {
        Forget(now);

        if (TryRefresh(location, now))
        {
            return;
        }

        if (_spots.Count >= MaxSpots)
        {
            _spots.RemoveAt(0);
        }

        _spots.Add((location, now + Memory));
    }

    /// <summary>Refresh the live spot near this place. False when none is close.</summary>
    public bool TryRefresh(Point3D location, DateTime now)
    {
        for (var i = 0; i < _spots.Count; i++)
        {
            if (NavMetric.Chebyshev(_spots[i].Location, location) <= SameSpotTiles)
            {
                _spots[i] = (location, now + Memory);
                return true;
            }
        }

        return false;
    }

    /// <summary>The spots still remembered at this moment.</summary>
    public IReadOnlyList<Point3D> Active(DateTime now)
    {
        Forget(now);

        var found = new List<Point3D>(_spots.Count);

        for (var i = 0; i < _spots.Count; i++)
        {
            found.Add(_spots[i].Location);
        }

        return found;
    }

    /// <summary>The spots still remembered at this moment, each with the time it was last noted or refreshed.</summary>
    public IReadOnlyList<(Point3D Location, DateTime NotedAt)> Noted(DateTime now)
    {
        Forget(now);

        var found = new List<(Point3D Location, DateTime NotedAt)>(_spots.Count);

        for (var i = 0; i < _spots.Count; i++)
        {
            found.Add((_spots[i].Location, _spots[i].Until - Memory));
        }

        return found;
    }

    public void Forget(DateTime now) => _spots.RemoveAll(spot => spot.Until <= now);
}
