using System.Diagnostics;

namespace SosariaAI.Navigation;

/// <summary>
/// Route planning on the game thread is paid from a small time budget. When a whole town
/// sets out at once, as after a boot or First Time Setup, every tile route would run in
/// the same few seconds and freeze the shard. A traveler over the budget waits a heartbeat
/// and plans then, so people leave a few at a time and the world keeps moving.
/// </summary>
public static class PlanBudget
{
    /// <summary>The window the budget refills over.</summary>
    public const double WindowMs = 100;

    /// <summary>Planning time allowed per window: a sixth of the game thread at most.</summary>
    public const double BudgetMs = 15;

    private static long _windowStart;
    private static double _spentMs;

    /// <summary>True when a plan may run now. The first plan of a window always may.</summary>
    public static bool TryEnter()
    {
        var now = Stopwatch.GetTimestamp();

        if (PlanBudgetRules.WindowOver(ElapsedMs(_windowStart, now), WindowMs))
        {
            _windowStart = now;
            _spentMs = 0;
        }

        return PlanBudgetRules.MayPlan(_spentMs, BudgetMs);
    }

    /// <summary>Starts timing a plan that <see cref="TryEnter"/> allowed.</summary>
    public static long Start() => Stopwatch.GetTimestamp();

    /// <summary>Charges the plan that began at <paramref name="started"/> to the window.</summary>
    public static void Charge(long started) => _spentMs += ElapsedMs(started, Stopwatch.GetTimestamp());

    private static double ElapsedMs(long from, long to) =>
        (to - from) * PlanBudgetRules.MillisecondsPerSecond / Stopwatch.Frequency;
}

/// <summary>Pure rules behind <see cref="PlanBudget"/>.</summary>
public static class PlanBudgetRules
{
    public const double MillisecondsPerSecond = 1000.0;

    public static bool WindowOver(double elapsedMs, double windowMs) => elapsedMs >= windowMs;

    public static bool MayPlan(double spentMs, double budgetMs) => spentMs < budgetMs;
}
