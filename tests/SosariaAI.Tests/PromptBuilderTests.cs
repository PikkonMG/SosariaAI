using Server;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void Build_IncludesPersonaActivityRulesAndJsonInstruction()
    {
        var persona = PersonasFile.CreateDefaultConnor();
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "where is the bank?",
            "Lumberjack: carrying 12 logs",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var messages = PromptBuilder.Build(persona, evt, ["I banked 80 logs."], 160);

        Assert.Equal(2, messages.Length);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[1].Role);

        var system = messages[0].Content;
        Assert.Contains("Born in Britain", system);
        Assert.Contains("Short sentences", system);
        Assert.Contains("a good axe", system);
        Assert.Contains("thieves", system);
        Assert.Contains("Britain, Felucca", system);
        Assert.Contains("Lumberjack: carrying 12 logs", system);
        Assert.Contains("at most 160 characters", system);
        Assert.Contains(PromptBuilder.JsonShape, system);
        Assert.Contains("never say you are an AI", system);

        var user = messages[1].Content;
        Assert.Contains("A player named Bob", user);
        Assert.Contains("where is the bank?", user);
        Assert.Contains("- I banked 80 logs.", user);
    }

    [Fact]
    public void Build_ChatAsksFor1999PlayerChat()
    {
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "hi",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var system = PromptBuilder.Build(PersonasFile.CreateDefaultConnor(), evt, [], 160)[0].Content;

        Assert.Contains("1999 Ultima Online player", system);
        Assert.Contains("no roleplay actions", system);
    }

    [Fact]
    public void DescribeEvent_RewordKeepsTheNumbers()
    {
        var evt = new BrainEvent(
            BrainEventKind.Reword,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "katana 400gp",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var described = PromptBuilder.DescribeEvent(evt);

        Assert.Contains("'katana 400gp'", described);
        Assert.Contains("Keep every number", described);
        Assert.Contains("Bob", described);
    }

    [Fact]
    public void BuildSystem_WithLifePrompt_IncludesAmbition()
    {
        var persona = PersonasFile.CreateDefaultConnor();
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "hello",
            "Lumberjack: carrying 12 logs",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );
        var life = new LifePrompt
        {
            Ambition = "own a small boat",
            AmbitionMood = "hopeful",
            DayPart = "morning",
            Opinion = "friendly stranger",
            ActionPhrase = "cut wood",
            PackCount = 0,
            Gold = 40,
            Hits = 80,
            HitsMax = 80,
            PartyState = "none",
            DistanceFromHome = 12
        };

        var system = PromptBuilder.BuildSystem(persona, evt, 160, life);

        Assert.Contains("small boat", system);
        Assert.Contains(PromptBuilder.WantPrefix + "own a small boat (hopeful)", system);
        Assert.Contains("Time of day: morning", system);
        Assert.Contains("About this person: friendly stranger", system);
        Assert.Contains("Doing: cut wood", system);
        Assert.Contains("Pack of the work good: 0", system);
        Assert.Contains("Gold on hand: 40", system);
        Assert.Contains("Hits: 80/80", system);
        Assert.Contains("Tiles from home: 12", system);
        Assert.Contains("Talk about your own life when it fits", system);
    }

    [Fact]
    public void BuildSystem_WithNullLife_DoesNotMentionWant()
    {
        var persona = PersonasFile.CreateDefaultConnor();
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "hello",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var system = PromptBuilder.BuildSystem(persona, evt, 160, null);

        Assert.DoesNotContain(PromptBuilder.WantPrefix, system);
    }

    [Theory]
    [InlineData(BrainEventKind.PlayerNoticed, true)]
    [InlineData(BrainEventKind.DungeonEnded, true)]
    [InlineData(BrainEventKind.Spoken, false)]
    public void BuildSystem_DecideShape_TiesTheLineToTheChoice(BrainEventKind kind, bool expected)
    {
        var persona = PersonasFile.CreateDefaultConnor();
        var evt = new BrainEvent(
            kind,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "hello",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var system = PromptBuilder.BuildSystem(persona, evt, 160, null);

        Assert.Equal(expected, system.Contains("Your say must match your choose", System.StringComparison.Ordinal));
    }

    [Fact]
    public void DescribeEvent_DungeonEnded_ListsTheRoutines()
    {
        var evt = new BrainEvent(
            BrainEventKind.DungeonEnded,
            (Serial)1u,
            "Connor",
            null,
            (Serial)0u,
            false,
            "tavern, work, loiter",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var text = PromptBuilder.DescribeEvent(evt);

        Assert.Contains("next thing to do", text);
        Assert.Contains("tavern, work, loiter", text);
        Assert.DoesNotContain("next job", text);
    }

    [Fact]
    public void DescribeEvent_Plan_AsksForAGoalAndSteps()
    {
        var evt = new BrainEvent(
            BrainEventKind.Plan,
            (Serial)1u,
            "Connor",
            null,
            (Serial)0u,
            false,
            "NoPlan",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var text = PromptBuilder.DescribeEvent(evt);
        Assert.Contains("goal", text);
        Assert.Contains("plan", text);
        Assert.Equal(PromptBuilder.PlanJsonShape, PromptBuilder.ShapeFor(BrainEventKind.Plan));

        var persona = PersonasFile.CreateDefaultConnor();
        var system = PromptBuilder.BuildSystem(persona, evt, 160);
        Assert.Contains("the say field is at most 160", system);
        Assert.Contains(PromptBuilder.PlanJsonShape, system);
        Assert.DoesNotContain("at most 160 characters; one or two short sentences", system);
    }

    [Theory]
    [InlineData(BrainEventKind.Spoken, true)]
    [InlineData(BrainEventKind.Musing, true)]
    [InlineData(BrainEventKind.PlayerNoticed, false)]
    [InlineData(BrainEventKind.Plan, false)]
    public void BuildSystem_ChatStaysOnTheWorkAtHand(BrainEventKind kind, bool expected)
    {
        // Leander, a red camping Destard with a want of "traveling to Minoc", told a player
        // "heading to minoc, u coming" and then "keep up, i dont wait long", and never left.
        var evt = new BrainEvent(
            kind,
            (Serial)1u,
            "Leander",
            "Wystan",
            (Serial)2u,
            true,
            "hey",
            "Conflict: patrol destard to kill players",
            "Destard, Felucca",
            new System.DateTime(2026, 1, 1)
        );

        var system = PromptBuilder.BuildSystem(PersonasFile.CreateDefaultConnor(), evt, 160, null);

        Assert.Equal(expected, system.Contains(PromptBuilder.StayOnTaskRule, System.StringComparison.Ordinal));
    }

    [Fact]
    public void BuildSystem_WantReadsAsALaterGoalNotAPlan()
    {
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Leander",
            "Wystan",
            (Serial)2u,
            true,
            "hey",
            "Conflict: patrol destard to kill players",
            "Destard, Felucca",
            new System.DateTime(2026, 1, 1)
        );
        var life = new LifePrompt { Ambition = "traveling to Minoc (0 of 1)", AmbitionMood = "just started" };

        var system = PromptBuilder.BuildSystem(PersonasFile.CreateDefaultConnor(), evt, 160, life);

        Assert.Contains("not your plan right now", PromptBuilder.WantPrefix);
        Assert.Contains(PromptBuilder.WantPrefix + "traveling to Minoc (0 of 1)", system);
        Assert.DoesNotContain("what you want,", system);
    }

    [Theory]
    [InlineData(BrainEventKind.Spoken)]
    [InlineData(BrainEventKind.Musing)]
    [InlineData(BrainEventKind.Reword)]
    [InlineData(BrainEventKind.PlayerNoticed)]
    [InlineData(BrainEventKind.Attacked)]
    [InlineData(BrainEventKind.Plan)]
    public void BuildSystem_EveryShapeForbidsInvitesAndMeetings(BrainEventKind kind)
    {
        // No game code forms a group, a meeting or a trip from a model's words.
        var system = PromptBuilder.BuildSystem(PersonasFile.CreateDefaultConnor(), LeanderEvent(kind), 160, null);

        Assert.Contains(PromptBuilder.NoPromiseRule, system);
    }

    [Fact]
    public void StayOnTaskRule_OffersNoInviteToJoinTheWork()
    {
        // "You may ask someone next to you to join the work" let a bot invite a player when no party could form.
        Assert.DoesNotContain("join", PromptBuilder.StayOnTaskRule, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ask someone", PromptBuilder.StayOnTaskRule, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("follow", PromptBuilder.NoPromiseRule, System.StringComparison.Ordinal);
        Assert.Contains("meet", PromptBuilder.NoPromiseRule, System.StringComparison.Ordinal);
        Assert.Contains("travel with", PromptBuilder.NoPromiseRule, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(BrainEventKind.Plan, true)]
    [InlineData(BrainEventKind.PlayerNoticed, false)]
    [InlineData(BrainEventKind.Spoken, false)]
    public void BuildSystem_PlanSayMatchesItsSteps(BrainEventKind kind, bool expected)
    {
        var system = PromptBuilder.BuildSystem(PersonasFile.CreateDefaultConnor(), LeanderEvent(kind), 160, null);

        Assert.Equal(expected, system.Contains(PromptBuilder.PlanSayRule, System.StringComparison.Ordinal));
    }

    [Fact]
    public void DecideJsonShape_OffersOnlyTheActsCodeCarriesOut()
    {
        // Rest and follow were offered and nothing ran them, while their line was still spoken.
        using var shape = System.Text.Json.JsonDocument.Parse(PromptBuilder.DecideJsonShape);
        var act = shape.RootElement.GetProperty("act").GetString();

        Assert.Equal("optional greet|go_hunt|go_town", act);
        Assert.Equal("optional " + string.Join('|', ImmediateActs.AllowList), act);
        Assert.DoesNotContain("rest", act, System.StringComparison.Ordinal);
        Assert.DoesNotContain("follow", act, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSystem_PlanPromptSaysEachLifeFactOnce()
    {
        var life = new LifePrompt { Ambition = "traveling to Minoc", AmbitionMood = "just started", Gold = 40, Hits = 70, HitsMax = 80 };
        var plan = new PlanFacts { Role = "red", BankGold = 300 };

        var system = PromptBuilder.BuildSystem(PersonasFile.CreateDefaultConnor(), LeanderEvent(BrainEventKind.Plan), 160, life, plan);

        Assert.Single(Occurrences(system, PromptBuilder.WantPrefix));
        Assert.Single(Occurrences(system, "Hits: "));
        Assert.Single(Occurrences(system, "Gold on hand: "));
        Assert.Contains(PromptBuilder.WantPrefix + "traveling to Minoc (just started)", system);
        Assert.Contains("Role: red", system);
        Assert.Contains("Gold in bank: 300", system);
    }

    private static System.Collections.Generic.List<int> Occurrences(string text, string part)
    {
        var found = new System.Collections.Generic.List<int>();

        for (var at = text.IndexOf(part, System.StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(part, at + part.Length, System.StringComparison.Ordinal))
        {
            found.Add(at);
        }

        return found;
    }

    private static BrainEvent LeanderEvent(BrainEventKind kind) =>
        new(
            kind,
            (Serial)1u,
            "Leander",
            "Wystan",
            (Serial)2u,
            true,
            "hey",
            "Conflict: patrol destard to kill players",
            "Destard, Felucca",
            new System.DateTime(2026, 1, 1)
        );
}
