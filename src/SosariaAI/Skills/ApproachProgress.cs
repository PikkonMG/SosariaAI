namespace SosariaAI.Skills;

/// <summary>
/// Stuck guard for a walk leg or a chase. A walker that keeps lowering its best distance
/// to the goal is making progress, even on a detour. One that paces against a wall never
/// does. After <see cref="GiveUpTicks"/> ticks without a new best distance the walk is
/// given up.
/// </summary>
public sealed class ApproachProgress
{
    public const int GiveUpTicks = 40;

    private double _bestDistance = double.MaxValue;
    private int _ticksWithoutProgress;

    /// <summary>Ticks since the last new best distance: the stuck ladder climbs on it.</summary>
    public int TicksWithoutProgress => _ticksWithoutProgress;

    public void Reset()
    {
        _bestDistance = double.MaxValue;
        _ticksWithoutProgress = 0;
    }

    /// <summary>Returns true when the walker should give up on this goal.</summary>
    public bool Observe(double distance)
    {
        if (distance < _bestDistance)
        {
            _bestDistance = distance;
            _ticksWithoutProgress = 0;
            return false;
        }

        return ++_ticksWithoutProgress >= GiveUpTicks;
    }
}
