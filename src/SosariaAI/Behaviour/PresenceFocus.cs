using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Behaviour;

/// <summary>
/// Spend world-thread time on people a client can see, or on combat and ghosts.
/// Far characters still walk and work. They do not greet empty air at full think rate.
/// </summary>
public static class PresenceFocus
{
    /// <summary>UO screen plus a little margin. A client this close gets full think.</summary>
    public const int NearPlayerTiles = 24;

    public const double NearThinkSeconds = 0.25;

    /// <summary>Eight times slower than a watched wander. Movement still runs; scans do not.</summary>
    public const double FarThinkSeconds = 2.0;

    public const int FarDangerMs = 2500;

    /// <summary>
    /// A client within screen range, hidden or not. A hidden GM still watches the screen, so the
    /// think rate stays up for it; nothing the character says or does may count on it.
    /// </summary>
    public static bool ClientWatching(Mobile mobile) => HumanClientNear(mobile, perceivedOnly: false);

    /// <summary>A person at a keyboard within screen range that this mobile can see: a hidden GM is not one.</summary>
    public static bool PlayerNearby(Mobile mobile) => HumanClientNear(mobile, perceivedOnly: true);

    private static bool HumanClientNear(Mobile mobile, bool perceivedOnly)
    {
        if (mobile?.Map == null || mobile.Map == Map.Internal)
        {
            return false;
        }

        foreach (var state in mobile.GetClientsInRange(NearPlayerTiles))
        {
            if (People.IsHuman(state.Mobile) && !state.Mobile.Deleted &&
                (!perceivedOnly || People.Perceives(mobile, state.Mobile)))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MustThinkFast(bool playerNearby, bool inCombat, bool isGhost, bool hunting) =>
        playerNearby || inCombat || isGhost || hunting;
}
