using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// Places a character recently ran from. Route planning steers round them for a while,
/// so a walk home does not lead straight back to the monster the character just fled.
/// The memory is short and not saved: monsters move, and a restart starts clean.
/// </summary>
public sealed class DangerSpots
{

    private readonly SpotMemory _spots = new();
    private readonly List<DateTime> _runs = [];

    public void Note(Point3D location, DateTime now)
    {
        ForgetRuns(now);
        _runs.Add(now);
        _spots.Note(location, now);
    }

    /// <summary>
    /// The same threat is still in sight. Refresh the remembered place without counting
    /// another run: one chase is one run, not one per score tick. A place not yet
    /// remembered is a new escape, so it does count.
    /// </summary>
    public void NoteSighting(Point3D location, DateTime now)
    {
        ForgetRuns(now);
        _spots.Forget(now);

        if (!_spots.TryRefresh(location, now))
        {
            Note(location, now);
        }
    }

    /// <summary>The spots still remembered at this moment.</summary>
    public IReadOnlyList<Point3D> Active(DateTime now) => _spots.Active(now);

    /// <summary>
    /// The spots still remembered, each with the time the threat was last noted there: every
    /// run and every sighting renews it (<see cref="NoteSighting"/>). A walker reads how long a
    /// place has lain quiet from it before it walks that road again.
    /// </summary>
    public IReadOnlyList<(Point3D Location, DateTime NotedAt)> Sightings(DateTime now) => _spots.Noted(now);

    /// <summary>How many times the character ran within the remembered time.</summary>
    public int RecentRuns(DateTime now)
    {
        ForgetRuns(now);
        return _runs.Count;
    }

    private void ForgetRuns(DateTime now) => _runs.RemoveAll(ran => ran + SpotMemory.Memory <= now);
}
