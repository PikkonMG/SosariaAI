using System.Collections.Generic;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Memory;
using Xunit;
using static SosariaAI.Tests.RecallTestStore;

namespace SosariaAI.Tests;

public class RecallPromptTests
{
    private const int HoursAgo = 2;
    private const int DaysAgo = 3;
    private const int ManyRuns = 5;
    private const int ManyLines = 12;
    private const int FewThoughts = 2;
    private const int ReplyCharacters = 160;
    private const string Shame = "Shame";
    private const string MiningSummary = "Mine done at Minoc";

    [Fact]
    public void SharedFacts_ArePromptFactsForThePersonPresent_BoundAtThree()
    {
        using var memory = new RecallTestStore();

        for (var i = 0; i < ManyRuns; i++)
        {
            memory.Record(
                AdventureKinds.Dungeon,
                Despise,
                Now.AddHours(-HoursAgo - i),
                "Cleared Despise",
                (Halvard, AdventureRoles.With),
                (Tamsin, AdventureRoles.Healer)
            );
        }

        memory.Record(AdventureKinds.Hunt, Shame, Now.AddHours(-HoursAgo), "Hunted at Shame", (Halvard, AdventureRoles.With), (Bryn, AdventureRoles.With));

        var facts = Recall.SharedFacts(memory.Store, Halvard.Id, Tamsin.Id, Now, PlanFacts.MaxShared);

        Assert.Equal(PlanFacts.MaxShared, facts.Count);
        Assert.All(facts, fact => Assert.Contains("Cleared Despise", fact));
        Assert.All(facts, fact => Assert.Contains(Tamsin.Name, fact));
        Assert.All(facts, fact => Assert.DoesNotContain(Shame, fact));
        Assert.Empty(Recall.SharedFacts(memory.Store, Halvard.Id, null, Now, PlanFacts.MaxShared));
    }

    [Fact]
    public void Fact_SaysWhenWhoStoodWithAndAgainst()
    {
        using var memory = new RecallTestStore();
        memory.Record(
            AdventureKinds.Dungeon,
            Despise,
            Now.AddDays(-1),
            string.Empty,
            (Halvard, AdventureRoles.With),
            (Bryn, AdventureRoles.Healer),
            (Grim, AdventureRoles.Against)
        );

        var fact = Recall.Fact(memory.Store.AdventuresOf(Halvard.Id)[0], Halvard.Id, Now);

        Assert.Equal("yesterday: dungeon at Despise, with Bryn, against Grim", fact);
    }

    [Fact]
    public void Fact_DoesNotRepeatTheCountsTheSummaryTells()
    {
        const int kills = 34;
        const int deaths = 1;
        using var memory = new RecallTestStore();
        var summary = AdventureRules.PartySummary(Despise, kills, [(Bryn.Name, deaths)]);
        memory.Store.Record(
            new Adventure
            {
                Kind = AdventureKinds.Dungeon,
                Place = Despise,
                StartedAt = Now.AddDays(-1),
                EndedAt = Now.AddDays(-1),
                Kills = kills,
                Deaths = deaths,
                Summary = summary,
                Members = [new AdventureMember(Halvard, AdventureRoles.With), new AdventureMember(Bryn, AdventureRoles.Fallen)]
            }
        );

        var fact = Recall.Fact(memory.Store.AdventuresOf(Halvard.Id)[0], Halvard.Id, Now);

        Assert.Equal($"yesterday: {summary}", fact);
    }

    [Fact]
    public void Days_AreTodayAndYesterday_OldestFirst()
    {
        using var memory = new RecallTestStore();
        memory.Record(AdventureKinds.Outing, Despise, Now.AddDays(-DaysAgo), "Hunt done at Despise", (Halvard, AdventureRoles.With));
        memory.Record(AdventureKinds.Outing, Despise, Now.AddDays(-1), "Fish done at Despise", (Halvard, AdventureRoles.With));
        memory.Record(AdventureKinds.Outing, Britain, Now.AddHours(-HoursAgo), MiningSummary, (Halvard, AdventureRoles.With));

        var days = Recall.Days(memory.Store, Halvard.Id, Now, PlanFacts.MaxMemory);

        Assert.Equal(["yesterday: Fish done at Despise", "today: Mine done at Minoc at Britain"], days);
    }

    [Fact]
    public void Memories_KeepAdventuresThenThoughts_InsideMaxMemory()
    {
        var adventures = Lines("adventure", ManyLines);
        var thoughts = Lines("thought", ManyLines);

        var both = PlanFacts.MemoriesFrom(adventures, thoughts);
        var noThoughts = PlanFacts.MemoriesFrom(adventures, []);
        var fewThoughts = PlanFacts.MemoriesFrom(adventures, Lines("thought", FewThoughts));

        Assert.Equal(PlanFacts.MaxMemory, both.Count);
        Assert.Equal("adventure" + (ManyLines - PlanFacts.MinThoughts), both[0]);
        Assert.Equal("thought" + (ManyLines - 1), both[^1]);
        Assert.Equal(PlanFacts.MaxMemory, noThoughts.Count);
        Assert.Equal("adventure" + (ManyLines - 1), noThoughts[^1]);
        Assert.Equal(PlanFacts.MaxMemory, fewThoughts.Count);
        Assert.Equal("thought" + (FewThoughts - 1), fewThoughts[^1]);
        Assert.Empty(PlanFacts.MemoriesFrom([], []));
    }

    [Fact]
    public void Latest_IsTheNewestAdventures_OldestFirst()
    {
        using var memory = new RecallTestStore();

        for (var i = ManyRuns; i > 0; i--)
        {
            memory.Record(AdventureKinds.Outing, Britain, Now.AddHours(-i), "Mine done " + i, (Halvard, AdventureRoles.With));
        }

        var latest = Recall.Latest(memory.Store, Halvard.Id, Now, FewThoughts);

        Assert.Equal(["today: Mine done 2 at Britain", "today: Mine done 1 at Britain"], latest);
    }

    [Fact]
    public void BuildSystem_CarriesTheSharedAdventures()
    {
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            Halvard.Name,
            Tamsin.Name,
            (Serial)2u,
            true,
            "hey",
            "idle",
            Britain,
            Now
        );
        var life = new LifePrompt { SharedAdventures = ["yesterday: Cleared Despise, with Tamsin"] };

        var system = PromptBuilder.BuildSystem(null, evt, ReplyCharacters, life);

        Assert.Contains(PromptBuilder.SharedAdventuresHeading, system);
        Assert.Contains("- yesterday: Cleared Despise, with Tamsin", system);
    }

    private static List<string> Lines(string word, int count)
    {
        var lines = new List<string>(count);

        for (var i = 0; i < count; i++)
        {
            lines.Add(word + i);
        }

        return lines;
    }
}
