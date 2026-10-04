using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// The monsters a hunter goes after: live wild creatures the hunter counts as enemies, and
/// not town vermin. Vermin in a dungeon are its spawn, as the sewer rats of the Britain
/// Sewer are the novices' first crawl. A hunter looks over the whole ground for them, not
/// only the tiles it can see, the way a player scans the screen and walks toward the spawn.
/// World thread only.
/// </summary>
public static class HuntPrey
{
    public static bool IsPrey(SosariaCharacter hunter, Mobile mobile) =>
        mobile is BaseCreature { Deleted: false, Alive: true, Hidden: false, Blessed: false } creature &&
        !creature.IsInvulnerable &&
        (!HarmlessCreatures.LooksNamed(creature.GetType().Name) || DungeonTripSkill.InDungeon(creature)) &&
        hunter.IsEnemy(creature);

    /// <summary>
    /// The nearest prey within <paramref name="radius"/> of <paramref name="center"/> that the
    /// hunter has not passed over, or null.
    /// </summary>
    public static Mobile Nearest(SosariaCharacter hunter, Point3D center, int radius, IReadOnlySet<Serial> passed)
    {
        if (!People.InWorld(hunter))
        {
            return null;
        }

        Mobile nearest = null;
        var best = int.MaxValue;

        foreach (var mobile in hunter.Map.GetMobilesInRange(center, radius))
        {
            if (passed.Contains(mobile.Serial) || !IsPrey(hunter, mobile))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(hunter.Location, mobile.Location);

            if (distance < best)
            {
                nearest = mobile;
                best = distance;
            }
        }

        return nearest;
    }

    /// <summary>How many prey stand within <paramref name="radius"/> of a point on a map.</summary>
    public static int CountAt(SosariaCharacter hunter, Map map, Point3D center, int radius)
    {
        if (hunter == null || map == null || map == Map.Internal)
        {
            return 0;
        }

        var count = 0;

        foreach (var mobile in map.GetMobilesInRange(center, radius))
        {
            if (IsPrey(hunter, mobile))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// What <paramref name="hunter"/> would find at a catalog ground right now. A ground a red
    /// cannot get to without the guards (see <see cref="RedGangReach"/>) is as closed as one
    /// under them: from Buccaneer's Den every hunt set out for ground across the water.
    /// </summary>
    public static GroundState StateOf(SosariaCharacter hunter, Map map, Destination ground, DateTime now)
    {
        if (hunter == null || ground == null)
        {
            return GroundState.Closed;
        }

        var at = ground.Arrival;

        return HuntGround.StateOf(
            GuardCall.IsGuardedPlace(at, map) ||
            hunter.Disposition == DispositionKind.Outlaw && !RedGangReach.CanReach(hunter, at),
            DryGrounds.IsDry(hunter.Serial.Value, new Point2D(at.X, at.Y), now),
            CountAt(hunter, map, at, HuntGround.AreaRadius)
        );
    }
}
