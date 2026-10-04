using System.Collections.Generic;
using System.Text;
using SosariaAI.Configuration;

namespace SosariaAI.Deliberation;

public sealed class LifePrompt
{
    public string Ambition { get; init; }
    public string AmbitionMood { get; init; }
    public string DayPart { get; init; }
    public string Opinion { get; init; }

    public IReadOnlyList<string> RecentSpeech { get; init; }

    public IReadOnlyList<string> Days { get; init; }

    /// <summary>The biggest adventures shared with the person present, biggest first.</summary>
    public IReadOnlyList<string> SharedAdventures { get; init; }

    public string ActionPhrase { get; init; }

    public int PackCount { get; init; }

    public int Gold { get; init; }

    public int Hits { get; init; }

    public int HitsMax { get; init; }

    public string PartyState { get; init; }

    public int DistanceFromHome { get; init; }
}

public static class PromptBuilder
{
    public const string JsonShape = """{"say": "text or empty string"}""";

    /// <summary>The decide answer's shape. Its act list is <see cref="ImmediateActs.AllowList"/>, so it never offers an act no code runs.</summary>
    public static readonly string DecideJsonShape =
        """{"choose": "<routine id>", "say": "optional short line", "act": "optional """
        + string.Join('|', ImmediateActs.AllowList)
        + "\"}";

    public const string PlanJsonShape =
        """{"goal":"short goal","reason":"why this character","success":"equipped-armor|gold-earned|item-obtained|party-joined|hunt-returned","steps":[{"do":"Mine","why":"earn ore","ref":""}],"say":"optional"}""";
    private const string MissingName = "someone";
    private const string PlayerLabel = "player";
    private const string LabourerLabel = "labourer";
    public const string WantPrefix = "Long goal, for some later day, not your plan right now: ";
    private const string DayPartPrefix = "Time of day: ";
    private const string OpinionPrefix = "About this person: ";
    public const string SharedAdventuresHeading = "What you did together with this person:";
    private const string LifeTalkRule =
        "Talk about your own life when it fits: what you are doing right now, who you just saw, what you did today. Your long goal is only a hope for later. Do not invent being an AI.";

    /// <summary>
    /// For every shape: no game code forms a group, a meeting or a trip from a model's words, so
    /// a line that offers one is a promise nobody keeps. A red camping Destard told a player
    /// "u coming or what" and "keep up", and no party formed when the player said yes.
    /// </summary>
    public const string NoPromiseRule =
        "Never invite anyone to come along, follow you, group up, hunt with you, or meet you; never promise to meet later; never agree to follow or travel with anyone. Nothing you say makes a group, a meeting, or a trip happen.";

    /// <summary>For chat only: a chat line changes no job, so it may only speak of the work at hand.</summary>
    public const string StayOnTaskRule =
        "Stay on what you are doing right now. You may say what you are doing and that you are here. Never say you are heading somewhere else: you will not go.";

    /// <summary>For the plan shape: its say is spoken as the plan starts, so it may only speak of the plan's own steps.</summary>
    public const string PlanSayRule =
        "Your say must match your steps: speak only about the steps you plan, or leave it empty. Never name a place, a person, or a plan your steps do not hold.";

    private const string DecideSayRule =
        "Your say must match your choose: speak about the routine you choose, or about what you are doing right now if you choose nothing. Never name a place or plan you did not choose.";
    private const string EraChatRule =
        "Chat like a 1999 Ultima Online player: short, mostly lowercase, casual slang such as lol, u, ty, gl; no roleplay actions or emotes.";

    public static ChatMessage[] Build(
        Persona persona,
        BrainEvent evt,
        IReadOnlyList<string> memory,
        int maxReplyCharacters,
        LifePrompt life = null,
        PlanFacts plan = null)
    {
        return
        [
            new ChatMessage { Role = ChatMessage.SystemRole, Content = BuildSystem(persona, evt, maxReplyCharacters, life, plan) },
            new ChatMessage { Role = ChatMessage.UserRole, Content = BuildUser(evt, memory) }
        ];
    }

    public static string BuildSystem(
        Persona persona,
        BrainEvent evt,
        int maxReplyCharacters,
        LifePrompt life = null,
        PlanFacts plan = null
    )
    {
        persona ??= Persona.CreateNeutral();
        var builder = new StringBuilder();

        builder.Append("You are ");
        builder.Append(string.IsNullOrWhiteSpace(persona.DisplayName) ? evt?.CharacterName : persona.DisplayName);
        builder.Append('.');
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(persona.Background))
        {
            builder.AppendLine(persona.Background);
        }

        if (!string.IsNullOrWhiteSpace(persona.Voice))
        {
            builder.Append("Voice: ");
            builder.AppendLine(persona.Voice);
        }

        AppendList(builder, "Likes", persona.Likes);
        AppendList(builder, "Dislikes", persona.Dislikes);

        if (UsesDecideShape(evt?.Kind))
        {
            var drives = persona.ResolvedDrives();
            builder.Append("Drives: greed ");
            builder.Append(drives.Greed);
            builder.Append(", caution ");
            builder.Append(drives.Caution);
            builder.Append(", valor ");
            builder.Append(drives.Valor);
            builder.Append('.');
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(evt?.Identity))
        {
            builder.Append(evt.Identity);
            builder.AppendLine();
        }

        builder.Append("You are in ");
        builder.Append(string.IsNullOrWhiteSpace(evt?.Location) ? "Sosaria" : evt.Location);
        builder.Append(". You are doing this right now: ");
        builder.Append(string.IsNullOrWhiteSpace(evt?.CurrentActivity) ? "idle" : evt.CurrentActivity);
        builder.Append('.');
        builder.AppendLine();

        if (life != null)
        {
            builder.Append(WantPrefix);
            builder.Append(life.Ambition);
            builder.Append(" (");
            builder.Append(life.AmbitionMood);
            builder.Append(')');
            builder.AppendLine();

            builder.Append(DayPartPrefix);
            builder.AppendLine(life.DayPart);

            builder.Append(OpinionPrefix);
            builder.AppendLine(life.Opinion);

            if (life.Days is { Count: > 0 })
            {
                builder.AppendLine("What you remember of your own days:");

                for (var i = 0; i < life.Days.Count; i++)
                {
                    builder.Append("- ");
                    builder.AppendLine(life.Days[i]);
                }
            }

            if (life.SharedAdventures is { Count: > 0 })
            {
                builder.AppendLine(SharedAdventuresHeading);

                for (var i = 0; i < life.SharedAdventures.Count; i++)
                {
                    builder.Append("- ");
                    builder.AppendLine(life.SharedAdventures[i]);
                }
            }

            builder.Append("Doing: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(life.ActionPhrase) ? "idle" : life.ActionPhrase);
            builder.Append("Pack of the work good: ");
            builder.Append(life.PackCount);
            builder.AppendLine();
            builder.Append("Gold on hand: ");
            builder.Append(life.Gold);
            builder.AppendLine();
            builder.Append("Hits: ");
            builder.Append(life.Hits);
            builder.Append('/');
            builder.Append(life.HitsMax);
            builder.AppendLine();
            builder.Append("Party: ");
            builder.AppendLine(string.IsNullOrWhiteSpace(life.PartyState) ? "none" : life.PartyState);
            builder.Append("Tiles from home: ");
            builder.Append(life.DistanceFromHome);
            builder.AppendLine();
            builder.AppendLine("Do not claim a full load, gold, or wounds the facts above contradict.");

            if (life.RecentSpeech is { Count: > 0 })
            {
                builder.AppendLine("Do not repeat these lines or the same idea in different words:");

                for (var i = 0; i < life.RecentSpeech.Count; i++)
                {
                    builder.Append("- ");
                    builder.AppendLine(life.RecentSpeech[i]);
                }
            }
        }

        if (plan != null)
        {
            builder.AppendLine(plan.ToPromptBlock());
        }

        builder.AppendLine(LifeTalkRule);
        builder.AppendLine(NoPromiseRule);

        if (UsesDecideShape(evt?.Kind))
        {
            builder.AppendLine(DecideSayRule);
        }

        if (UsesPlanShape(evt?.Kind))
        {
            builder.AppendLine(PlanSayRule);
            builder.Append("Hard rules: stay in character; the say field is at most ");
            builder.Append(maxReplyCharacters);
            builder.Append(" characters or empty; no modern words; never say you are an AI, a bot, a program, or in a game; do not invent coordinates or items; reply ONLY with a JSON object of this exact shape and nothing else: ");
            builder.Append(PlanJsonShape);
        }
        else
        {
            if (!UsesDecideShape(evt?.Kind))
            {
                builder.AppendLine(StayOnTaskRule);
            }

            builder.AppendLine(EraChatRule);
            builder.Append("Hard rules: answer in character; at most ");
            builder.Append(maxReplyCharacters);
            builder.Append(" characters; one or two short sentences; no modern words; never say you are an AI, a bot, a program, or in a game; if you have nothing to say, say nothing; reply ONLY with a JSON object of this exact shape and nothing else: ");
            builder.Append(ShapeFor(evt?.Kind));
        }

        return builder.ToString();
    }

    public static string BuildUser(BrainEvent evt, IReadOnlyList<string> memory)
    {
        var builder = new StringBuilder();
        builder.AppendLine(DescribeEvent(evt));

        if (memory != null && memory.Count > 0)
        {
            builder.AppendLine("Recent memory:");

            for (var i = 0; i < memory.Count; i++)
            {
                builder.Append("- ");
                builder.AppendLine(memory[i]);
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string DescribeEvent(BrainEvent evt)
    {
        if (evt == null)
        {
            return "Nothing happens.";
        }

        var speaker = string.IsNullOrWhiteSpace(evt.SpeakerName) ? MissingName : evt.SpeakerName;
        var kind = evt.SpeakerIsPlayer ? PlayerLabel : LabourerLabel;

        return evt.Kind switch
        {
            BrainEventKind.Spoken =>
                $"A {kind} named {speaker}, standing next to you, says: '{evt.Text}'",
            BrainEventKind.Attacked =>
                $"A {kind} named {speaker} attacks you.",
            BrainEventKind.Plan =>
                "Form a goal and a short plan of real actions. Game code will carry out each step.",
            BrainEventKind.DungeonEnded =>
                $"You finished a dungeon. Pick the next thing to do. Routines:\n{evt.Text}",
            BrainEventKind.Musing =>
                "Something just happened: '"
                + (string.IsNullOrWhiteSpace(evt.Text) ? "nothing" : evt.Text)
                + "'. Say one short thing about that event to people nearby. "
                + "Do not repeat an idea you already said. Leave say empty if you would keep quiet.",
            BrainEventKind.Reword =>
                $"Say this to {speaker} in your own words. Keep every number and every item exactly as written: '{evt.Text}'",
            BrainEventKind.PlayerNoticed =>
                $"A {kind} named {speaker} is nearby and idle. You may speak or pick the next thing to do. Routines:\n{evt.Text}",
            _ => "Something happens nearby."
        };
    }

    internal static bool UsesDecideShape(BrainEventKind? kind) =>
        kind is BrainEventKind.Attacked or BrainEventKind.DungeonEnded or BrainEventKind.PlayerNoticed;

    internal static bool UsesPlanShape(BrainEventKind? kind) => kind == BrainEventKind.Plan;

    internal static string ShapeFor(BrainEventKind? kind) =>
        UsesPlanShape(kind) ? PlanJsonShape : UsesDecideShape(kind) ? DecideJsonShape : JsonShape;

    private static void AppendList(StringBuilder builder, string label, List<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendJoin(", ", values);
        builder.AppendLine();
    }
}
