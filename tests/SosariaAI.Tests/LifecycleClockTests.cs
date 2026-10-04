using System;
using SosariaAI.Population;
using Xunit;

namespace SosariaAI.Tests;

public class LifecycleClockTests
{
    private const int OffHour = 3;
    private const int DayStartHour = 8;
    private const int DayEndHour = 23;
    private const int EveningHour = 19;
    private const int HalfPastMinute = 30;
    private const double HalfHour = 0.5;
    private const uint Serial = 0x51;

    [Fact]
    public void IdlesOffHours_AFixtureOutsideItsHours()
    {
        Assert.True(LifecycleClock.IdlesOffHours(true, OffHour, Serial, DayStartHour, DayEndHour));
        Assert.False(LifecycleClock.IdlesOffHours(true, EveningHour, Serial, DayStartHour, DayEndHour));
    }

    [Fact]
    public void IdlesOffHours_NeverAnyoneElse()
    {
        Assert.False(LifecycleClock.IdlesOffHours(false, OffHour, Serial, DayStartHour, DayEndHour));
        Assert.False(LifecycleClock.IdlesOffHours(false, EveningHour, Serial, DayStartHour, DayEndHour));
    }

    [Fact]
    public void LogsIn_APersonOffTheWorld()
    {
        Assert.True(LifecycleClock.LogsIn(inWorld: false, returnFromDeathSet: false));
        Assert.False(LifecycleClock.LogsIn(inWorld: true, returnFromDeathSet: false));
    }

    [Fact]
    public void LogsIn_NotAGhostFallback_ItsReturnFromDeathStandsItUpAtHome()
    {
        Assert.False(LifecycleClock.LogsIn(inWorld: false, returnFromDeathSet: true));
        Assert.False(LifecycleClock.LogsIn(inWorld: true, returnFromDeathSet: true));
    }

    [Fact]
    public void HourOfDay_CountsTheMinutes()
    {
        var at = new DateTime(2026, 9, 23, EveningHour, HalfPastMinute, 0, DateTimeKind.Utc);
        var hour = LifecycleClock.HourOfDay(at);

        Assert.Equal(HalfHour, hour - Math.Floor(hour), precision: 6);
    }
}
