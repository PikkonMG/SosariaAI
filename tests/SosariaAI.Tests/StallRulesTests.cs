using System;
using Server;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class StallRulesTests
{
    private const string Felucca = "Felucca";
    private const string Trammel = "Trammel";
    private static readonly DateTime Start = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Pass = TimeSpan.FromMinutes(1);
    private static readonly Point3D Spot = new(1425, 1690, 0);

    private static StallSample Still(bool held = false, bool advanced = false, string map = Felucca) =>
        new(map, Spot, advanced, held);

    /// <summary>Looks once a minute until <paramref name="span"/> has passed and returns the last verdict.</summary>
    private static StallVerdict LookFor(StallTrack track, TimeSpan span, Func<StallSample> sample, ref DateTime now)
    {
        var verdict = StallVerdict.Waiting;
        var end = now + span;

        while (now < end)
        {
            now += Pass;
            verdict = StallRules.Observe(track, sample(), now);
        }

        return verdict;
    }

    [Fact]
    public void StallAfter_WaitsLongerThanTheSkillGiveUps() =>
        Assert.True(StallRules.StallAfter > GoToSkill.GiveUp + HomeLeash.MaroonedCheckCooldown);

    [Fact]
    public void Observe_FirstLookStartsTheClock()
    {
        var track = new StallTrack();

        Assert.Equal(StallVerdict.Progress, StallRules.Observe(track, Still(), Start));
        Assert.True(track.Started);
        Assert.False(StallRules.IsStalled(track, Start));
    }

    [Fact]
    public void Observe_StillForTheFullSpanRescuesOnce()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);

        var beforeSpan = LookFor(track, StallRules.StallAfter - Pass, () => Still(), ref now);
        Assert.Equal(StallVerdict.Waiting, beforeSpan);

        now += Pass;
        Assert.Equal(StallVerdict.Rescue, StallRules.Observe(track, Still(), now));
        Assert.True(StallRules.IsStalled(track, now));
        Assert.Equal(1, track.Rescues);

        now += Pass;
        Assert.Equal(StallVerdict.Waiting, StallRules.Observe(track, Still(), now));
    }

    [Fact]
    public void Observe_StillAfterARescueTriesAgainWithoutALog()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);
        LookFor(track, StallRules.StallAfter, () => Still(), ref now);

        var again = LookFor(track, StallRules.RescueRetryAfter, () => Still(), ref now);

        Assert.Equal(StallVerdict.RescueAgain, again);
        Assert.Equal(2, track.Rescues);
    }

    [Fact]
    public void Observe_SmallDriftIsStillStandingStill()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);
        var jitter = new StallSample(Felucca, new Point3D(Spot.X + StallRules.StillRadius, Spot.Y, Spot.Z), false, false);

        now += StallRules.StallAfter;

        Assert.Equal(StallVerdict.Rescue, StallRules.Observe(track, jitter, now));
    }

    [Fact]
    public void Observe_MoveOffTheSpotStartsOver()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);
        now += StallRules.StallAfter - Pass;
        var moved = new StallSample(Felucca, new Point3D(Spot.X + StallRules.StillRadius + 1, Spot.Y, Spot.Z), false, false);

        Assert.Equal(StallVerdict.Progress, StallRules.Observe(track, moved, now));
        Assert.False(StallRules.IsStalled(track, now + Pass));
    }

    [Fact]
    public void Observe_FacetChangeIsProgress()
    {
        var track = new StallTrack();
        StallRules.Observe(track, Still(), Start);

        Assert.Equal(
            StallVerdict.Progress,
            StallRules.Observe(track, Still(map: Trammel), Start + StallRules.StallAfter)
        );
    }

    [Fact]
    public void Observe_FinishedWorkClearsAStall()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);
        LookFor(track, StallRules.StallAfter, () => Still(), ref now);
        Assert.Equal(1, track.Rescues);

        now += Pass;

        Assert.Equal(StallVerdict.Progress, StallRules.Observe(track, Still(advanced: true), now));
        Assert.Equal(0, track.Rescues);
        Assert.False(StallRules.IsStalled(track, now));
    }

    [Fact]
    public void Observe_HoldPausesTheClock()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);

        var held = LookFor(track, StallRules.StallAfter * 3, () => Still(held: true), ref now);

        Assert.Equal(StallVerdict.Held, held);
        Assert.False(StallRules.IsStalled(track, now));
        Assert.Equal(0, StallRules.StillMinutes(track, now));
    }

    [Fact]
    public void Observe_HoldDoesNotWipeStillTimeBeforeIt()
    {
        var track = new StallTrack();
        var now = Start;
        StallRules.Observe(track, Still(), now);
        var half = TimeSpan.FromTicks(StallRules.StallAfter.Ticks / 2);

        LookFor(track, half, () => Still(), ref now);
        LookFor(track, StallRules.StallAfter, () => Still(held: true), ref now);
        var verdict = LookFor(track, StallRules.StallAfter - half, () => Still(), ref now);

        Assert.Equal(StallVerdict.Rescue, verdict);
    }

    [Theory]
    [InlineData(SkillKinds.Rest)]
    [InlineData(SkillKinds.BankCrowd)]
    [InlineData(SkillKinds.Tavern)]
    [InlineData(SkillKinds.Camp)]
    [InlineData(SkillKinds.Meditate)]
    [InlineData(SkillKinds.Follow)]
    public void IsHoldSkill_StepsThatWaitInPlace(string kind) => Assert.True(StallRules.IsHoldSkill(kind));

    [Theory]
    [InlineData(SkillKinds.GoTo)]
    [InlineData(SkillKinds.IdleWander)]
    [InlineData(SkillKinds.Decide)]
    [InlineData(SkillKinds.Mine)]
    [InlineData(null)]
    public void IsHoldSkill_WalksAndWorkAreNotHolds(string kind) => Assert.False(StallRules.IsHoldSkill(kind));

    [Fact]
    public void IsHeld_TalkFightHuntAndCraftStationHold()
    {
        Assert.True(StallRules.IsHeld(SkillKinds.GoTo, craftStation: false, talking: true, fighting: false, hunting: false));
        Assert.True(StallRules.IsHeld(SkillKinds.GoTo, craftStation: false, talking: false, fighting: true, hunting: false));
        Assert.True(StallRules.IsHeld(SkillKinds.Hunt, craftStation: false, talking: false, fighting: false, hunting: true));
        Assert.True(StallRules.IsHeld(SkillKinds.Smith, craftStation: true, talking: false, fighting: false, hunting: false));
        Assert.False(StallRules.IsHeld(SkillKinds.GoTo, craftStation: false, talking: false, fighting: false, hunting: false));
    }
}
