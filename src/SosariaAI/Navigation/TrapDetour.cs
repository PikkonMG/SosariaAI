using System.Collections.Generic;
using Server;
using SosariaAI.Skills;

namespace SosariaAI.Navigation;

/// <summary>
/// The way round the armed traps for one walker. The engine's pathfinder knows nothing of
/// traps, so a walk whose way passes near one takes the walker's own trap-aware steps
/// (<see cref="FloorSteps.Around"/> with <see cref="TileWalker.SafeStep"/>) instead, and
/// the engine path again where no trap is near. Where no safe way exists within one leg, a
/// corridor trapped wall to wall, the engine path walks the leg and crosses. World thread only.
/// </summary>
public sealed class TrapDetour
{
    /// <summary>A trap this far outside the box round a walker and its goal cannot lie on its way.</summary>
    public const int WayMargin = 4;

    /// <summary>A goal further than one pathfinder leg is beyond the step search; the engine path walks toward it.</summary>
    public const int MaxGoalTiles = NavLimits.MaxLegDistance;

    private Map _map;
    private Point3D _goal;
    private int _range;
    private IReadOnlyList<Direction> _steps;
    private int _next;
    private bool _noSafeWay;

    /// <summary>
    /// The next step round the traps toward <paramref name="goal"/>, or null when the engine
    /// path should take this step: no trap near the way, the goal beyond one leg, already in
    /// range, or no safe way to it.
    /// </summary>
    public Direction? Next(Map map, Point3D at, Point3D goal, int range)
    {
        if (map != _map || goal != _goal || range != _range)
        {
            _map = map;
            _goal = goal;
            _range = range;
            _steps = null;
            _noSafeWay = false;
        }

        if (_noSafeWay)
        {
            return null;
        }

        if (_steps != null && _next < _steps.Count)
        {
            return _steps[_next];
        }

        _steps = null;
        var traps = TrapTiles.For(map);

        if (traps.Count == 0 || NavMetric.Chebyshev(at, goal) > MaxGoalTiles || !traps.AnyNear(at, goal, WayMargin))
        {
            return null;
        }

        var steps = FloorSteps.Around(at, goal, range, Standable.Walker(map).SafeStep);

        if (steps == null)
        {
            _noSafeWay = true;
            return null;
        }

        if (steps.Count == 0)
        {
            return null;
        }

        _steps = steps;
        _next = 0;
        return _steps[0];
    }

    /// <summary>The step <see cref="Next"/> gave was taken.</summary>
    public void Stepped() => _next++;

    /// <summary>The step <see cref="Next"/> gave was blocked, by a body or a shut door: the way is searched again.</summary>
    public void Blocked() => _steps = null;
}
