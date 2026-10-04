using System;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Logging;
using Xunit;

namespace SosariaAI.Tests;

public class JevUsageTests
{
    private const double Tenth = 0.1;
    private const double Tolerance = 1e-9;

    private static readonly DateTime TenPast = new(2026, 9, 24, 12, 10, 0, DateTimeKind.Utc);

    [Fact]
    public void Record_CountsRequestsAndTokensInTheHourByKind()
    {
        var usage = new JevUsage(1_000);
        usage.RecordRequest(TenPast, JevKind.BigMoment);
        usage.RecordRequest(TenPast, JevKind.MonsterFight);
        usage.RecordTokens(TenPast, JevKind.BigMoment, 300);

        var hour = usage.Current(TenPast.AddMinutes(5));

        Assert.Equal(2, hour.Requests);
        Assert.Equal(300, hour.InputTokens);
        Assert.Equal(1, hour.KindRequests[(int)JevKind.BigMoment]);
        Assert.Equal(1, hour.KindRequests[(int)JevKind.MonsterFight]);
        Assert.Equal(300, hour.KindTokens[(int)JevKind.BigMoment]);
        Assert.Equal(0, hour.KindTokens[(int)JevKind.MonsterFight]);
    }

    [Fact]
    public void UsedShare_HoldsUntilTheClockHourTurns()
    {
        var usage = new JevUsage(500);
        usage.RecordTokens(TenPast, JevKind.Decision, 300);

        Assert.Equal(0.6, usage.UsedShare(TenPast), Tolerance);

        usage.RecordTokens(TenPast.AddMinutes(1), JevKind.Trade, 200);

        Assert.Equal(1.0, usage.UsedShare(TenPast.AddMinutes(49)), Tolerance);
        Assert.Equal(0, usage.UsedShare(TenPast.AddMinutes(50)), Tolerance);
    }

    [Fact]
    public void TakeFinished_GivesTheLastHourOnceAfterItTurns()
    {
        var usage = new JevUsage(1_000_000);
        usage.RecordRequest(TenPast, JevKind.SpeechGate);
        usage.RecordTokens(TenPast, JevKind.SpeechGate, 400);

        Assert.Null(usage.TakeFinished(TenPast.AddMinutes(30)));

        var finished = usage.TakeFinished(TenPast.AddMinutes(51));

        Assert.NotNull(finished);
        Assert.Equal(1, finished.Value.Requests);
        Assert.Equal(400, finished.Value.InputTokens);
        Assert.Equal(1, finished.Value.KindRequests[(int)JevKind.SpeechGate]);
        Assert.Null(usage.TakeFinished(TenPast.AddMinutes(52)));
        Assert.Equal(0, usage.Current(TenPast.AddMinutes(52)).Requests);
        Assert.Equal(0, usage.Current(TenPast.AddMinutes(52)).KindRequests[(int)JevKind.SpeechGate]);
    }

    [Fact]
    public void LogLine_IsTheOperatorsCostLineSplitByKind()
    {
        var kinds = new int[JevBudgetRules.KindCount];
        var tokens = new long[JevBudgetRules.KindCount];
        kinds[(int)JevKind.BigMoment] = 5_000;
        tokens[(int)JevKind.BigMoment] = 2_000_000;
        kinds[(int)JevKind.PersonFight] = 10_000;
        tokens[(int)JevKind.PersonFight] = 4_000_000;
        var hour = new JevUsageHour(15_000, 6_000_000, kinds, tokens);

        Assert.Equal(0.252, hour.Dollars, 3);
        Assert.Equal(
            "jev usage: 15000 requests, 6000000 input tokens, ~$0.25 this hour " +
            "(big moment 5000 req/2000000 tok, stance vs person 10000 req/4000000 tok)",
            MessageTemplate.Render(JevUsageHour.LogTemplate, hour.LogArgs)
        );
    }

    [Fact]
    public void LogLine_AnEmptyHourSaysNone()
    {
        var hour = new JevUsageHour(0, 0, new int[JevBudgetRules.KindCount], new long[JevBudgetRules.KindCount]);

        Assert.Equal(JevUsageHour.NoSplit, hour.Split);
    }

    [Fact]
    public void DefaultCap_IsAboutATenthOfADollarAnHour()
    {
        var hour = new JevUsageHour(0, BrainConfiguration.DefaultJevInputTokensPerHour, null, null);

        Assert.InRange(hour.Dollars, Tenth - 0.01, Tenth);
    }
}
