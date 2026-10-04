using System;
using Server;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class EventJournalTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);
    private static readonly Point3D Speaker = new(100, 100, 0);

    [Fact]
    public void Record_IgnoresNull()
    {
        var journal = new EventJournal();
        journal.Record(null);
        Assert.Empty(journal.Snapshot());
    }

    [Fact]
    public void Record_SnapshotKeepsOrder()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now, "first", 200, 200));
        journal.Record(Make(Now, "second", 200, 200));
        var snap = journal.Snapshot();
        Assert.Equal(2, snap.Count);
        Assert.Equal("first", snap[0].Actor);
        Assert.Equal("second", snap[1].Actor);
    }

    [Fact]
    public void PickGossip_SameSpotYoungerThanHereAge_ReturnsNull()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - TimeSpan.FromMinutes(5), "bran", Speaker.X, Speaker.Y));
        Assert.Null(journal.PickGossip("sela", Speaker, Now));
    }

    [Fact]
    public void PickGossip_AfterNewsTravel_ReturnsEvent()
    {
        var journal = new EventJournal();
        var evt = Make(Now - TimeSpan.FromSeconds(200), "bran", Speaker.X + 100, Speaker.Y);
        journal.Record(evt);
        var picked = journal.PickGossip("sela", Speaker, Now);
        Assert.Same(evt, picked);
        Assert.Equal(1, evt.TellCount);
    }

    [Fact]
    public void PickGossip_NewsNotYetArrived_ReturnsNull()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - TimeSpan.FromSeconds(199), "bran", Speaker.X + 100, Speaker.Y));
        Assert.Null(journal.PickGossip("sela", Speaker, Now));
    }

    [Fact]
    public void PickGossip_OlderThanMaxAge_ReturnsNull()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - GossipRules.MaxAge - TimeSpan.FromSeconds(1), "bran", Speaker.X + 200, Speaker.Y));
        Assert.Null(journal.PickGossip("sela", Speaker, Now));
    }

    [Fact]
    public void PickGossip_FutureEvent_ReturnsNull()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now.AddSeconds(1), "bran", Speaker.X + 200, Speaker.Y));
        Assert.Null(journal.PickGossip("sela", Speaker, Now));
    }

    [Fact]
    public void PickGossip_OwnEventAtSameSpot_IsNotToldWhereItHappened()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now, "bran", Speaker.X, Speaker.Y));
        Assert.Null(journal.PickGossip("Bran", Speaker, Now));
    }

    [Fact]
    public void PickGossip_OldEventAtSameSpot_IsStillNotTold()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - TimeSpan.FromHours(1), "bran", Speaker.X + 10, Speaker.Y));
        Assert.Null(journal.PickGossip("sela", Speaker, Now));
    }

    [Fact]
    public void PickGossip_OwnEventFarAway_IsToldAtOnce()
    {
        var journal = new EventJournal();
        var evt = Make(Now, "bran", Speaker.X + 500, Speaker.Y);
        journal.Record(evt);
        Assert.Same(evt, journal.PickGossip("Bran", Speaker, Now));
    }

    [Fact]
    public void PickGossip_FarNews_FadesOnAHighRoll()
    {
        var journal = new EventJournal();
        var evt = Make(Now - TimeSpan.FromMinutes(50), "bran", Speaker.X + 800, Speaker.Y);
        journal.Record(evt);

        Assert.Null(journal.PickGossip("sela", Speaker, Now, fadeRoll: GossipRules.VagueRetellPercent));
        Assert.Equal(0, evt.TellCount);
        Assert.Same(evt, journal.PickGossip("sela", Speaker, Now, fadeRoll: 0));
    }

    [Fact]
    public void PickGossip_RedSighting_IsToldOnce()
    {
        var journal = new EventJournal();
        var red = Make(Now - TimeSpan.FromHours(1), "grim", Speaker.X + 200, Speaker.Y, ShardEventType.Red);
        journal.Record(red);

        Assert.Same(red, journal.PickGossip("sela", Speaker, Now));
        Assert.Null(journal.PickGossip("tam", Speaker, Now + TimeSpan.FromHours(1)));
    }

    [Fact]
    public void PickGossip_SameStory_RestsBeforeTheNextTelling()
    {
        var journal = new EventJournal();
        var evt = Make(Now - TimeSpan.FromMinutes(10), "bran", Speaker.X + 200, Speaker.Y);
        journal.Record(evt);

        Assert.Same(evt, journal.PickGossip("sela", Speaker, Now));
        Assert.Null(journal.PickGossip("tam", Speaker, Now + TimeSpan.FromMinutes(1)));
        Assert.Same(evt, journal.PickGossip("tam", Speaker, Now + GossipRules.RetellAfter));
    }

    [Fact]
    public void PickGossip_ThiefKeepsItsOwnLiftQuiet()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - TimeSpan.FromMinutes(10), "slick", Speaker.X + 200, Speaker.Y, ShardEventType.Theft));

        Assert.Null(journal.PickGossip("Slick", Speaker, Now));
        Assert.NotNull(journal.PickGossip("sela", Speaker, Now));
    }

    [Fact]
    public void KillsBy_CountsOnlyThatKillersMurders()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - TimeSpan.FromMinutes(5), "a", 1000, 1000));
        journal.Record(Make(Now - TimeSpan.FromMinutes(6), "b", 1000, 1000));
        journal.Record(Make(Now - TimeSpan.FromMinutes(7), "c", 1000, 1000, ShardEventType.Death));
        journal.Record(Make(Now - GossipRules.MaxAge - TimeSpan.FromMinutes(1), "d", 1000, 1000));

        Assert.Equal(2, journal.KillsBy("Red", Now));
        Assert.Equal(0, journal.KillsBy("nobody", Now));
    }

    [Fact]
    public void PickGossip_PrefersHigherWeightAndIncrementsTellCount()
    {
        var journal = new EventJournal();
        var death = Make(Now - TimeSpan.FromMinutes(10), "sela", Speaker.X + 200, Speaker.Y, ShardEventType.Death);
        var pk = Make(Now - TimeSpan.FromMinutes(10), "tam", Speaker.X + 200, Speaker.Y, ShardEventType.Pk);
        journal.Record(death);
        journal.Record(pk);
        var picked = journal.PickGossip("bran", Speaker, Now);
        Assert.Same(pk, picked);
        Assert.Equal(1, pk.TellCount);
        Assert.Equal(0, death.TellCount);
    }

    [Fact]
    public void Record_PastCapacity_DropsOldest()
    {
        var journal = new EventJournal();
        for (var i = 0; i < GossipRules.JournalCapacity + 1; i++)
        {
            journal.Record(Make(Now.AddMinutes(-i), $"bot{i}", 1000, 1000));
        }

        var snap = journal.Snapshot();
        Assert.Equal(GossipRules.JournalCapacity, snap.Count);
        Assert.Equal("bot1", snap[0].Actor);
        Assert.Equal($"bot{GossipRules.JournalCapacity}", snap[^1].Actor);
    }

    [Fact]
    public void FindReport_DoesNotConsumeTheNews()
    {
        var journal = new EventJournal();
        var evt = Make(Now - TimeSpan.FromMinutes(10), "bran", Speaker.X + 200, Speaker.Y);
        journal.Record(evt);

        Assert.Same(evt, journal.FindReport("sela", Speaker, Now, ShardEventType.Pk));
        Assert.Equal(0, evt.TellCount);
    }

    [Fact]
    public void FindReport_FiltersTheEventType()
    {
        var journal = new EventJournal();
        journal.Record(Make(Now - TimeSpan.FromMinutes(10), "bran", Speaker.X + 200, Speaker.Y, ShardEventType.Death));

        Assert.Null(journal.FindReport("sela", Speaker, Now, ShardEventType.Pk));
    }

    [Fact]
    public void FindReport_PassesOverWhatTheCallerRefuses()
    {
        var journal = new EventJournal();
        var refused = Make(Now - TimeSpan.FromMinutes(10), "bran", Speaker.X + 200, Speaker.Y);
        var taken = Make(Now - TimeSpan.FromMinutes(10), "tom", Speaker.X - 200, Speaker.Y);
        journal.Record(refused);
        journal.Record(taken);

        Assert.Same(taken, journal.FindReport("sela", Speaker, Now, ShardEventType.Pk, accepts: evt => evt != refused));
        Assert.Null(journal.FindReport("sela", Speaker, Now, ShardEventType.Pk, accepts: _ => false));
    }

    [Fact]
    public void FindReport_DoesNotCrossFacets()
    {
        var journal = new EventJournal();
        journal.Record(new ShardEvent
        {
            At = Now - TimeSpan.FromMinutes(10),
            Type = ShardEventType.Pk,
            Actor = "bran",
            Other = "red",
            Place = "Yew",
            Facet = "Felucca",
            X = Speaker.X + 200,
            Y = Speaker.Y
        });

        Assert.Null(journal.FindReport("sela", Speaker, Now, ShardEventType.Pk, "Trammel"));
    }

    private static ShardEvent Make(DateTime at, string actor, int x, int y, string type = ShardEventType.Pk)
    {
        return new ShardEvent
        {
            At = at,
            Type = type,
            Actor = actor,
            Other = "red",
            Place = "Britain",
            X = x,
            Y = y
        };
    }

    [Fact]
    public void PickGossip_NobodyTellsTheSameStoryTwice()
    {
        var journal = new EventJournal();
        var evt = Make(Now - TimeSpan.FromMinutes(10), "bran", Speaker.X + 200, Speaker.Y);
        journal.Record(evt);

        Assert.Same(evt, journal.PickGossip("sela", Speaker, Now));
        Assert.True(evt.ToldBy("Sela"));
        Assert.Null(journal.PickGossip("sela", Speaker, Now + GossipRules.RetellAfter));
        Assert.Same(evt, journal.PickGossip("tam", Speaker, Now + GossipRules.RetellAfter));
    }
}
