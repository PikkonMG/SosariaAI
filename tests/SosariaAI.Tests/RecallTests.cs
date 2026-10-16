using System;
using SosariaAI.Memory;
using Xunit;

namespace SosariaAI.Tests;

public class RecallTests
{
    // Local wall-clock times: the day words follow the server's local day, as the day part does,
    // so the cases hold in any time zone the tests run in.
    private static readonly DateTime JustAfterMidnight = new(2026, 10, 3, 0, 30, 0, DateTimeKind.Local);
    private static readonly DateTime LateEvening = new(2026, 10, 3, 23, 30, 0, DateTimeKind.Local);

    [Fact]
    public void WhenWord_AnHourBeforeLocalMidnightIsYesterday()
    {
        var now = JustAfterMidnight.ToUniversalTime();
        var at = JustAfterMidnight.AddHours(-1).ToUniversalTime();

        Assert.Equal("yesterday", Recall.WhenWord(at, now));
    }

    [Fact]
    public void WhenWord_TheSameLocalDayIsToday()
    {
        // Late evening west of Greenwich is already the next UTC day; it is still today.
        var now = LateEvening.ToUniversalTime();
        var at = JustAfterMidnight.ToUniversalTime();

        Assert.Equal("today", Recall.WhenWord(at, now));
    }
}
