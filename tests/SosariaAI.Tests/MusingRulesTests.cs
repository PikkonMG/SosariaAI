using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class MusingRulesTests
{
    private const string FirstEvent = "I noticed Mira nearby.";
    private static readonly DateTime Noon = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ShouldAttempt_SameStateTwice_AllowsOnlyTheFirst()
    {
        var interval = MusingRules.IntervalFromMinutes(MusingRules.DefaultIntervalMinutes);

        Assert.True(
            MusingRules.ShouldAttempt(FirstEvent, lastEvent: null, lastAttempt: default, Noon, interval)
        );
        Assert.False(
            MusingRules.ShouldAttempt(FirstEvent, FirstEvent, Noon, Noon, interval)
        );
        Assert.False(
            MusingRules.ShouldAttempt(FirstEvent, FirstEvent, Noon, Noon.AddMinutes(1), interval)
        );
        Assert.True(
            MusingRules.ShouldAttempt(
                "I killed a skeleton.",
                FirstEvent,
                Noon,
                Noon.Add(interval),
                interval
            )
        );
    }

    [Fact]
    public void HasNewEvent_BlankOrSame_IsFalse()
    {
        Assert.False(MusingRules.HasNewEvent(null, FirstEvent));
        Assert.False(MusingRules.HasNewEvent("  ", FirstEvent));
        Assert.False(MusingRules.HasNewEvent(FirstEvent, FirstEvent));
        Assert.True(MusingRules.HasNewEvent("I arrived at Minoc.", FirstEvent));
    }

    [Fact]
    public void PassesSpeakChance_OnlyTheFirstQuarter()
    {
        Assert.True(MusingRules.PassesSpeakChance(0));
        Assert.True(MusingRules.PassesSpeakChance(MusingRules.SpeakChancePercent - 1));
        Assert.False(MusingRules.PassesSpeakChance(MusingRules.SpeakChancePercent));
        Assert.False(MusingRules.PassesSpeakChance(99));
    }

    [Fact]
    public void MayUseWrittenLine_OncePerHour()
    {
        Assert.True(MusingRules.MayUseWrittenLine(default, Noon));
        Assert.False(MusingRules.MayUseWrittenLine(Noon, Noon.AddMinutes(59)));
        Assert.True(MusingRules.MayUseWrittenLine(Noon, Noon.Add(MusingRules.WrittenLineRest)));
    }

    [Fact]
    public void IntervalFromMinutes_UsesDefaultWhenMissing()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(CharactersConfiguration.DefaultMusingIntervalMinutes),
            MusingRules.IntervalFromMinutes(0)
        );
        Assert.Equal(TimeSpan.FromMinutes(12), MusingRules.IntervalFromMinutes(12));
    }
}
