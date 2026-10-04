using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

/// <summary>
/// What a System One call carries: the state Jev judges plus the typed questions
/// it answers. One request ingests the state once, so extra questions are nearly
/// free. Built on the world thread where the facts live; the worker only
/// serializes it.
/// </summary>
public sealed record JevDecision(
    object State,
    IReadOnlyDictionary<string, JevQuestion> Questions
);

/// <summary>
/// Builds the System One payloads. A decision gets the small word-only state of
/// <see cref="JevSituationState"/>, never a memory dump, and at most
/// <see cref="JevJobOptions.MaxOptions"/> options: Jev reads named fields, judges words
/// better than numbers, and every input token is paid for.
/// </summary>
public static class JevPrompt
{
    public const string Instructions =
        "Pick the routine this person would choose next, given who they are and what just happened.";

    public const string ActInstructions =
        "Beyond their routine, is there something this person should do right now?";

    public const string GateInstructions =
        "Would this person answer aloud? Judge from who they are, what was said, and whether it was meant for them.";

    public const string IntentInstructions =
        "What does the player want from this person with these words?";

    public const string GreetInstructions = "Would this person greet someone nearby right now?";

    /// <summary>The next-job question: what to decide, and the one rule that keeps a job whole.</summary>
    public static readonly IReadOnlyDictionary<string, string> NextJobInstructions =
        new Dictionary<string, string>
        {
            ["question"] = "Which job does this person do next?",
            ["rule"] = "Keep to the plan unless body, goods, or danger say otherwise."
        };

    private static readonly IReadOnlyDictionary<string, string> GreetCriteria =
        new Dictionary<string, string>
        {
            ["true"] = "friendly and free",
            ["false"] = "shy, busy, or wary"
        };

    internal const string NextQuestion = "next";
    internal const string ActQuestion = "act";
    internal const string ReplyQuestion = "reply";
    internal const string IntentQuestion = "intent";
    internal const string NextJobQuestion = "next_job";
    internal const string GreetQuestion = "greet";

    // The act fan-out mirrors the frontier decide shape's act field. "none" is the
    // usual answer; the rest are exactly ImmediateActs.AllowList, the acts Brain.ApplyAct
    // carries out, so Jev is never offered an act no code runs.
    private static readonly IReadOnlyDictionary<string, string> ActOptions =
        new Dictionary<string, string>
        {
            ["none"] = "nothing besides the routine",
            [ImmediateActs.Greet] = "greet the person involved",
            [ImmediateActs.GoHunt] = "head out to hunt now",
            [ImmediateActs.GoTown] = "head back to town now"
        };

    /// <summary>An event decision (attacked, a dungeon ended, a player idles nearby): pick a routine.</summary>
    public static JevDecision Build(
        JevSituation situation,
        BrainEvent evt,
        IReadOnlyList<ChoiceDefinition> choices
    )
    {
        var state = JevSituationState.Build(situation);
        state["event"] = EventLine(evt);

        var questions = new Dictionary<string, JevQuestion>
        {
            [NextQuestion] = new(SystemOneApi.ChoiceType, Instructions, Options(choices)),
            [ActQuestion] = new(SystemOneApi.ChoiceType, ActInstructions, ActOptions)
        };

        return new JevDecision(state, questions);
    }

    /// <summary>
    /// The goal loop's next-job pick. The options are the scorer's allowed jobs, each with a
    /// what / not_for rubric. When someone is near, a greet noul rides the same call for free.
    /// </summary>
    public static JevDecision BuildNextJob(JevSituation situation, IReadOnlyList<JobOption> options, bool askGreet)
    {
        var questions = new Dictionary<string, JevQuestion>
        {
            [NextJobQuestion] = new(SystemOneApi.ChoiceType, NextJobInstructions, JevJobOptions.Criteria(options))
        };

        if (askGreet)
        {
            questions[GreetQuestion] = new(SystemOneApi.NoulType, GreetInstructions, GreetCriteria);
        }

        return new JevDecision(JevSituationState.Build(situation), questions);
    }

    /// <summary>
    /// The speech gate: a noul asked before a paid chat call, so a character only
    /// spends a generated reply on words they would actually answer. The state
    /// stays lean — this is a reflex, not a deliberation.
    /// </summary>
    public static JevDecision BuildGate(Persona persona, BrainEvent evt, LifePrompt life)
    {
        var state = new Dictionary<string, object>
        {
            ["who_you_are"] = Who(persona, evt),
            ["situation"] = Situation(evt),
            ["heard"] = new Dictionary<string, object>
            {
                ["speaker"] = string.IsNullOrWhiteSpace(evt?.SpeakerName) ? "someone" : evt.SpeakerName,
                ["their_words"] = evt?.Text ?? "",
                ["what_you_think_of_them"] = string.IsNullOrWhiteSpace(life?.Opinion) ? "plain" : life.Opinion
            }
        };

        var questions = new Dictionary<string, JevQuestion>
        {
            [ReplyQuestion] = new(
                SystemOneApi.NoulType,
                GateInstructions,
                new Dictionary<string, string>
                {
                    ["true"] = "the words were meant for them or invite an answer",
                    ["false"] = "overheard chatter, or they would stay quiet"
                }
            )
        };

        return new JevDecision(state, questions);
    }

    // The player-line intents Jev picks between. Keys are SpeechIntentKind names, so the answer
    // parses straight back into the enum the speech floor already routes on.
    private static readonly IReadOnlyDictionary<string, string> IntentOptions =
        new Dictionary<string, string>
        {
            [nameof(SpeechIntentKind.Greeting)] = "says hello or goodbye",
            [nameof(SpeechIntentKind.Question)] = "asks something and wants an answer",
            [nameof(SpeechIntentKind.Party)] = "wants to group up, hunt together, or join a group",
            [nameof(SpeechIntentKind.Trade)] = "wants to buy, sell, or trade goods",
            [nameof(SpeechIntentKind.Insult)] = "mocks, insults, or taunts",
            [nameof(SpeechIntentKind.Other)] = "small talk or anything else"
        };

    /// <summary>
    /// A typed read of a player's line, asked only when the free word lists could not place
    /// it, so the right handler runs: a group call goes to the party board, a trade line to
    /// the trade handler, an insult gets a brush-off, and the rest gets a generated reply.
    /// </summary>
    public static JevDecision BuildIntent(Persona persona, BrainEvent evt)
    {
        var state = new Dictionary<string, object>
        {
            ["who_you_are"] = Who(persona, evt),
            ["situation"] = Situation(evt),
            ["heard"] = new Dictionary<string, object>
            {
                ["speaker"] = string.IsNullOrWhiteSpace(evt?.SpeakerName) ? "someone" : evt.SpeakerName,
                ["their_words"] = evt?.Text ?? ""
            }
        };

        var questions = new Dictionary<string, JevQuestion>
        {
            [IntentQuestion] = new(SystemOneApi.ChoiceType, IntentInstructions, IntentOptions)
        };

        return new JevDecision(state, questions);
    }

    /// <summary>The intent Jev picked; an unknown or missing answer is Other.</summary>
    public static SpeechIntentKind ParseIntent(string choice) =>
        Enum.TryParse<SpeechIntentKind>(choice, ignoreCase: true, out var kind) && IntentOptions.ContainsKey(kind.ToString())
            ? kind
            : SpeechIntentKind.Other;

    public static IReadOnlyDictionary<string, string> Options(IReadOnlyList<ChoiceDefinition> choices)
    {
        var options = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        if (choices == null)
        {
            return options;
        }

        for (var i = 0; i < choices.Count && options.Count < JevJobOptions.MaxOptions; i++)
        {
            var routine = choices[i].Routine;

            if (string.IsNullOrWhiteSpace(routine))
            {
                continue;
            }

            options[routine.Trim()] = choices[i].Description;
        }

        return options;
    }

    /// <summary>
    /// Whether a decision event has options Jev could pick between. Calls with no
    /// options are not worth a request; the needs brain answers them for free.
    /// </summary>
    public static bool HasOptions(IReadOnlyList<ChoiceDefinition> choices)
    {
        if (choices == null)
        {
            return false;
        }

        for (var i = 0; i < choices.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(choices[i].Routine))
            {
                return true;
            }
        }

        return false;
    }

    private static string Who(Persona persona, BrainEvent evt)
    {
        persona ??= Persona.CreateNeutral();
        var name = string.IsNullOrWhiteSpace(persona.DisplayName) ? evt?.CharacterName : persona.DisplayName;
        var who = $"{name}, {JevSituationState.TemperWords(persona.ResolvedDrives())}.";

        if (!string.IsNullOrWhiteSpace(evt?.Identity))
        {
            who += " " + evt.Identity;
        }

        return string.IsNullOrWhiteSpace(persona.Background) ? who : who + " " + persona.Background;
    }

    private static Dictionary<string, object> Situation(BrainEvent evt) =>
        new()
        {
            ["place"] = string.IsNullOrWhiteSpace(evt?.Location) ? "Sosaria" : evt.Location,
            ["doing_now"] = string.IsNullOrWhiteSpace(evt?.CurrentActivity) ? "idle" : evt.CurrentActivity,
            ["status"] = StatusText(evt?.Text)
        };

    /// <summary>
    /// Decision events carry the character's status plus their routine list in one
    /// text block ("- id: description" lines). Jev gets the routines as typed
    /// options, so the state keeps only the status part.
    /// </summary>
    private static string StatusText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var optionsStart = text.IndexOf("\n- ", System.StringComparison.Ordinal);
        return optionsStart < 0 ? text : text[..optionsStart];
    }

    /// <summary>
    /// The decide-shape events (<see cref="PromptBuilder.UsesDecideShape"/>), minus the routine
    /// list a chat prompt appends: Jev gets the routines as typed options instead.
    /// </summary>
    private static string EventLine(BrainEvent evt)
    {
        if (evt == null)
        {
            return "Nothing happens.";
        }

        var speaker = string.IsNullOrWhiteSpace(evt.SpeakerName) ? "someone" : evt.SpeakerName;
        var kind = evt.SpeakerIsPlayer ? "player" : "labourer";

        return evt.Kind switch
        {
            BrainEventKind.Attacked => $"A {kind} named {speaker} attacks you.",
            BrainEventKind.DungeonEnded => "You finished a dungeon. Pick the next thing to do.",
            _ => $"A {kind} named {speaker} is nearby and idle. You may speak or pick the next thing to do."
        };
    }
}
