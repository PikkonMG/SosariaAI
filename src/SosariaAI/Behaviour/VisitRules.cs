using Server;
using SosariaAI.Combat;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Who a character may walk over to visit. Pure. No world objects.
/// </summary>
public static class VisitRules
{
    /// <summary>
    /// A visit is a walk across town, not across the land. A friend picked anywhere on the
    /// facet was never reached in the five minutes a visit lasts.
    /// </summary>
    public const int ReachTiles = 100;

    /// <summary>
    /// A visit is a walk across town for a chat. The friend must be alive, a real person
    /// on the same map, and standing under the town guards. A miner visited a friend who
    /// was hunting in the graveyard and walked straight into the undead.
    /// </summary>
    public static bool MayVisit(bool alive, bool isPerson, bool sameMap, bool friendUnderGuards) =>
        alive && isPerson && sameMap && friendUnderGuards;

    /// <summary>
    /// The friend to set out for: one who may be visited, within <see cref="ReachTiles"/>, and
    /// settled where it is, not on its way somewhere. A friend mustering a party at the bank
    /// left for a dungeon, and the visitor stood waiting for five minutes.
    /// </summary>
    public static bool MayPick(bool mayVisit, int distance, bool friendSettled) =>
        mayVisit && distance <= ReachTiles && friendSettled;

    /// <summary>
    /// Beside the friend: the square reach the walk uses. The round distance said three tiles
    /// and a half was still too far while the walk said it had arrived, so the visitor stood
    /// three tiles off its friend for the whole visit and then gave up.
    /// </summary>
    public static bool Reached(Point3D visitor, Point3D friend) =>
        NavMetric.Chebyshev(visitor, friend) <= SosariaCombat.FollowRangeMax;
}
