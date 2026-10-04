using System;
using System.Collections.Generic;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>The System One models the plugin knows. Each speaks the same wire format but judges differently.</summary>
public enum SystemOneFamily
{
    /// <summary>TypeSafe's paid model. Sure and right on every task tested.</summary>
    Jev,

    /// <summary>A free model run by the operator. Reliable on one yes/no at a time.</summary>
    Von,

    /// <summary>A free model run by the operator. Reliable on one yes/no at a time.</summary>
    Laya
}

/// <summary>The kinds of typed question the Brain puts to a System One provider.</summary>
public enum SystemOneTask
{
    /// <summary>How to fight a foe for the next few seconds: one yes/no per stance.</summary>
    Stance,

    /// <summary>The next job, or the routine after an event: one pick among a few jobs.</summary>
    NextJob,

    /// <summary>What a player's unclear line wants: one pick among six intents.</summary>
    Intent,

    /// <summary>A haggle line from a player that fits no trade rule.</summary>
    Trade,

    /// <summary>Would a character answer another character's words aloud.</summary>
    SpeechGate
}

/// <summary>
/// Knows which model a System One provider runs and which tasks that model is trusted with.
/// A task a model is not trusted with is never sent to it: the rules answer, for free, so a
/// weak guess never moves a character and never costs a GPU call. The trust comes from
/// side-by-side tests of all three models on the same states (September 2026): Jev was right
/// on every task; Von and Laya were right on most fight stances asked as yes/no questions,
/// but on next jobs, player intents, and the speech gate they were near even or often wrong.
/// Pure.
/// </summary>
public static class SystemOneFamilies
{
    public const string TypeSafeHost = "typesafe.ai";

    private static readonly IReadOnlySet<SystemOneTask> AllTasks = new HashSet<SystemOneTask>(Enum.GetValues<SystemOneTask>());

    private static readonly IReadOnlySet<SystemOneTask> YesNoTasks = new HashSet<SystemOneTask> { SystemOneTask.Stance };

    private static readonly IReadOnlyDictionary<SystemOneTask, string> TaskWords = new Dictionary<SystemOneTask, string>
    {
        [SystemOneTask.Stance] = "fight stances",
        [SystemOneTask.NextJob] = "next jobs",
        [SystemOneTask.Intent] = "player intent",
        [SystemOneTask.Trade] = "trade reads",
        [SystemOneTask.SpeechGate] = "the speech gate"
    };

    /// <summary>
    /// The family a provider runs. An explicit <see cref="ProviderDefinition.Family"/> wins;
    /// else the TypeSafe address or a model or provider name that starts with the family's
    /// word. An unknown model gets the careful Von trust, so it never decides more than
    /// fight stances until the operator names it.
    /// </summary>
    public static SystemOneFamily Detect(string providerName, ProviderDefinition provider)
    {
        if (Parse(provider?.Family) is { } named)
        {
            return named;
        }

        if (provider?.BaseUrl?.Contains(TypeSafeHost, StringComparison.OrdinalIgnoreCase) == true ||
            StartsWith(provider?.Model, BrainProviders.JevName) || StartsWith(providerName, BrainProviders.JevName))
        {
            return SystemOneFamily.Jev;
        }

        return StartsWith(provider?.Model, BrainProviders.LayaName) || StartsWith(providerName, BrainProviders.LayaName)
            ? SystemOneFamily.Laya
            : SystemOneFamily.Von;
    }

    /// <summary>The family a word names, or null for no word or an unknown one.</summary>
    public static SystemOneFamily? Parse(string word) =>
        word?.Trim().ToLowerInvariant() switch
        {
            BrainProviders.JevName => SystemOneFamily.Jev,
            BrainProviders.VonName => SystemOneFamily.Von,
            BrainProviders.LayaName => SystemOneFamily.Laya,
            _ => null
        };

    /// <summary>True when this family is trusted with the task.</summary>
    public static bool Handles(SystemOneFamily family, SystemOneTask task) => TasksOf(family).Contains(task);

    /// <summary>The task a Brain call is: the ask shape wins, then what the call is for.</summary>
    public static SystemOneTask TaskOf(JevKind use, JevAsk ask) =>
        ask switch
        {
            JevAsk.Gate => SystemOneTask.SpeechGate,
            JevAsk.Intent => SystemOneTask.Intent,
            _ => use switch
            {
                JevKind.PersonFight or JevKind.MonsterFight => SystemOneTask.Stance,
                JevKind.Trade => SystemOneTask.Trade,
                JevKind.SpeechGate => SystemOneTask.SpeechGate,
                _ => SystemOneTask.NextJob
            }
        };

    /// <summary>"fight stances" and the like: the tasks a family takes, for the boot log.</summary>
    public static string HandledWords(SystemOneFamily family) => Words(family, handled: true);

    /// <summary>The tasks the rules keep from this family, for the boot log; empty when none.</summary>
    public static string RulesWords(SystemOneFamily family) => Words(family, handled: false);

    private static IReadOnlySet<SystemOneTask> TasksOf(SystemOneFamily family) =>
        family == SystemOneFamily.Jev ? AllTasks : YesNoTasks;

    private static string Words(SystemOneFamily family, bool handled)
    {
        var words = new List<string>();

        foreach (var task in Enum.GetValues<SystemOneTask>())
        {
            if (Handles(family, task) == handled)
            {
                words.Add(TaskWords[task]);
            }
        }

        return string.Join(", ", words);
    }

    private static bool StartsWith(string value, string word) =>
        value?.Trim().StartsWith(word, StringComparison.OrdinalIgnoreCase) == true;
}
