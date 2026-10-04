using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class BotConversationTrackerTests
{
    [Fact]
    public void Pair_AllowsMaxExchangesThenBlocksUntilPairRest()
    {
        var tracker = new BotConversationTracker();
        var a = (Serial)1u;
        var b = (Serial)2u;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var pairRest = TimeSpan.FromMinutes(10);
        const int maxExchanges = 2;

        Assert.True(tracker.CanGreet(a, b, start, TimeSpan.Zero, pairRest, maxExchanges));
        tracker.Record(a, b, start, pairRest);

        Assert.True(tracker.CanGreet(b, a, start, TimeSpan.Zero, pairRest, maxExchanges));
        tracker.Record(b, a, start, pairRest);

        Assert.False(tracker.CanGreet(a, b, start.AddMinutes(5), TimeSpan.Zero, pairRest, maxExchanges));
        Assert.True(tracker.CanGreet(a, b, start.AddMinutes(10), TimeSpan.Zero, pairRest, maxExchanges));
    }

    [Fact]
    public void Pair_ResetsCountAfterPairRest()
    {
        var tracker = new BotConversationTracker();
        var a = (Serial)3u;
        var b = (Serial)4u;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var pairRest = TimeSpan.FromMinutes(10);

        tracker.Record(a, b, start, pairRest);
        tracker.Record(a, b, start, pairRest);
        tracker.Record(a, b, start.AddMinutes(10), pairRest);

        Assert.True(tracker.CanGreet(a, b, start.AddMinutes(10), TimeSpan.Zero, pairRest, 2));
    }

    [Fact]
    public void Greeting_BlockedUntilPairCooldownThenAllowed()
    {
        var tracker = new BotConversationTracker();
        var a = (Serial)5u;
        var b = (Serial)6u;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cooldown = TimeSpan.FromMinutes(5);

        Assert.True(tracker.CanGreet(a, b, start, cooldown));
        tracker.RecordGreeting(a, b, start);
        Assert.False(tracker.CanGreet(a, b, start.AddMinutes(1), cooldown));
        Assert.True(tracker.CanGreet(b, a, start.AddMinutes(5), cooldown));
    }

    [Fact]
    public void Greeting_DoesNotResetTheExchangeCount()
    {
        var tracker = new BotConversationTracker();
        var a = (Serial)7u;
        var b = (Serial)8u;
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cooldown = TimeSpan.FromMinutes(2);
        var pairRest = TimeSpan.FromMinutes(10);
        const int maxExchanges = 2;

        tracker.RecordGreeting(a, b, start);
        tracker.Record(a, b, start, pairRest);
        Assert.False(tracker.CanGreet(a, b, start.AddSeconds(1), TimeSpan.Zero, pairRest, maxExchanges));
        Assert.False(
            tracker.CanGreet(a, b, start.AddMinutes(2), cooldown, pairRest, maxExchanges)
        );
        Assert.True(
            tracker.CanGreet(a, b, start.AddMinutes(10), cooldown, pairRest, maxExchanges)
        );
    }

    [Fact]
    public void Speaker_BlocksUntilQuietGap()
    {
        var tracker = new BotConversationTracker();
        var a = (Serial)9u;
        var start = new DateTime(2026, 9, 13, 17, 54, 0, DateTimeKind.Utc);
        var quiet = TimeSpan.FromMinutes(2);

        Assert.True(tracker.SpeakerMayGreet(a, start, quiet));
        tracker.RecordSpeakerGreet(a, start);
        Assert.False(tracker.SpeakerMayGreet(a, start.AddSeconds(30), quiet));
        Assert.True(tracker.SpeakerMayGreet(a, start.AddMinutes(2), quiet));
    }

    [Fact]
    public void BankArea_BlocksUntilGap()
    {
        var tracker = new BotConversationTracker();
        var start = new DateTime(2026, 9, 13, 17, 54, 0, DateTimeKind.Utc);
        var gap = TimeSpan.FromSeconds(20);

        Assert.True(tracker.AreaMayGreet(MeetingRules.BankArea, start, gap));
        tracker.RecordAreaGreet(MeetingRules.BankArea, start);
        Assert.False(tracker.AreaMayGreet(MeetingRules.BankArea, start.AddSeconds(5), gap));
        Assert.True(tracker.AreaMayGreet(MeetingRules.BankArea, start.AddSeconds(20), gap));
    }
}
