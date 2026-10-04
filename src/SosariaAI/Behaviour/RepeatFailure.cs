using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// After this many failures of the same plan, pick a different one.
/// Bank, rest and Decide finishing do not wipe a failed walk or dungeon.
/// </summary>
public static class RepeatFailure
{
    public const int Limit = 3;
    public static readonly TimeSpan SkillCooldown = TimeSpan.FromMinutes(5);

    /// <summary>Each block after the first doubles, up to this long.</summary>
    public static readonly TimeSpan MaxSkillCooldown = TimeSpan.FromHours(1);

    public const int QuickFailureSeconds = 20;

    /// <summary>A failure this soon after the start is a start that cannot work where it stands.</summary>
    public static readonly TimeSpan QuickFailure = TimeSpan.FromSeconds(QuickFailureSeconds);

    /// <summary>How far round the spot of a failure the skill stays barred.</summary>
    public const int FailSpotRange = 12;

    /// <summary>How long the spot of a quick failure bars the skill.</summary>
    public static readonly TimeSpan QuickSpotBar = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long the spot of a slow failure bars the skill: a failure after a long run may be
    /// the day's luck, not the ground, so the bar is short, but long enough that the skill
    /// is not picked again on the tile where it just ended.
    /// </summary>
    public static readonly TimeSpan SlowSpotBar = TimeSpan.FromMinutes(3);

    private const int CooldownGrowth = 2;

    public static bool IsBlocked(int consecutiveFailures) => consecutiveFailures >= Limit;

    /// <summary>True when <paramref name="skillKind"/> is among the listed skill kinds, in any case.</summary>
    public static bool Lists(IReadOnlyList<string> kinds, string skillKind)
    {
        if (string.IsNullOrWhiteSpace(skillKind) || kinds == null)
        {
            return false;
        }

        for (var i = 0; i < kinds.Count; i++)
        {
            if (skillKind.Equals(kinds[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A skill failed. The count runs on until the skill succeeds: at the limit it is off
    /// the menu for <see cref="SkillCooldown"/>, and each failure after that blocks it
    /// again at twice the length. The count once went back to one when a block ran out,
    /// so a Britain cook with no oven failed three times every five minutes all day. Every
    /// failure also bars the skill round the spot where it failed: the scorer picked a cook
    /// again four seconds after it failed at its start, and a seller six seconds after its
    /// walk to a shop ended in failure on that same tile. A failure within
    /// <see cref="QuickFailure"/> of the start bars the spot for <see cref="QuickSpotBar"/>,
    /// a slower one for <see cref="SlowSpotBar"/>.
    /// </summary>
    public static SkillFailures AfterSkillFailure(SkillFailures prior, DateTime now, TimeSpan ran, Point3D at)
    {
        var count = prior.Count + 1;
        var blockedUntil = IsBlocked(count) ? now + CooldownAfter(count) : prior.BlockedUntil;
        var bar = ran <= QuickFailure ? QuickSpotBar : SlowSpotBar;

        return new SkillFailures(count, blockedUntil, at, now + bar);
    }

    /// <summary>How long the skill is off the menu after <paramref name="failures"/> failures in a row.</summary>
    public static TimeSpan CooldownAfter(int failures)
    {
        if (!IsBlocked(failures))
        {
            return TimeSpan.Zero;
        }

        var cooldown = SkillCooldown;

        for (var extra = Limit; extra < failures && cooldown < MaxSkillCooldown; extra++)
        {
            cooldown *= CooldownGrowth;
        }

        return cooldown < MaxSkillCooldown ? cooldown : MaxSkillCooldown;
    }

    /// <summary>
    /// A job failed at one target. A failure the same way as the last one adds to the streak;
    /// any other way starts it again at one. At <see cref="Limit"/> the job rests for that
    /// target as long as <see cref="CooldownAfter"/> says: Bran walked for Sela's ghost 58
    /// times in nine hours, and Darian failed his remount 36 times, each "no walk" the same.
    /// </summary>
    public static TargetStreak AfterTargetFailure(TargetStreak prior, string reason, DateTime now, Point3D spot)
    {
        var way = reason ?? string.Empty;
        var count = string.Equals(prior.Reason, way, StringComparison.Ordinal) ? prior.Count + 1 : 1;
        var restUntil = IsBlocked(count) ? now + CooldownAfter(count) : default;
        return new TargetStreak(count, way, restUntil, spot);
    }

    /// <summary>True while the job rests for this target.</summary>
    public static bool TargetRests(TargetStreak streak, DateTime now) => streak.RestUntil > now;

    /// <summary>The skill is cooling down, or it just failed near where the character stands.</summary>
    public static bool IsSkillBlocked(SkillFailures failures, DateTime now, Point3D at) =>
        failures.BlockedUntil > now ||
        failures.FailSpotUntil > now && NavMetric.Chebyshev(at, failures.FailSpot) <= FailSpotRange;

    public static int AfterFailure(string lastId, string failedId, int consecutive)
    {
        if (string.IsNullOrWhiteSpace(failedId))
        {
            return 0;
        }

        if (string.Equals(lastId, failedId, StringComparison.OrdinalIgnoreCase))
        {
            return consecutive + 1;
        }

        return 1;
    }

    public static int AfterSuccess() => 0;

    /// <summary>
    /// A failed Decide, wander or rest does not count. An errand that fails counts: a
    /// buyer with no route to the shop started the same errand every three seconds.
    /// </summary>
    public static bool Counts(string skillName) =>
        !string.IsNullOrWhiteSpace(skillName) && !IsIncidental(skillName);

    /// <summary>
    /// Only the work itself succeeding clears the streak. Walking home or
    /// banking after a failed dungeon is not success of that dungeon.
    /// </summary>
    public static bool Clears(string skillName) =>
        skillName is SkillKinds.Dungeon or SkillKinds.Hunt or SkillKinds.Patrol
            or SkillKinds.Lumberjack or SkillKinds.Mine or SkillKinds.Fish
            or SkillKinds.VendorSell or SkillKinds.House or SkillKinds.Smith
            or SkillKinds.Boat or SkillKinds.Cartography or SkillKinds.Alchemy
            or SkillKinds.PlayerVendor or SkillKinds.Mage;

    private static bool IsIncidental(string skillName) =>
        skillName is SkillKinds.Decide or SkillKinds.IdleWander or SkillKinds.Rest;

    /// <summary>
    /// Wander and rest refuse on a bad spot: the spot is wrong, so the skill
    /// cools down without touching the errand streak. Decide is the planner
    /// itself and must never block itself.
    /// </summary>
    public static bool CoolsDown(string skillName) =>
        skillName is SkillKinds.IdleWander or SkillKinds.Rest;

    /// <summary>How long a finished outing stays off the menu.</summary>
    public static readonly TimeSpan DoneRest = TimeSpan.FromMinutes(DoneRestMinutes);

    public const int DoneRestMinutes = 30;

    /// <summary>
    /// A completed outing rests before it can be picked again: nothing counted a
    /// success, so a character with one sight in reach stared at the same healer
    /// every minute or two forever.
    /// </summary>
    public static bool RestsAfterDone(string skillName) =>
        skillName is SkillKinds.Sightsee;
}
