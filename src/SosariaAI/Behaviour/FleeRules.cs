using System;
using Server;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// Flee is a skill. A rat in a guarded town is not a threat.
/// </summary>
public static class FleeRules
{
    public const int CoveredTiles = 24;

    /// <summary>
    /// A flee may end early when nothing hostile is in sight, but only after this many
    /// tiles. The threat scan reaches ten tiles, so a few steps hide the monster; ending
    /// there sent the character straight back into it, every second.
    /// </summary>
    public const int SafeGapTiles = 12;

    public static bool MayFlee(bool inGuardedRegion, int threat) =>
        !inGuardedRegion && threat >= ThreatRating.MinThreatToFlee;

    /// <summary>
    /// True when the guards keep this person safe where it stands, so a fight there is no
    /// reason to run: under the guards and wanted by neither side of the law
    /// (<see cref="ResurrectAid.WantedUnderGuards"/>). A red or a criminal under the guards is
    /// the one they come for. The guard exemption of <see cref="MayFlee"/> is a blue's rule;
    /// applied to a red it held the red in town, in the fight, until the guards came.
    /// </summary>
    public static bool GuardsShelter(bool underGuards, bool criminal, bool murderer) =>
        underGuards && !ResurrectAid.WantedUnderGuards(criminal, murderer, underGuards);

    /// <summary>
    /// Whoever is already landing the hits is not a sighting. The guard-zone
    /// exemption does not apply to an attack in progress, and a guild-war enemy
    /// may never appear on the sight scan at all.
    /// </summary>
    public static bool MayFleeAttacker(int threat) => threat >= ThreatRating.MinThreatToFlee;

    /// <summary>
    /// The one rule for running away. The goal loop and the danger check both use it, so
    /// a character is never stopped for danger and then offered no way to run.
    /// </summary>
    public static bool MustRun(bool inGuardedRegion, CharacterRole role, int threat, bool intolerable) =>
        MayFlee(inGuardedRegion, threat) && (DangerRules.MustFlee(role, threat) || intolerable);

    /// <summary>
    /// A party member that ran this often stops following. Each walk back to the leader
    /// led it into the same monsters, and the leader stood still waiting for it.
    /// </summary>
    public const int RunsBeforeLeavingTrip = 3;
    public const int RunsBeforeGoingHome = 1;
    public const int RunsBeforeBlockingFlee = 3;

    /// <summary>A character that ran this often lately goes home whole and stocked: the place keeps chasing it off.</summary>
    public const int RunsBeforeGoingHomeAnyway = 3;

    public static bool KeepsAwayFromParty(int recentRuns) => recentRuns >= RunsBeforeLeavingTrip;

    public static bool FledEnough(int recentRuns) => recentRuns >= RunsBeforeBlockingFlee;

    /// <summary>
    /// After a flee outside town the danger is gone and the character goes back to its work.
    /// It walks home only when the run left it hurt, when its supplies are low, or when it ran
    /// several times lately (a bank walk on the Despise road led Matai back into the trolls
    /// every few seconds). Sending every worker who fled home made "go home" the commonest job.
    /// </summary>
    public static bool ShouldGoHomeAfterRuns(bool inTown, int recentRuns, double hitsFraction, bool suppliesLow) =>
        !inTown && recentRuns >= RunsBeforeGoingHome &&
        (hitsFraction < RecoveryRules.RecoverBelowHitsFraction || suppliesLow || recentRuns >= RunsBeforeGoingHomeAnyway);

    /// <summary>
    /// After repeated runs, the walk home must not reverse the flee. Matai fled
    /// north off the Despise road, then GoHome walked south into the same trolls.
    /// Step aside first, then turn toward home.
    /// </summary>
    public const int BypassTiles = 48;

    public static Point3D BypassTowardHome(Point3D from, Point3D home)
    {
        var east = home.X - from.X;
        var south = home.Y - from.Y;

        if (Math.Abs(south) >= Math.Abs(east))
        {
            var west = from.X >= BypassTiles ? from.X - BypassTiles : from.X + BypassTiles;
            return new Point3D(west, from.Y, from.Z);
        }

        var north = from.Y >= BypassTiles ? from.Y - BypassTiles : from.Y + BypassTiles;
        return new Point3D(from.X, north, from.Z);
    }

    public static bool IsDone(int threat, int coveredTiles) =>
        coveredTiles >= CoveredTiles || (threat == 0 && coveredTiles >= SafeGapTiles);

    public static bool FleeIsBlocked(string blockedActionId, int blockedCount) =>
        RepeatFailure.IsBlocked(blockedCount) &&
        ActionId.SkillKindOf(blockedActionId) == SkillKinds.Flee;

    /// <summary>
    /// Abort current work only when a flee can still run. After flee is blocked,
    /// aborting wander every tick left the character restarting idle at the
    /// dungeon mouth, never leaving the threat. GoHome after a flee must finish:
    /// aborting it at the next troll put Matai back on the Despise road every few seconds.
    /// </summary>
    public static bool ShouldAbortWork(bool mustRun, bool fleeBlocked, string currentSkillKind)
    {
        if (currentSkillKind == SkillKinds.GoHome)
        {
            return false;
        }

        return mustRun && !fleeBlocked;
    }

    /// <summary>
    /// The AI action leaving Wander for anything but a fight (a fight holds the job, see
    /// <see cref="Mobiles.RoutineDriver.HoldForFight"/>) must not abort idle wander. Aborting
    /// idle every time a monster flickered into range restarted wander every second at the
    /// dungeon mouth, with no matching end.
    /// </summary>
    public static bool ShouldAbortForAiAction(string skillKind, bool actionIsWander)
    {
        if (actionIsWander)
        {
            return false;
        }

        return skillKind is not (
            SkillKinds.IdleWander or
            SkillKinds.Flee or
            SkillKinds.Hunt or
            SkillKinds.Dungeon or
            SkillKinds.GoHome
        );
    }

    /// <summary>
    /// Idle fallback after an abort must keep the same wander running. A new
    /// IdleWander Begin every tick is the start loop.
    /// </summary>
    public static bool ShouldReuseIdle(string skillKind, bool aborted) =>
        aborted && skillKind == SkillKinds.IdleWander;

    public static bool IsEscapeWhenFleeBlocked(string skillKind) =>
        skillKind is SkillKinds.GoHome or SkillKinds.IdleWander;

    /// <summary>
    /// A hit that lands during a flee, or on a character already fighting this same
    /// attacker, is answered by standing the fight. Answering every swing with a new
    /// flee refreshed the engine flee timer forever: the live timer made the AI skip
    /// every think, so workers stood in place and died without a swing.
    /// </summary>
    public static bool MayAnswerHitWithFlee(bool fleeing, bool fightingThisAttacker) =>
        !fleeing && !fightingThisAttacker;

    /// <summary>One run from one threat is one decision for this long.</summary>
    public static readonly TimeSpan EpisodeRest = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A run from a new threat, or from the same one once the episode rest ran out, is a new
    /// decision with its line and its log. A renewed chase inside the rest is the same run.
    /// </summary>
    public static bool NewEpisode(uint lastThreat, DateTime lastAt, uint threat, DateTime now) =>
        lastThreat != threat || TimeRules.Rested(lastAt, now, EpisodeRest);
}
