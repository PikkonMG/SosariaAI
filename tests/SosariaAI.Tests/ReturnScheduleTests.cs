using System;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class ReturnScheduleTests
{
    [Fact]
    public void DelayUntil_PastTime_ReturnsZero()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var returnAt = now.AddMinutes(-1);

        Assert.Equal(TimeSpan.Zero, ReturnSchedule.DelayUntil(returnAt, now));
    }

    [Fact]
    public void DelayUntil_FutureTime_ReturnsRemaining()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var returnAt = now.AddMinutes(5);

        Assert.Equal(TimeSpan.FromMinutes(5), ReturnSchedule.DelayUntil(returnAt, now));
    }

    [Fact]
    public void Unscheduled_IsNotScheduled() =>
        Assert.False(ReturnSchedule.IsScheduled(default));
}
