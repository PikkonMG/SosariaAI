using System;
using System.Collections.Generic;
using System.Globalization;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>What came of one Jev next-job answer: the action to run, or null for the scorer's pick.</summary>
public readonly record struct JevVerdict(string ActionId, string Source, bool Greet);

/// <summary>The facts that make a next-job pick a big moment, read on the world thread.</summary>
public readonly record struct MomentFacts(bool DiedRecently, bool PlayerNear);

/// <summary>
/// When a next-job pick goes to Jev, and how its answer is read. Pure. The code keeps
/// control: Jev only picks between jobs the rules already allow, and every doubt, error,
/// or limit hands the pick back to the scorer. With jevScope "combat-and-big" only a big
/// moment is put to Jev: back from a death, or a real player near. Routine jobs stay with the
/// scorer. A hunt's end, low gold or supplies, a party forming and a fight on offer were big
/// moments once; with 1,600 people online they came every few seconds, and Jev was asked
/// three times more often than it could answer. Each outcome has a log source so the activity log can count Jev picks against
/// scorer picks.
/// </summary>
public static class JevDecisionRules
{
    /// <summary>How long a character stands waiting for Jev before the scorer picks.</summary>
    public static readonly TimeSpan AnswerWait = TimeSpan.FromSeconds(3);

    /// <summary>A greet noul at or above this greets the person nearby.</summary>
    public const double GreetThreshold = 0.6;

    /// <summary>Fewer options than this leave nothing to choose.</summary>
    public const int MinOptions = 2;

    /// <summary>Jev's own in-flight limit waits this many times over in its queue before a call is refused.</summary>
    public const int QueueDepth = 2;

    /// <summary>A real player this near makes the next job a big moment.</summary>
    public const int PlayerNearTiles = 12;

    private const string ConfidenceFormat = "0.00";
    public const string FallbackSource = GoalLoop.FallbackSource;
    public const string RuleSource = FallbackSource + ": rule";
    public const string OneChoiceSource = FallbackSource + ": one choice";
    public const string CooldownSource = FallbackSource + ": jev cooldown";
    public const string CapSource = FallbackSource + ": jev hourly budget";
    public const string ScopeSource = "scorer: routine job";
    public const string RateSource = FallbackSource + ": jev rate limit";
    public const string BusySource = FallbackSource + ": jev busy";
    public const string SlowSource = FallbackSource + ": jev too slow";
    public const string NoPickSource = FallbackSource + ": jev gave no pick";
    public const string StaleSource = FallbackSource + ": jev pick no longer allowed";

    /// <summary>The decision floor: the brain.json decision floor, or the provider's own when higher.</summary>
    public static double Floor(double decisionMinConfidence, double? providerMinConfidence) =>
        Math.Max(decisionMinConfidence, providerMinConfidence ?? 0);

    /// <summary>
    /// A pick the rules hold above any plan step, such as following the party leader, is not
    /// put to Jev: a party member does not wander off its group on a model's whim.
    /// </summary>
    public static bool IsForced(ScoreResult result) =>
        result != null && result.Winner.Score >= ActionScorer.PartyFollowScore;

    /// <summary>
    /// The rules keep this pick: a forced one (<see cref="IsForced"/>), a red that must go back
    /// to the Den before anything else, or a blue that must leave it (<see cref="Situation.DenTripFirst"/>).
    /// Reds are always armed and stocked fighters; a model never sends one out naked or short,
    /// and never keeps a blue standing about the reds' town.
    /// </summary>
    public static bool RulesHold(ScoreResult result, bool denTripFirst) => denTripFirst || IsForced(result);

    /// <summary>Why this pick stays with the scorer without a call, or null to ask Jev.</summary>
    public static string SkipReason(int optionCount, bool forced, bool coolingDown, bool budgetAllows, bool rateAllows)
    {
        if (forced)
        {
            return RuleSource;
        }

        if (optionCount < MinOptions)
        {
            return OneChoiceSource;
        }

        if (coolingDown)
        {
            return CooldownSource;
        }

        if (!budgetAllows)
        {
            return CapSource;
        }

        return rateAllows ? null : RateSource;
    }

    /// <summary>The big moment behind this next-job pick, in a few words, or null for a routine one.</summary>
    public static string Moment(MomentFacts facts)
    {
        if (facts.DiedRecently)
        {
            return "back from death";
        }

        return facts.PlayerNear ? "a player stands near" : null;
    }

    /// <summary>Why the rules picked a job the routed model is not trusted with.</summary>
    public static string RulesKeepSource(string providerName) => $"{FallbackSource}: {providerName} leaves next jobs to the rules";

    /// <summary>The kind a next-job call is booked as: a big moment, or with scope "all" a routine decision.</summary>
    public static JevKind? JobKind(bool scopeAll, string moment) =>
        moment != null ? JevKind.BigMoment : scopeAll ? JevKind.Decision : null;

    /// <summary>Calls Jev may have out at once, sent or waiting in its queue.</summary>
    public static int QueueLimit(int concurrency) => Math.Max(1, concurrency) * QueueDepth;

    /// <summary>A call waits its turn in Jev's own short queue instead of falling back at once.</summary>
    public static bool HasRoom(int inFlight, int concurrency) => inFlight < QueueLimit(concurrency);

    /// <summary>
    /// Reads Jev's answer. The pick stands only when it names an offered option at or above the
    /// floor; the greet fan-out follows the same answer only when it was asked.
    /// </summary>
    public static JevVerdict Read(
        IReadOnlyDictionary<string, SystemOneAnswer> answers,
        IReadOnlyList<JobOption> options,
        double floor,
        string error,
        string providerName = BrainProviders.JevName
    )
    {
        var source = string.IsNullOrWhiteSpace(providerName) ? BrainProviders.JevName : providerName;

        if (!string.IsNullOrEmpty(error))
        {
            return new JevVerdict(null, $"{FallbackSource}: {source} {error}", false);
        }

        var greet = answers != null &&
                    answers.TryGetValue(JevPrompt.GreetQuestion, out var greetAnswer) &&
                    greetAnswer.Noul >= GreetThreshold;

        if (answers == null ||
            !answers.TryGetValue(JevPrompt.NextJobQuestion, out var next) ||
            string.IsNullOrWhiteSpace(next.Choice) ||
            Find(options, next.Choice) is not { } picked)
        {
            return new JevVerdict(null, NoPickSource, greet);
        }

        if (next.Confidence < floor)
        {
            return new JevVerdict(null, $"{FallbackSource}: {source} unsure {Word(next.Confidence)}", greet);
        }

        return new JevVerdict(picked.ActionId, $"{source} {Word(next.Confidence)}", greet);
    }

    // The log reads the same on every server culture.
    private static string Word(double confidence) => confidence.ToString(ConfidenceFormat, CultureInfo.InvariantCulture);

    private static JobOption Find(IReadOnlyList<JobOption> options, string key)
    {
        if (options == null)
        {
            return null;
        }

        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return options[i];
            }
        }

        return null;
    }
}
