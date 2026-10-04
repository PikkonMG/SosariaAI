namespace SosariaAI.Skills;

public sealed class TreeProgress
{
    public const int TreeGiveUpTicks = 10;

    private const int UnsetLogCount = -1;

    private int _ticksWithoutGrowth;
    private int _lastLogCount = UnsetLogCount;

    public void Reset()
    {
        _ticksWithoutGrowth = 0;
        _lastLogCount = UnsetLogCount;
    }

    public bool Observe(int logCount, bool harvesting)
    {
        if (_lastLogCount != UnsetLogCount && logCount > _lastLogCount)
        {
            _lastLogCount = logCount;
            _ticksWithoutGrowth = 0;
            return false;
        }

        var isFirstSample = _lastLogCount == UnsetLogCount;
        _lastLogCount = logCount;

        if (harvesting)
        {
            return false;
        }

        // First sample after Reset is a baseline only. Do not increment.
        if (isFirstSample)
        {
            return false;
        }

        _ticksWithoutGrowth++;
        return _ticksWithoutGrowth >= TreeGiveUpTicks;
    }
}
