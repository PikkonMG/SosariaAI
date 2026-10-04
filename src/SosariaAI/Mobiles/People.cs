using Server;
using Server.Mobiles;

namespace SosariaAI.Mobiles;

/// <summary>
/// Characters are PlayerMobiles too, so "is PlayerMobile" no longer means a person at a
/// keyboard. These checks say which kind of player a mobile is.
/// </summary>
public static class People
{
    /// <summary>A player driven by a real client, not by this plugin.</summary>
    public static bool IsHuman(Mobile mobile) => mobile is PlayerMobile and not SosariaCharacter;

    /// <summary>In the world: on a map, and not the internal map where the engine keeps the logged out.</summary>
    public static bool InWorld(Mobile mobile) => mobile?.Map != null && mobile.Map != Map.Internal;

    /// <summary>Any living player: a human or a character.</summary>
    public static bool IsLivingPlayer(Mobile mobile) => mobile is PlayerMobile { Deleted: false, Alive: true };

    /// <summary>
    /// Whether <paramref name="viewer"/> may notice <paramref name="other"/> at all. A hidden
    /// mobile is noticed only when the engine's own sight rule (<see cref="Mobile.CanSee(Mobile)"/>)
    /// lets the viewer see it, so a hidden GM or a hidden thief is not there for a character
    /// at player level. A visible mobile is noticed as before, staff included.
    /// </summary>
    public static bool Perceives(Mobile viewer, Mobile other) =>
        viewer != null && other != null && (!other.Hidden || viewer.CanSee(other));
}
