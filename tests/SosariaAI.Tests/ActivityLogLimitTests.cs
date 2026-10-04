using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class ActivityLogLimitTests
{
    private static readonly DateTime Start = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AllowFailureLog_FirstLineAlways()
    {
        Assert.True(ActivityLogLimit.AllowFailureLog(default, Start));
    }

    [Fact]
    public void AllowFailureLog_BlocksInsideTheWindow()
    {
        Assert.False(ActivityLogLimit.AllowFailureLog(Start, Start + TimeSpan.FromSeconds(59)));
        Assert.True(ActivityLogLimit.AllowFailureLog(Start, Start + ActivityLogLimit.FailureLogWindow));
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
