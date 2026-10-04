using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using SosariaAI.Logging;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// Settles the phase job against what the person can do, keeps its plan, and picks the
/// next skill inside it. One skill runs until it ends, then the loop scores again; the job
/// itself only changes when the phase ends or a big event moves it.
/// </summary>
public static class GoalLoop
{
    private static readonly ILogger logger = SosariaLog.For(typeof(GoalLoop));

    public static ScoreResult Score(
        CharacterDefinition definition,
        Situation situation,
        Goal goal,
        int seed,
        DestinationCatalog catalog,
        GoalPlan plan = null,
        HuntHome hunt = null
    )
    {
        var candidates = Candidates(definition, catalog, hunt, situation, seed);
        var skills = GoalPlanRules.SkillKindsFrom(candidates);
        var settled = SettleJob(goal, situation, seed, skills, candidates, catalog);
        bool Runs(string skillKind) => CanRun(candidates, skillKind, situation, settled, catalog);
        var next = GoalPlanRules.SkipUnavailable(
            GoalPlanRules.ContinueOrStart(plan, settled, situation, skills, seed, Runs),
            situation,
            Runs
        );
        return ActionScorer.Rank(candidates, situation, settled, seed, catalog, next);
    }

    /// <summary>
    /// The authored actions, plus the trip a travel job would take this phase: a bank in
    /// another town, away from places the person ran from and banks it just found no way to.
    /// </summary>
    private static List<ActionCandidate> Candidates(
        CharacterDefinition definition,
        DestinationCatalog catalog,
        HuntHome hunt,
        Situation situation,
        int seed
    )
    {
        var candidates = ActionCatalog.From(definition, catalog, hunt);
        var bank = TownTripRules.Pick(
            catalog?.All,
            situation?.Location ?? Point3D.Zero,
            HomeLeash.ConfiguredRadius(),
            situation?.DangerSpots,
            situation?.UnreachableGoals,
            seed,
            situation?.BeyondLeash == true
        );

        if (definition != null && TownTripRules.Candidate(bank) is { } trip)
        {
            candidates.Add(trip);
        }

        return candidates;
    }

    /// <summary>
    /// The goal picker rolls before the skill list exists. With the list, the roll keeps
    /// to jobs the person has the skills for, and a job whose fresh plan has no step that
    /// can run now is dropped for the next roll. An outing whose core step cannot run is
    /// dropped too, whatever filler it could still do. Idle always has a step: staying a while.
    /// </summary>
    private static Goal SettleJob(
        Goal goal,
        Situation situation,
        int seed,
        IReadOnlyList<string> skills,
        IReadOnlyList<ActionCandidate> candidates,
        DestinationCatalog catalog
    )
    {
        if (!JobRules.IsJob(goal.Target))
        {
            return goal;
        }

        var picked = GoalRules.Pick(situation, seed, skills);

        if (!JobRules.TryParse(picked.Target, out var job))
        {
            return picked;
        }

        var excluded = new List<JobKind>();

        while (job != JobKind.Idle && !HasRunnableStep(JobRules.GoalFor(job), situation, seed, skills, candidates, catalog))
        {
            excluded.Add(job);
            job = JobRules.Roll(situation, seed, skills, excluded);
        }

        return JobRules.GoalFor(job);
    }

    private static bool HasRunnableStep(
        Goal goal,
        Situation situation,
        int seed,
        IReadOnlyList<string> skills,
        IReadOnlyList<ActionCandidate> candidates,
        DestinationCatalog catalog
    )
    {
        bool Runs(string skillKind) => CanRun(candidates, skillKind, situation, goal, catalog);

        var fresh = GoalPlanRules.Start(goal, situation, skills, seed);
        var core = GoalPlanRules.CoreSkill(fresh?.Id);

        // A flee or a bad wound bars every step for a moment only; the job stays.
        return GoalPlanRules.SkipUnavailable(fresh, situation, Runs) is { IsComplete: false } &&
               (core == null || GoalPlanRules.IsInterrupt(situation) || Runs(core));
    }

    private static bool CanRun(
        IReadOnlyList<ActionCandidate> candidates,
        string skillKind,
        Situation situation,
        Goal goal,
        DestinationCatalog catalog
    )
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (GoalPlanRules.StepAccepts(skillKind, candidates[i].SkillKind) &&
                ActionScorer.Unavailable(candidates[i], situation, catalog, goal) == null)
            {
                return true;
            }
        }

        return false;
    }

    public static ActionCandidate Find(
        CharacterDefinition definition,
        ActionId id,
        DestinationCatalog catalog,
        HuntHome hunt = null
    )
    {
        if (TownTripRules.BankNameOf(id.Value) is { } bankName &&
            TownTripRules.Candidate(catalog?.GetByName(bankName)) is { } trip)
        {
            return trip;
        }

        var candidates = ActionCatalog.From(definition, catalog, hunt);

        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Id.Value.Equals(id.Value, StringComparison.OrdinalIgnoreCase))
            {
                return candidates[i];
            }
        }

        return candidates.Count > 0 ? candidates[0] : null;
    }

    public static Skill CreateSkill(ActionCandidate candidate, FacetContent facet, string facetName)
    {
        if (candidate?.Step == null)
        {
            return null;
        }

        return SkillFactory.Create(candidate.Step, facet, facetName);
    }

    /// <summary>A model plan's step or a chat model's routine pick chose the action.</summary>
    public const string ModelSource = "model";

    /// <summary>The free scorer chose the action.</summary>
    public const string FallbackSource = "fallback";

    /// <summary>
    /// One activity line per new action. The source says who picked it, "model", "jev" with
    /// its confidence, or "fallback" with any reason Jev was not used, so a run's log can be
    /// counted by source. A pick other than the scorer's winner gives its own reason.
    /// </summary>
    public static void LogChoice(string name, ScoredAction chosen, ScoreResult result, string source)
    {
        if (!SosariaSettings.LogActivity || result == null)
        {
            return;
        }

        logger.Information(
            "{Name} decided {Action} ({Source}, {Goal}) because {Reason}",
            name,
            ActionDescriptions.Phrase(chosen.SkillKind, chosen.Id.Value),
            source,
            GoalRules.Describe(result.Goal),
            chosen.Id == result.Winner.Id ? result.WinnerReason : chosen.Why
        );
    }
}
