using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Combat;

/// <summary>
/// The way a character came: a spot every few tiles of its last walk. Every spot on it was
/// stood on, so it is ground a runner knows it can walk, the way a player backs out of a
/// cave the way it came in. Oldest spots drop off first. A jump no walk makes (a gate, a
/// recall, another facet) starts a new trail. World thread only.
/// </summary>
public sealed class StepTrail
{
    /// <summary>Spots kept.</summary>
    public const int Capacity = 16;

    /// <summary>A new spot is kept only this far from the last one.</summary>
    public const int SpacingTiles = 3;

    /// <summary>A move this long between two notes was not walked.</summary>
    public const int JumpTiles = 12;

    private readonly List<Point3D> _spots = new(Capacity);
    private Map _map;

    public IReadOnlyList<Point3D> Spots => _spots;

    /// <summary>Notes where the walker stands now; a spot too near the last one is skipped.</summary>
    public void Note(Map map, Point3D at)
    {
        if (map != _map || _spots.Count > 0 && NavMetric.Chebyshev(_spots[^1], at) > JumpTiles)
        {
            _spots.Clear();
            _map = map;
        }

        if (_spots.Count > 0 && NavMetric.Chebyshev(_spots[^1], at) < SpacingTiles)
        {
            return;
        }

        if (_spots.Count == Capacity)
        {
            _spots.RemoveAt(0);
        }

        _spots.Add(at);
    }
}
