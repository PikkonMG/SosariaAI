using System;
using SosariaAI.Behaviour;
using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class TownScuffleRulesTests
{
    private const int GapMin = 20;
    private const int GapMax = 45;
    private const int MaxSide = 3;
    private const int MaxFighters = 6;
    private const int SmallCap = 4;
    private const string Britain = "Britain";

    private static readonly DateTime Now = new(2026, 9, 28, 20, 0, 0);
    private static readonly TimeSpan Rest = TimeSpan.FromMinutes(60);

    [Fact]
    public void IsBlue_KeepsOutMurderersRedsAndGrays()
    {
        Assert.True(TownScuffleRules.IsBlue(isPk: false, red: false, criminal: false));
        Assert.False(TownScuffleRules.IsBlue(isPk: true, red: false, criminal: false));
        Assert.False(TownScuffleRules.IsBlue(isPk: false, red: true, criminal: false));
        Assert.False(TownScuffleRules.IsBlue(isPk: false, red: false, criminal: true));
    }

    [Fact]
    public void Ready_NeedsHealthAndTheFightersRest()
    {
        Assert.True(TownScuffleRules.Ready(TownScuffleRules.ReadyHits, default, Now, Rest));
        Assert.False(TownScuffleRules.Ready(TownScuffleRules.ReadyHits - 0.01, default, Now, Rest));
        Assert.False(TownScuffleRules.Ready(1.0, Now - Rest + TimeSpan.FromMinutes(1), Now, Rest));
        Assert.True(TownScuffleRules.Ready(1.0, Now - Rest, Now, Rest));
    }

    [Fact]
    public void OnGround_OnlyUnderTheSameTownsGuardsAndClearOfPeace()
    {
        Assert.True(TownScuffleRules.OnGround(underGuards: true, sameTown: true, nearPeace: false));
        Assert.False(TownScuffleRules.OnGround(underGuards: false, sameTown: true, nearPeace: false));
        Assert.False(TownScuffleRules.OnGround(underGuards: true, sameTown: false, nearPeace: false));
        Assert.False(TownScuffleRules.OnGround(underGuards: true, sameTown: true, nearPeace: true));
    }

    [Fact]
    public void ShardOpen_HoldsTheSwitchTheCapAndTheShardGap()
    {
        Assert.True(TownScuffleRules.ShardOpen(true, 2, 3, Now, Now));
        Assert.False(TownScuffleRules.ShardOpen(false, 0, 3, default, Now));
        Assert.False(TownScuffleRules.ShardOpen(true, 3, 3, default, Now));
        Assert.False(TownScuffleRules.ShardOpen(true, 0, -1, default, Now));
        Assert.False(TownScuffleRules.ShardOpen(true, 0, 3, Now + TimeSpan.FromSeconds(1), Now));
    }

    [Fact]
    public void TownOpen_OneScuffleATownAndOnlyAfterItsGap()
    {
        Assert.True(TownScuffleRules.TownOpen(townBusy: false, Now, Now));
        Assert.False(TownScuffleRules.TownOpen(townBusy: true, default, Now));
        Assert.False(TownScuffleRules.TownOpen(townBusy: false, Now + TimeSpan.FromMinutes(1), Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(25)]
    [InlineData(int.MaxValue)]
    [InlineData(-5)]
    public void TownGap_StaysBetweenTheLeastAndTheMostGap(int roll)
    {
        var gap = TownScuffleRules.TownGap(roll, GapMin, GapMax);

        Assert.InRange(gap, TimeSpan.FromMinutes(GapMin), TimeSpan.FromMinutes(GapMax));
    }

    [Fact]
    public void TownGap_ReachesBothEndsAndMendsAnUpsideDownRange()
    {
        Assert.Equal(TimeSpan.FromMinutes(GapMin), TownScuffleRules.TownGap(0, GapMin, GapMax));
        Assert.Equal(TimeSpan.FromMinutes(GapMax), TownScuffleRules.TownGap(GapMax - GapMin, GapMin, GapMax));
        Assert.Equal(TimeSpan.FromMinutes(GapMin), TownScuffleRules.TownGap(99, GapMin, GapMin - 5));
    }

    [Fact]
    public void FirstGap_ComesInsideTheLeastGap()
    {
        Assert.Equal(TimeSpan.Zero, TownScuffleRules.FirstGap(0, GapMin));
        Assert.InRange(TownScuffleRules.FirstGap(12345, GapMin), TimeSpan.Zero, TimeSpan.FromMinutes(GapMin));
    }

    [Fact]
    public void SideSizes_OneToThreeASideAndNoMoreThanTheTotal()
    {
        for (var order = 0; order < MaxSide; order++)
        {
            for (var chaos = 0; chaos < MaxSide; chaos++)
            {
                var (orderSize, chaosSize) = TownScuffleRules.SideSizes(order, chaos, TownScuffleRules.MinSide, MaxSide, MaxFighters);

                Assert.InRange(orderSize, TownScuffleRules.MinSide, MaxSide);
                Assert.InRange(chaosSize, TownScuffleRules.MinSide, MaxSide);
                Assert.True(orderSize + chaosSize <= MaxFighters);
            }
        }
    }

    [Fact]
    public void SideSizes_TheBiggerSideGivesWayToTheTotalCap()
    {
        Assert.Equal((2, 2), TownScuffleRules.SideSizes(MaxSide - 1, MaxSide - 1, TownScuffleRules.MinSide, MaxSide, SmallCap));
        Assert.Equal((1, 1), TownScuffleRules.SideSizes(5, 9, TownScuffleRules.MinSide, 0, 0));
    }

    [Fact]
    public void SideSizes_ACallSeeksTwoOrThreeASide()
    {
        for (var order = 0; order < MaxSide; order++)
        {
            for (var chaos = 0; chaos < MaxSide; chaos++)
            {
                var (orderSize, chaosSize) = TownScuffleRules.SideSizes(order, chaos, TownScuffleRules.CalledSide, MaxSide, MaxFighters);

                Assert.InRange(orderSize, TownScuffleRules.CalledSide, MaxSide);
                Assert.InRange(chaosSize, TownScuffleRules.CalledSide, MaxSide);
            }
        }

        Assert.Equal((1, 1), TownScuffleRules.SideSizes(4, 7, TownScuffleRules.CalledSide, TownScuffleRules.MinSide, MaxFighters));
    }

    [Fact]
    public void CallState_StartsWhenAllCameOrAtTheEndWithOneASide()
    {
        Assert.Equal(ScuffleCallState.Starts, TownScuffleRules.CallState(2, 3, 2, 3, timeUp: false));
        Assert.Equal(ScuffleCallState.Gathering, TownScuffleRules.CallState(2, 3, 2, 1, timeUp: false));
        Assert.Equal(ScuffleCallState.Starts, TownScuffleRules.CallState(2, 3, 2, 1, timeUp: true));
        Assert.Equal(ScuffleCallState.Gathering, TownScuffleRules.CallState(2, 2, 0, 0, timeUp: false));
    }

    [Fact]
    public void CallState_LapsesWhenASideIsGoneOrNobodyOfItCame()
    {
        Assert.Equal(ScuffleCallState.Lapsed, TownScuffleRules.CallState(0, 2, 0, 2, timeUp: false));
        Assert.Equal(ScuffleCallState.Lapsed, TownScuffleRules.CallState(2, 0, 2, 0, timeUp: false));
        Assert.Equal(ScuffleCallState.Lapsed, TownScuffleRules.CallState(2, 2, 2, 0, timeUp: true));
    }

    [Fact]
    public void Balance_NeverMoreThanOneLopsided()
    {
        Assert.Equal((2, 1), TownScuffleRules.Balance(3, 1));
        Assert.Equal((1, 2), TownScuffleRules.Balance(1, 3));
        Assert.Equal((3, 3), TownScuffleRules.Balance(3, 3));
        Assert.Equal((1, 1), TownScuffleRules.Balance(1, 1));
    }

    [Fact]
    public void FoeIndex_GoesRoundTheOtherSide()
    {
        Assert.Equal(0, TownScuffleRules.FoeIndex(0, 1));
        Assert.Equal(0, TownScuffleRules.FoeIndex(1, 1));
        Assert.Equal(1, TownScuffleRules.FoeIndex(1, 2));
        Assert.Equal(0, TownScuffleRules.FoeIndex(2, 2));
        Assert.Equal(-1, TownScuffleRules.FoeIndex(0, 0));
    }

    [Fact]
    public void TimeLimit_StaysInsideTheSkirmishLimit()
    {
        const int twoAndAHalfMinutes = 150;

        Assert.Equal(TimeSpan.FromSeconds(twoAndAHalfMinutes), TownScuffleRules.TimeLimit(twoAndAHalfMinutes));
        Assert.Equal(TownScuffleRules.MinTimeLimit, TownScuffleRules.TimeLimit(0));
        Assert.Equal(FactionRules.SkirmishLimit, TownScuffleRules.TimeLimit(int.MaxValue));
    }

    [Fact]
    public void Result_ASideWithNobodyLeftLoses()
    {
        Assert.Equal(ScuffleResult.ChaosWon, TownScuffleRules.Result(0, 1, false, 0, 1));
        Assert.Equal(ScuffleResult.OrderWon, TownScuffleRules.Result(2, 0, false, 1, 0));
        Assert.Equal(ScuffleResult.Draw, TownScuffleRules.Result(0, 0, false, 0, 0));
        Assert.Null(TownScuffleRules.Result(1, 1, false, 0.2, 0.9));
    }

    [Fact]
    public void Result_AtTheLimitTheMoreHurtSideGivesWay()
    {
        Assert.Equal(ScuffleResult.OrderWon, TownScuffleRules.Result(1, 1, true, 0.8, 0.4));
        Assert.Equal(ScuffleResult.ChaosWon, TownScuffleRules.Result(1, 2, true, 0.3, 0.7));
        Assert.Equal(ScuffleResult.Draw, TownScuffleRules.Result(1, 1, true, 0.6, 0.6 + TownScuffleRules.EvenHitsMargin / 2));
    }

    [Fact]
    public void Lines_NameTheTownTheSidesAndTheResult()
    {
        const int x = 1450;
        const int y = 1640;
        const int ranSeconds = 95;
        const int fought = 3;

        Assert.Equal(
            "Town scuffle in Britain: Order (Rowan, Kerr) against Chaos (Vex) at 1450,1640",
            TownScuffleRules.StartLine(Britain, ["Rowan", "Kerr"], ["Vex"], x, y)
        );
        Assert.Equal(
            "Town scuffle in Britain ended: Chaos won after 95s; 3 fought",
            TownScuffleRules.EndLine(Britain, ScuffleResult.ChaosWon, TimeSpan.FromSeconds(ranSeconds), fought)
        );
        Assert.Equal("even", TownScuffleRules.ResultWords(ScuffleResult.Draw));
        Assert.Equal(
            "Town scuffle call in Britain: Order (Rowan, Kerr) and Chaos (Vex) gather at 1450,1640",
            TownScuffleRules.CallLine(Britain, ["Rowan", "Kerr"], ["Vex"], x, y)
        );
        Assert.Equal("Town scuffle call in Britain lapsed: 2 of Order and 0 of Chaos came", TownScuffleRules.LapseLine(Britain, 2, 0));
    }

    [Fact]
    public void SummaryLine_CountsTheTenMinutesAndStaysQuietWhenNothingHappened()
    {
        Assert.Null(TownScuffleRules.SummaryLine(0, 0, 0, 0, 0, 0, 0, CensusText.CensusMinutes));
        Assert.Equal(
            "Town scuffles in the last 10 minutes: 3 called, 1 lapsed; 2 begun with 5 fighters; 3 ended: Order won 1, Chaos won 1, even 1",
            TownScuffleRules.SummaryLine(3, 1, 2, 5, 1, 1, 1, CensusText.CensusMinutes)
        );
        Assert.NotNull(TownScuffleRules.SummaryLine(1, 0, 0, 0, 0, 0, 0, CensusText.CensusMinutes));
    }
}
