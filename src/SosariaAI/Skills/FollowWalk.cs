using System;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Chooses a short GoTo walk or a nav-graph TravelSkill walk for a follower, or a recall
/// after a leader that recalled far away.
/// </summary>
public static class FollowWalk
{
    /// <summary>Ticks a follower stands still before it asks the nav graph for a route again.</summary>
    public const int RetryRouteTicks = 40;

    public static int NavThreshold => NavLimits.SoftLegDistance;

    public static bool NeedsNav(int chebyshev) => chebyshev > NavThreshold;

    /// <summary>True when a follower with no route has waited long enough to try again.</summary>
    public static bool ShouldRetryRoute(int ticksWaited) => ticksWaited >= RetryRouteTicks;

    /// <summary>
    /// A follower plans again when its target moved, unless it is waiting out a failed
    /// route. A leader on the move changes the target every tick, and a follower that
    /// planned again on each change logged one failed route a second.
    /// </summary>
    public static bool ShouldReplan(bool waitingForRoute, bool destChanged, int ticksWaited) =>
        waitingForRoute ? ShouldRetryRoute(ticksWaited) : destChanged;

    /// <summary>A follower looks for a recall after the leader at most this often; between looks it walks.</summary>
    public static readonly TimeSpan RecallRetry = TimeSpan.FromSeconds(15);

    /// <summary>True when the follower may look for a recall after the leader: never looked, or the last look is old.</summary>
    public static bool MayTryRecall(DateTime lastTry, DateTime now) => lastTry == default || now - lastTry >= RecallRetry;
}
