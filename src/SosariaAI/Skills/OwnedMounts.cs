using System.Collections.Generic;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Finding and climbing onto a mount the character owns. Both the Mount skill and the
/// buy step need this, and the buy step must use it at once: the goal loop scores again
/// the moment a step ends, so a character that only bought a horse walked off to other
/// work and left it following behind.
/// </summary>
public static class OwnedMounts
{
    /// <summary>
    /// The nearest ready mount the character controls, within range, else null. A mount the
    /// Mount job rests for after failing at it three times the same way is passed over: the
    /// job learns its mount only from this search, so its start never saw the rest.
    /// </summary>
    public static BaseMount Nearest(SosariaCharacter rider, int range)
    {
        if (rider == null || rider.Deleted || !People.InWorld(rider))
        {
            return null;
        }

        var now = Core.Now;

        var mounts = new List<BaseMount>();
        var scores = new List<(bool Owned, int Chebyshev)>();

        foreach (var mobile in rider.GetMobilesInRange(range))
        {
            if (mobile is not BaseMount mount)
            {
                continue;
            }

            if (!MountRules.MountIsReady(mount.Deleted, mount.IsDeadPet, mount.Rider != null, mount.Poisoned))
            {
                continue;
            }

            if (!MountRules.GenderAllowed(rider.Female, mount.AllowMaleRider, mount.AllowFemaleRider) ||
                JobTargetRest.Rests(rider, SkillKinds.Mount, JobTargetRest.KeyOf(mount), now))
            {
                continue;
            }

            var owned = MountRules.RiderControls(
                mount.Controlled,
                mount.ControlMaster == rider,
                mount.Summoned,
                mount.SummonMaster == rider
            );

            mounts.Add(mount);
            scores.Add((owned, NavMetric.Chebyshev(rider.Location, mount.Location)));
        }

        var pick = MountRules.ChooseIndex(scores.ToArray());
        return pick == MountRules.NoCandidate ? null : mounts[pick];
    }

    /// <summary>Puts the character on the mount. False when the rider did not end up seated.</summary>
    public static bool Climb(SosariaCharacter rider, BaseMount mount)
    {
        if (rider == null || mount == null || mount.Deleted || !MountRules.RiderMayMount(rider.Mounted))
        {
            return false;
        }

        mount.Rider = rider;
        return rider.Mounted;
    }

    /// <summary>
    /// Gets off the mount; it stands where the rider stood. The engine will not mine or fish
    /// from the saddle (<see cref="HarvestSkill.RidingBlocksWork"/>). False when the rider is
    /// still seated.
    /// </summary>
    public static bool StepDown(SosariaCharacter rider)
    {
        if (rider?.Mount is { } mount)
        {
            mount.Rider = null;
        }

        return rider is { Mounted: false };
    }

    /// <summary>
    /// Gets off the ridden mount and onto <paramref name="mount"/>; the old one stands where the
    /// rider stood. A mount that will not take this rider is not tried, and the rider climbs
    /// back on the old one when the new one did not seat it. True when it rides the new one.
    /// </summary>
    public static bool SwapTo(SosariaCharacter rider, BaseMount mount)
    {
        if (rider?.Mount is not BaseMount ridden || mount == null ||
            !MountRules.MountIsReady(mount.Deleted, mount.IsDeadPet, mount.Rider != null, mount.Poisoned) ||
            !MountRules.GenderAllowed(rider.Female, mount.AllowMaleRider, mount.AllowFemaleRider) ||
            !StepDown(rider))
        {
            return false;
        }

        if (Climb(rider, mount))
        {
            return true;
        }

        Climb(rider, ridden);
        return false;
    }

    /// <summary>Climbs onto a mount already within arm's reach. Used right after a purchase.</summary>
    public static bool ClimbNearest(SosariaCharacter rider) =>
        Climb(rider, Nearest(rider, MountRules.ReachTiles));
}
