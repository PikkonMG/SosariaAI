using System;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>A hunter leaves when overloaded, out of supplies, hurt again and again, the ground is empty, or the time is up.</summary>
public class HuntEndDecisionTests
{
    private const double Healthy = 1.0;
    private const double Wounded = 0.2;
    private const double StopBelow = 0.4;
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Ends = Start.AddMinutes(15);

    private static HuntEndReason Reason(
        DateTime now,
        bool packFull = false,
        double hits = Healthy,
        int lowHits = 0,
        DateTime lastPreyAt = default,
        bool suppliesLow = false
    ) =>
        HuntEndDecision.Reason(now, Ends, packFull, hits, StopBelow, lowHits, lastPreyAt, HuntEndDecision.EmptyHuntLimit, suppliesLow);

    [Fact]
    public void Reason_StillHealthyWithPrey_GoesOn() =>
        Assert.Equal(HuntEndReason.None, Reason(Start.AddMinutes(1), lastPreyAt: Start));

    [Fact]
    public void Reason_TimeUp() =>
        Assert.Equal(HuntEndReason.TimeUp, Reason(Ends, lastPreyAt: Ends));

    [Fact]
    public void Reason_PackFull() =>
        Assert.Equal(HuntEndReason.PackFull, Reason(Start, packFull: true));

    [Fact]
    public void Reason_SuppliesLow() =>
        Assert.Equal(HuntEndReason.SuppliesLow, Reason(Start, suppliesLow: true));

    [Fact]
    public void RanLowOnRun_OnlyWhenTheSuppliesWentLowDuringTheRun()
    {
        Assert.True(HuntEndDecision.RanLowOnRun(lowAtStart: false, lowNow: true));
        Assert.False(HuntEndDecision.RanLowOnRun(lowAtStart: true, lowNow: true));
        Assert.False(HuntEndDecision.RanLowOnRun(lowAtStart: false, lowNow: false));
        Assert.False(HuntEndDecision.RanLowOnRun(lowAtStart: true, lowNow: false));
    }

    [Fact]
    public void Reason_AHuntBegunLow_FightsOnWithWhatItHas() =>
        Assert.Equal(
            HuntEndReason.None,
            Reason(Start.AddMinutes(1), lastPreyAt: Start, suppliesLow: HuntEndDecision.RanLowOnRun(lowAtStart: true, lowNow: true))
        );

    [Fact]
    public void Reason_AHuntBegunLow_StillEndsOnItsWounds() =>
        Assert.Equal(
            HuntEndReason.Hurt,
            Reason(
                Start,
                hits: Wounded,
                lowHits: SosariaCombat.HuntLowHitsLimit,
                suppliesLow: HuntEndDecision.RanLowOnRun(lowAtStart: true, lowNow: true)
            )
        );

    [Fact]
    public void Reason_HurtTooOften()
    {
        Assert.Equal(HuntEndReason.Hurt, Reason(Start, hits: Wounded, lowHits: SosariaCombat.HuntLowHitsLimit));
        Assert.Equal(HuntEndReason.None, Reason(Start, hits: Wounded, lowHits: SosariaCombat.HuntLowHitsLimit - 1));
    }

    [Fact]
    public void Reason_NoPreySeenForTheLimit_IsEmpty()
    {
        Assert.Equal(HuntEndReason.Empty, Reason(Start + HuntEndDecision.EmptyHuntLimit, lastPreyAt: Start));
        Assert.Equal(HuntEndReason.None, Reason(Start + HuntEndDecision.EmptyHuntLimit, lastPreyAt: Start.AddSeconds(1)));
    }

    [Fact]
    public void Reason_AShortLook_EndsSooner()
    {
        var look = TimeSpan.FromSeconds(30);

        Assert.Equal(
            HuntEndReason.Empty,
            HuntEndDecision.Reason(Start + look, Ends, false, Healthy, StopBelow, 0, Start, look, false)
        );
        Assert.Equal(
            HuntEndReason.None,
            HuntEndDecision.Reason(Start + look, Ends, false, Healthy, StopBelow, 0, Start.AddSeconds(1), look, false)
        );
    }

    [Fact]
    public void ShouldStay_TimeUpWithARecentKill()
    {
        var now = Ends;

        Assert.True(HuntEndDecision.ShouldStay(HuntEndReason.TimeUp, now, now - HuntEndDecision.PayingWindow, stays: 0));
        Assert.False(HuntEndDecision.ShouldStay(HuntEndReason.TimeUp, now, now - HuntEndDecision.PayingWindow - TimeSpan.FromSeconds(1), 0));
        Assert.False(HuntEndDecision.ShouldStay(HuntEndReason.TimeUp, now, default, 0));
        Assert.False(HuntEndDecision.ShouldStay(HuntEndReason.TimeUp, now, now, HuntEndDecision.MaxStays));
        Assert.False(HuntEndDecision.ShouldStay(HuntEndReason.PackFull, now, now, 0));
    }

    [Fact]
    public void CountLowHits_CountsEdgesOnly()
    {
        var count = HuntEndDecision.CountLowHits(0, false, true);
        Assert.Equal(1, count);
        count = HuntEndDecision.CountLowHits(count, true, true);
        Assert.Equal(1, count);
        count = HuntEndDecision.CountLowHits(count, true, false);
        Assert.Equal(1, count);
        count = HuntEndDecision.CountLowHits(count, false, true);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Routed_RunOffTheGroundTooOften_EndsAsFightsGoneBadly()
    {
        Assert.Equal(HuntEndReason.None, HuntEndDecision.Routed(HuntEndReason.None, HuntEndDecision.RunsOffLimit - 1, HuntEndDecision.RunsOffLimit));
        Assert.Equal(HuntEndReason.Hurt, HuntEndDecision.Routed(HuntEndReason.None, HuntEndDecision.RunsOffLimit, HuntEndDecision.RunsOffLimit));
        Assert.Equal(HuntEndReason.PackFull, HuntEndDecision.Routed(HuntEndReason.PackFull, HuntEndDecision.RunsOffLimit, HuntEndDecision.RunsOffLimit));
    }
}
