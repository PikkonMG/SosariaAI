using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class ChoiceSeedTests
{
    private const uint Serial = 0x1234;
    private const uint PeopleSampled = 200;
    private const int SaltA = 1;
    private const int SaltB = 2;
    private const int MinuteSweep = 24 * 60;
    private const int MinDistinctPhaseEnds = 20;
    private static readonly DateTime Noon = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void For_HoldsThroughTheWholePhase()
    {
        var start = PhaseStart(Serial, Noon);
        var length = ChoiceSeed.PhaseMinutes(Serial);

        Assert.Equal(ChoiceSeed.For(Serial, start), ChoiceSeed.For(Serial, start.AddMinutes(length - 1)));
    }

    [Fact]
    public void For_ChangesWithTheNextPhase()
    {
        var start = PhaseStart(Serial, Noon);
        var length = ChoiceSeed.PhaseMinutes(Serial);

        Assert.NotEqual(ChoiceSeed.For(Serial, start), ChoiceSeed.For(Serial, start.AddMinutes(length)));
    }

    [Fact]
    public void For_DiffersBetweenPeople()
    {
        Assert.NotEqual(ChoiceSeed.For(Serial, Noon), ChoiceSeed.For(Serial + 1, Noon));
    }

    [Fact]
    public void PhaseMinutes_StaysInTheLongJobRange()
    {
        for (uint serial = 0; serial < PeopleSampled; serial++)
        {
            Assert.InRange(ChoiceSeed.PhaseMinutes(serial), ChoiceSeed.MinPhaseMinutes, ChoiceSeed.MaxPhaseMinutes);
        }
    }

    [Fact]
    public void PhaseIndex_CrowdDoesNotChangeJobsOnTheSameMinute()
    {
        // Everyone shared the hour seed, so a whole crowd changed its mind on the hour.
        var endMinutes = new HashSet<int>();

        for (uint serial = 0; serial < PeopleSampled; serial++)
        {
            endMinutes.Add((int)(PhaseStart(serial, Noon) - Noon.Date).TotalMinutes % MinuteSweep);
        }

        Assert.True(endMinutes.Count >= MinDistinctPhaseEnds, $"only {endMinutes.Count} distinct phase starts");
    }

    [Fact]
    public void Unit_IsInRangeAndSaltMatters()
    {
        for (var seed = -50; seed < 50; seed++)
        {
            Assert.InRange(ChoiceSeed.Unit(seed, SaltA), 0.0, 1.0);
        }

        Assert.NotEqual(ChoiceSeed.Unit(Serial.GetHashCode(), SaltA), ChoiceSeed.Unit(Serial.GetHashCode(), SaltB));
    }

    [Fact]
    public void PhaseMinutes_TraitScale_StretchesAndShortensWithinBounds()
    {
        const double Restless = 0.4;
        const double Homebody = 2.1;
        var neutral = ChoiceSeed.PhaseMinutes(Serial);

        Assert.True(ChoiceSeed.PhaseMinutes(Serial, Restless) < neutral);
        Assert.True(ChoiceSeed.PhaseMinutes(Serial, Homebody) > neutral);
        Assert.InRange(ChoiceSeed.PhaseMinutes(Serial, Restless), ChoiceSeed.MinScaledPhaseMinutes, ChoiceSeed.MaxScaledPhaseMinutes);
        Assert.InRange(ChoiceSeed.PhaseMinutes(Serial, Homebody), ChoiceSeed.MinScaledPhaseMinutes, ChoiceSeed.MaxScaledPhaseMinutes);
    }

    private static DateTime PhaseStart(uint serial, DateTime at)
    {
        var index = ChoiceSeed.PhaseIndex(serial, at);
        var start = at;

        while (ChoiceSeed.PhaseIndex(serial, start.AddMinutes(-1)) == index)
        {
            start = start.AddMinutes(-1);
        }

        return start;
    }
}
