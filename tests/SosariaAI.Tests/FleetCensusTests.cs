using System;
using System.Collections.Generic;
using SosariaAI.Admin;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class FleetCensusTests
{
    private const string Felucca = "Felucca";
    private const string Trammel = "Trammel";
    private const string Britain = "Britain, Felucca";
    private const string Minoc = "Minoc, Felucca";
    private const uint LeaderA = 100;
    private const uint LeaderB = 200;
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static CharacterSample Live(
        string facet = Felucca,
        string activity = "GoTo",
        string job = "bank",
        string place = Britain,
        bool red = false,
        bool ghost = false,
        bool combat = false,
        bool dungeon = false,
        bool suppliesLow = false,
        uint party = CharacterSample.NoParty,
        int chat = 0,
        int plan = 0
    ) =>
        new(facet, red, ghost, combat, dungeon, suppliesLow, party, activity, job, place, chat, plan);

    [Fact]
    public void Of_CountsLiveByFacet()
    {
        var counts = FleetCounts.Of([Live(), Live(), Live(facet: Trammel)]);

        Assert.Equal(3, counts.Live);
        Assert.Equal(2, counts.LiveByFacet[Felucca]);
        Assert.Equal(1, counts.LiveByFacet[Trammel]);
    }

    [Fact]
    public void Of_CountsStates()
    {
        var counts = FleetCounts.Of(
            [Live(red: true, combat: true), Live(ghost: true, dungeon: true, suppliesLow: true), Live(red: true)]
        );

        Assert.Equal(2, counts.Reds);
        Assert.Equal(1, counts.Ghosts);
        Assert.Equal(1, counts.InCombat);
        Assert.Equal(1, counts.InDungeons);
        Assert.Equal(1, counts.SuppliesLow);
        Assert.False(counts.ByActivity.ContainsKey(FleetCounts.UnknownKey));
    }

    [Fact]
    public void Of_PartiesCountDistinctLeaders()
    {
        var counts = FleetCounts.Of([Live(party: LeaderA), Live(party: LeaderA), Live(party: LeaderB), Live()]);

        Assert.Equal(2, counts.Parties);
    }

    [Fact]
    public void Of_ModelCallsAddUp()
    {
        var counts = FleetCounts.Of([Live(chat: 3, plan: 2), Live(chat: 4)]);

        Assert.Equal(7, counts.ChatCalls);
        Assert.Equal(2, counts.PlanCalls);
    }

    [Fact]
    public void Of_BlankKeysCountAsUnknown()
    {
        var counts = FleetCounts.Of([Live(activity: null, job: " ", place: "")]);

        Assert.Equal(1, counts.ByActivity[FleetCounts.UnknownKey]);
        Assert.Equal(1, counts.ByJob[FleetCounts.UnknownKey]);
        Assert.Equal(1, counts.ByPlace[FleetCounts.UnknownKey]);
    }

    [Fact]
    public void Top_LargestFirstThenByName()
    {
        var counts = FleetCounts.Of([Live(place: Minoc), Live(place: Britain), Live(place: Britain), Live(place: "Cove")]);

        var top = FleetCounts.Top(counts.ByPlace, 2);

        Assert.Equal(2, top.Count);
        Assert.Equal(Britain, top[0].Key);
        Assert.Equal(2, top[0].Value);
        Assert.Equal("Cove", top[1].Key);
    }

    [Fact]
    public void Top_NegativeLimitKeepsAll()
    {
        var counts = FleetCounts.Of([Live(job: "bank"), Live(job: "hunt"), Live(job: "craft")]);

        Assert.Equal(3, FleetCounts.Top(counts.ByJob, -1).Count);
    }

    private static ShardEvent Event(string type, TimeSpan ago, string victim = "Erol", string killer = "Vex") =>
        new() { Type = type, At = Now - ago, Actor = victim, Other = killer, Place = Britain };

    [Fact]
    public void Within_CountsTheLastHourOnly()
    {
        var events = new List<ShardEvent>
        {
            Event(ShardEventType.Pk, TimeSpan.FromMinutes(5)),
            Event(ShardEventType.Pk, TimeSpan.FromMinutes(90)),
            Event(ShardEventType.Death, TimeSpan.FromMinutes(10)),
            Event(ShardEventType.Death, TimeSpan.FromMinutes(59)),
            Event(ShardEventType.Theft, TimeSpan.FromMinutes(1)),
            Event(ShardEventType.Death, TimeSpan.FromMinutes(-1))
        };

        var tally = JournalTally.Within(events, Now, JournalTally.Window, StatusPage.MurderLimit);

        Assert.Equal(1, tally.Murders);
        Assert.Equal(2, tally.Deaths);
    }

    [Fact]
    public void Within_NewestMurdersFirstUpToTheLimit()
    {
        var events = new List<ShardEvent>
        {
            Event(ShardEventType.Pk, TimeSpan.FromMinutes(30), victim: "Old"),
            Event(ShardEventType.Pk, TimeSpan.FromMinutes(2), victim: "New"),
            Event(ShardEventType.Pk, TimeSpan.FromMinutes(10), victim: "Mid")
        };

        var tally = JournalTally.Within(events, Now, JournalTally.Window, 2);

        Assert.Equal(3, tally.Murders);
        Assert.Equal(2, tally.RecentMurders.Count);
        Assert.Equal("New", tally.RecentMurders[0].Actor);
        Assert.Equal("Mid", tally.RecentMurders[1].Actor);
    }

    [Fact]
    public void Within_NoJournalIsEmpty()
    {
        var tally = JournalTally.Within(null, Now, JournalTally.Window, StatusPage.MurderLimit);

        Assert.Equal(0, tally.Murders);
        Assert.Equal(0, tally.Deaths);
        Assert.Empty(tally.RecentMurders);
    }
}
