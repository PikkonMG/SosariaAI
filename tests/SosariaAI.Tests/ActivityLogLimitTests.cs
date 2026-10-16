using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class ActivityLogLimitTests
{
    private static readonly DateTime Start = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Next_WritesTheFirstFailure_AndOpensTheWindow()
    {
        Assert.Equal((true, Start), ActivityLogLimit.Next(StepLine.Failed, default, Start));
    }

    [Fact]
    public void Next_KeepsStartsAndFailuresQuietInsideTheWindow()
    {
        var inside = Start + TimeSpan.FromSeconds(30);

        Assert.Equal((false, Start), ActivityLogLimit.Next(StepLine.Started, Start, inside));
        Assert.Equal((false, Start), ActivityLogLimit.Next(StepLine.Failed, Start, inside));
    }

    [Fact]
    public void Next_StartOfARepeatFailure_DoesNotEndTheWindow()
    {
        var restart = Start + TimeSpan.FromSeconds(5);
        var (_, kept) = ActivityLogLimit.Next(StepLine.Started, Start, restart);

        Assert.False(ActivityLogLimit.Next(StepLine.Failed, kept, restart + TimeSpan.FromSeconds(5)).Write);
    }

    [Fact]
    public void Next_StepThatEndsWell_IsWrittenAndClosesTheWindow()
    {
        var inside = Start + TimeSpan.FromSeconds(30);

        Assert.Equal((true, default(DateTime)), ActivityLogLimit.Next(StepLine.Ended, Start, inside));
    }

    [Fact]
    public void Next_WritesAgainAfterTheWindow()
    {
        var after = Start + ActivityLogLimit.FailureLogWindow;

        Assert.Equal((true, Start), ActivityLogLimit.Next(StepLine.Started, Start, after));
        Assert.Equal((true, after), ActivityLogLimit.Next(StepLine.Failed, Start, after));
    }

    [Fact]
    public void InQuietWindow_IsFalse_WhenNoFailureWasLogged()
    {
        Assert.False(ActivityLogLimit.InQuietWindow(default, Start));
    }

    [Fact]
    public void InQuietWindow_IsTrue_WhileTheWindowRuns()
    {
        Assert.True(ActivityLogLimit.InQuietWindow(Start, Start + TimeSpan.FromSeconds(30)));
        Assert.False(ActivityLogLimit.InQuietWindow(Start, Start + ActivityLogLimit.FailureLogWindow));
    }
}
