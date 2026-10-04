using System;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class JevPromptTests
{
    [Fact]
    public void Options_MapsRoutineIdsToDescriptions()
    {
        var options = JevPrompt.Options(Choices());

        Assert.Equal(2, options.Count);
        Assert.Equal("cut wood", options["work"]);
        Assert.Equal("rest", options["loiter"]);
    }

    [Fact]
    public void Options_SkipsBlankRoutineIds()
    {
        var choices = new List<ChoiceDefinition>
        {
            new() { Routine = " ", Description = "blank" },
            new() { Routine = "work", Description = "cut wood" }
        };

        var options = JevPrompt.Options(choices);

        Assert.Single(options);
        Assert.Equal("cut wood", options["work"]);
    }

    [Fact]
    public void Options_HandlesNullAndEmptyChoices()
    {
        Assert.Empty(JevPrompt.Options(null));
        Assert.Empty(JevPrompt.Options([]));
        Assert.False(JevPrompt.HasOptions(null));
        Assert.False(JevPrompt.HasOptions([new ChoiceDefinition { Routine = " " }]));
        Assert.True(JevPrompt.HasOptions(Choices()));
    }

    [Fact]
    public void Build_PutsTheWordStateAndTheEventInNamedFields()
    {
        var decision = JevPrompt.Build(Situation(), DecideEvent(), Choices());

        var state = (Dictionary<string, object>)decision.State;

        Assert.Equal("You are a veteran warrior.", state["who"]);
        Assert.Equal("greedy", state["temper"]);
        Assert.StartsWith("Britain, in town", (string)state["place"]);
        Assert.Equal("You finished a dungeon. Pick the next thing to do.", state["event"]);

        var next = decision.Questions[JevPrompt.NextQuestion];
        Assert.Equal(JevPrompt.Instructions, next.Instructions);
        Assert.Equal("cut wood", ((IReadOnlyDictionary<string, string>)next.Criteria)["work"]);
    }

    [Fact]
    public void Build_FansOutAnActQuestionForFree()
    {
        var decision = JevPrompt.Build(Situation(), DecideEvent(), Choices());

        var act = decision.Questions[JevPrompt.ActQuestion];

        Assert.Equal("choice", act.Type);
        var criteria = (IReadOnlyDictionary<string, string>)act.Criteria;
        Assert.Equal("nothing besides the routine", criteria["none"]);
        Assert.True(criteria.ContainsKey(ImmediateActs.Greet));
    }

    [Fact]
    public void Build_OffersOnlyTheActsCodeCarriesOut()
    {
        // Follow and rest were offered and nothing ran them, while the line that came with them was spoken.
        var act = JevPrompt.Build(Situation(), DecideEvent(), Choices()).Questions[JevPrompt.ActQuestion];
        var criteria = (IReadOnlyDictionary<string, string>)act.Criteria;

        Assert.Equal(ImmediateActs.AllowList.Length + 1, criteria.Count);
        Assert.True(criteria.ContainsKey("none"));

        foreach (var allowed in ImmediateActs.AllowList)
        {
            Assert.True(criteria.ContainsKey(allowed));
        }

        Assert.False(criteria.ContainsKey("follow"));
        Assert.False(criteria.ContainsKey("rest"));
    }

    [Fact]
    public void Build_SendsNoMemoryLinesAndNoStatusNumbers()
    {
        var decision = JevPrompt.Build(Situation(), DecideEvent(), Choices());
        var state = (Dictionary<string, object>)decision.State;

        Assert.False(state.ContainsKey("recent"));
        Assert.False(state.ContainsKey("situation"));
        Assert.DoesNotContain("percent", JsonSerializer.Serialize(state));
    }

    [Fact]
    public void Build_CapsTheRoutineOptions()
    {
        var many = new List<ChoiceDefinition>();

        for (var i = 0; i < JevJobOptions.MaxOptions * 2; i++)
        {
            many.Add(new ChoiceDefinition { Routine = $"routine{i}", Description = "a routine" });
        }

        var next = JevPrompt.Build(Situation(), DecideEvent(), many).Questions[JevPrompt.NextQuestion];

        Assert.Equal(JevJobOptions.MaxOptions, ((IReadOnlyDictionary<string, string>)next.Criteria).Count);
    }

    [Fact]
    public void BuildNextJob_AsksOneChoiceWithContrastiveCriteria()
    {
        var options = JevJobOptions.From(Ranking(8));
        var decision = JevPrompt.BuildNextJob(Situation(), options, askGreet: false);

        var next = decision.Questions[JevPrompt.NextJobQuestion];
        var criteria = (Dictionary<string, string>)next.Criteria;
        var first = criteria[options[0].Key];

        Assert.Equal("choice", next.Type);
        Assert.Same(JevPrompt.NextJobInstructions, next.Instructions);
        Assert.Equal(options.Count, criteria.Count);
        Assert.Contains(options[0].What, first);
        Assert.Contains(options[0].NotFor, first);
        Assert.False(decision.Questions.ContainsKey(JevPrompt.GreetQuestion));
    }

    [Fact]
    public void BuildNextJob_AddsTheGreetNoulOnlyWhenAsked()
    {
        var decision = JevPrompt.BuildNextJob(Situation(), JevJobOptions.From(Ranking(3)), askGreet: true);

        var greet = decision.Questions[JevPrompt.GreetQuestion];

        Assert.Equal("noul", greet.Type);
        Assert.Equal(JevPrompt.GreetInstructions, greet.Instructions);
    }

    [Fact]
    public void BuildNextJob_FullRequestStaysUnderTheTokenTarget()
    {
        var situation = Situation() with
        {
            Party = "Hope, Bran",
            Plan = "next: sell goods; job: mine ore, smelt, sell, bank, buy tools, and smith",
            Doing = "just finished: mine ore",
            Moment = "a hunt just ended"
        };
        var decision = JevPrompt.BuildNextJob(situation, JevJobOptions.From(Ranking(12)), askGreet: true);
        var tokens = Tokens(decision);

        Assert.True(tokens < TokenTarget, $"about {tokens} tokens: {Body(decision)}");
    }

    [Fact]
    public void BuildNextJob_TheMomentRidesInTheState()
    {
        var decision = JevPrompt.BuildNextJob(Situation() with { Moment = "back from death" }, JevJobOptions.From(Ranking(3)), askGreet: false);

        Assert.Equal("back from death", ((Dictionary<string, object>)decision.State)["moment"]);
    }

    [Fact]
    public void Build_EventDecisionStaysUnderTheTokenTarget()
    {
        var decision = JevPrompt.Build(Situation() with { Plan = "next: sell goods" }, DecideEvent(), Choices());
        var tokens = Tokens(decision);

        Assert.True(tokens < TokenTarget, $"about {tokens} tokens: {Body(decision)}");
    }

    /// <summary>The request body as the worker sends it, and its rough size in tokens.</summary>
    internal static string Body(JevDecision decision) =>
        JsonSerializer.Serialize(new { state = decision.State, model = "jev-latest", questions = decision.Questions }, JsonOptions);

    internal static int Tokens(JevDecision decision) => Body(decision).Length / CharactersPerToken;

    [Fact]
    public void BuildIntent_AsksWhatThePlayerWants()
    {
        var decision = JevPrompt.BuildIntent(
            Persona.CreateNeutral(),
            DecideEvent() with { SpeakerName = "Hope", Text = "anyone up for orcs" }
        );

        var intent = decision.Questions[JevPrompt.IntentQuestion];
        var criteria = (IReadOnlyDictionary<string, string>)intent.Criteria;

        Assert.Equal("choice", intent.Type);
        Assert.Equal(JevPrompt.IntentInstructions, intent.Instructions);
        Assert.True(criteria.ContainsKey(nameof(SpeechIntentKind.Party)));
        Assert.True(criteria.ContainsKey(nameof(SpeechIntentKind.Trade)));
        Assert.True(criteria.ContainsKey(nameof(SpeechIntentKind.Insult)));

        var heard = (Dictionary<string, object>)((Dictionary<string, object>)decision.State)["heard"];
        Assert.Equal("anyone up for orcs", heard["their_words"]);
    }

    [Theory]
    [InlineData("Party", SpeechIntentKind.Party)]
    [InlineData("trade", SpeechIntentKind.Trade)]
    [InlineData("Insult", SpeechIntentKind.Insult)]
    [InlineData("NameCall", SpeechIntentKind.Other)]
    [InlineData("nonsense", SpeechIntentKind.Other)]
    [InlineData(null, SpeechIntentKind.Other)]
    public void ParseIntent_KnownChoicesOnly(string choice, SpeechIntentKind expected)
    {
        Assert.Equal(expected, JevPrompt.ParseIntent(choice));
    }

    [Fact]
    public void BuildGate_AsksANoulWithALeanState()
    {
        var decision = JevPrompt.BuildGate(
            Persona.CreateNeutral(),
            DecideEvent() with { SpeakerName = "Hope", Text = "well met, Connor" },
            new LifePrompt { Opinion = "warm (saved me once)" }
        );

        var reply = decision.Questions[JevPrompt.ReplyQuestion];

        Assert.Equal("noul", reply.Type);
        Assert.Equal(JevPrompt.GateInstructions, reply.Instructions);

        var state = (Dictionary<string, object>)decision.State;
        var heard = (Dictionary<string, object>)state["heard"];

        Assert.Equal("Hope", heard["speaker"]);
        Assert.Equal("well met, Connor", heard["their_words"]);
        Assert.Equal("warm (saved me once)", heard["what_you_think_of_them"]);
        Assert.False(state.ContainsKey("recent_memory"));
    }

    [Fact]
    public void Build_DescribesTheEventWithoutRoutineText()
    {
        var attacked = DecideEvent() with
        {
            Kind = BrainEventKind.Attacked,
            SpeakerName = "Bran",
            SpeakerIsPlayer = true
        };

        var decision = JevPrompt.Build(Situation(), attacked, Choices());
        var state = (Dictionary<string, object>)decision.State;

        Assert.Equal("A player named Bran attacks you.", state["event"]);
    }

    [Fact]
    public void Who_GivesDrivesAsWordsNotNumbers()
    {
        var decision = JevPrompt.BuildGate(
            new Persona { DisplayName = "Connor", Drives = new Dictionary<string, double> { ["greed"] = 0.7 } },
            DecideEvent() with { SpeakerName = "Hope", Text = "hail" },
            new LifePrompt()
        );

        var who = (string)((Dictionary<string, object>)decision.State)["who_you_are"];

        Assert.Contains("Connor, greedy", who);
        Assert.DoesNotContain("0.7", who);
    }

    private static BrainEvent DecideEvent() =>
        new(
            BrainEventKind.DungeonEnded,
            (Serial)1u,
            "Connor",
            null,
            Serial.Zero,
            false,
            "Hits 90 percent. Gold in bank 10, carried 5. Pack 30 percent. Time 10:00.\n- work: cut wood\n- loiter: rest",
            null,
            "Britain",
            DateTime.UtcNow
        );

    // A rough English token size: about four characters a token. The live count is in the
    // hourly usage line from the API's usage field.
    private const int CharactersPerToken = 4;

    /// <summary>The owner's target for one Jev request, the API's own framing aside.</summary>
    internal const int TokenTarget = 350;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static JevSituation Situation() =>
        new()
        {
            Identity = "You are a veteran warrior.",
            Drives = new PersonaDrives(0.8, 0.5, 0.5, isCustom: true),
            Want = "save for a house",
            Mood = "hopeful",
            DayPart = "Morning",
            Place = "Britain",
            InTown = true,
            DistanceFromHome = 20,
            Hits = 90,
            HitsMax = 100,
            Mana = 20,
            ManaMax = 20,
            Gold = 700,
            PackFill = 0.5,
            GoodsToSell = true,
            SinceHunt = TimeSpan.FromHours(2)
        };

    /// <summary>A scorer ranking of distinct jobs, best first.</summary>
    internal static ScoreResult Ranking(int count)
    {
        string[] kinds =
        [
            SkillKinds.Mine, SkillKinds.VendorSell, SkillKinds.BankDeposit, SkillKinds.Hunt, SkillKinds.Rest,
            SkillKinds.Tavern, SkillKinds.Sightsee, SkillKinds.Smith, SkillKinds.GoHome, SkillKinds.Follow,
            SkillKinds.Fish, SkillKinds.Sword
        ];
        var ranked = new List<ScoredAction>();

        for (var i = 0; i < count; i++)
        {
            var kind = kinds[i % kinds.Length];
            ranked.Add(new ScoredAction(new ActionId($"miner:{kind}:Minoc Place {(char)('A' + i)}"), kind, "miner", count - i, "ok"));
        }

        return new ScoreResult { Ranked = ranked, Winner = ranked[0] };
    }

    private static List<ChoiceDefinition> Choices() =>
        [
            new ChoiceDefinition { Routine = "work", Description = "cut wood" },
            new ChoiceDefinition { Routine = "loiter", Description = "rest" }
        ];
}
