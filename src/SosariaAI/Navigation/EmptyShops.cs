using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

/// <summary>
/// Shop markers found with nobody behind the counter, passed over until a spawner has had
/// time to stand a vendor there again. A buyer walked to the same empty marker in Skara Brae
/// again and again and failed at it each time. Pure.
/// </summary>
public sealed class EmptyShops
{
    private readonly Dictionary<(string Facet, Point3D Marker), DateTime> _until = new();

    /// <summary>The marker on <paramref name="facet"/> is passed over until <paramref name="until"/>.</summary>
    public void Note(string facet, Point3D marker, DateTime until) => _until[(facet ?? string.Empty, marker)] = until;

    /// <summary>True while the marker is passed over; a memory past its time is forgotten.</summary>
    public bool IsEmpty(string facet, Point3D marker, DateTime now)
    {
        var key = (facet ?? string.Empty, marker);

        if (!_until.TryGetValue(key, out var until))
        {
            return false;
        }

        if (until > now)
        {
            return true;
        }

        _until.Remove(key);
        return false;
    }
}
