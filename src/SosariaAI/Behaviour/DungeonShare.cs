using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Admin;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// The minute count behind <see cref="DungeonShareRules"/>: live fighters, those of them
/// standing underground, and how many people each dungeon holds or has on its way. A fighter
/// still walking to the door does not count toward the pull: a run counted from its first
/// step held the pull at neutral while 81 were on the road and 14 were inside. It does count
/// toward its dungeon's crowd, so the next pick goes elsewhere. It rides the
/// <see cref="ActivityPulse"/> timer on the world thread; the job roll, the scorer and the
/// dungeon pick read what it leaves.
/// </summary>
public static class DungeonShare
{
    private static readonly ILogger logger = SosariaLog.For(typeof(DungeonShare));
    private static Dictionary<string, int> _visitors = new(StringComparer.OrdinalIgnoreCase);
    private static int _minutes;

    /// <summary>The pull on dungeon runs from the last count. Neutral before the first count.</summary>
    public static double Demand { get; private set; } = DungeonShareRules.Neutral;

    public static void Initialize() => ActivityPulse.MinutePassed += Count;

    /// <summary>People inside the named dungeon or on a run to it at the last count.</summary>
    public static int Visitors(string dungeon) =>
        string.IsNullOrWhiteSpace(dungeon) ? 0 : _visitors.GetValueOrDefault(dungeon);

    private static void Count()
    {
        var fighters = 0;
        var underground = 0;
        var visitors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is not SosariaCharacter { Deleted: false, Alive: true } character ||
                !People.InWorld(character) ||
                character.Build?.Role != CharacterRole.Fighter)
            {
                continue;
            }

            fighters++;
            var inside = DungeonTripSkill.InDungeon(character);
            underground += inside ? 1 : 0;

            if (DungeonOf(character, inside) is { } dungeon)
            {
                visitors[dungeon] = visitors.GetValueOrDefault(dungeon) + 1;
            }
        }

        Demand = DungeonShareRules.Demand(underground, fighters);
        _visitors = visitors;
        _minutes++;

        if (CensusText.Due(_minutes) && SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", DungeonShareRules.CensusLine(visitors, underground, fighters));
            logger.Information("{Line}", MissCensus());
        }
    }

    /// <summary>
    /// Why the live fighters above ground are not underground, one reason each, and the mean
    /// chance of the dungeon job for those whose delve is open. Read after the pull is set, so
    /// the odds carry this minute's pull. It reads a copy of the roster: a shop look in the
    /// situation can make the engine create a vendor's display beast, and the live mobile list
    /// changed under the count and crashed the server.
    /// </summary>
    private static string MissCensus()
    {
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        var oddsSum = 0.0;
        var open = 0;
        var aboveGround = 0;

        foreach (var character in FleetStatus.InWorld())
        {
            if (character is not { Alive: true, Definition: not null } ||
                character.Build?.Role != CharacterRole.Fighter || DungeonTripSkill.InDungeon(character))
            {
                continue;
            }

            aboveGround++;
            var reason = WhyAboveGround(character, out var odds);
            reasons[reason] = reasons.GetValueOrDefault(reason) + 1;

            if (odds is { } chance)
            {
                oddsSum += chance;
                open++;
            }
        }

        return DungeonShareRules.MissLine(reasons, oddsSum, open, aboveGround);
    }

    /// <summary>
    /// The first thing that keeps one fighter above ground, in the order the job loop meets
    /// them: a red (its runs are its own), a goal that is no job, no door in reach, no hall that
    /// fits its power, a delve the scorer bars, or dice that landed elsewhere.
    /// <paramref name="odds"/> is the dungeon job's chance when the delve is open.
    /// </summary>
    private static string WhyAboveGround(SosariaCharacter character, out double? odds)
    {
        odds = null;

        if (PkRules.IsRed(character.Kills))
        {
            return DungeonShareRules.RedRuns;
        }

        if (JobRules.TryParse(character.ActiveGoalTarget, out var held) && held == JobKind.Dungeon)
        {
            return DungeonShareRules.OnDungeonJob;
        }

        var situation = character.BuildSituation();
        var hunt = character.HuntHomeNow();
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var candidates = ActionCatalog.From(character.Definition, catalog, hunt);
        var skills = GoalPlanRules.SkillKindsFrom(candidates);
        var goal = GoalRules.Pick(situation, character.JobSeed(Core.Now), skills);

        if (!JobRules.IsJob(goal.Target))
        {
            return DungeonShareRules.GoalReason(goal.Kind.ToString());
        }

        if (hunt?.Dungeon == null)
        {
            return DungeonGround.ReachFor(character).Count == 0 ? DungeonShareRules.NoDoorInReach : DungeonShareRules.NoHallFits;
        }

        var delveGoal = JobRules.GoalFor(JobKind.Dungeon);
        string barred = null;

        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].SkillKind != SkillKinds.Dungeon)
            {
                continue;
            }

            barred = ActionScorer.Unavailable(candidates[i], situation, catalog, delveGoal);

            if (barred == null)
            {
                break;
            }
        }

        if (barred != null)
        {
            return barred;
        }

        odds = JobRules.Odds(JobKind.Dungeon, situation, skills);
        return DungeonShareRules.RolledReason(
            JobRules.TryParse(character.ActiveGoalTarget, out var job) ? JobRules.NameOf(job) : goal.Target
        );
    }

    /// <summary>The dungeon a fighter stands in, or the one its run heads for; null for neither.</summary>
    private static string DungeonOf(SosariaCharacter character, bool inside)
    {
        var dungeonAt = DungeonGround.RegionNames(character.Map);

        if (inside)
        {
            return dungeonAt(character.Location);
        }

        return character.Routine?.CurrentSkill is DungeonTripSkill trip ? dungeonAt(trip.Hall) : null;
    }
}
