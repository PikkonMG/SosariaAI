using System.Collections.Generic;
using SosariaAI.Population;
using Xunit;

namespace SosariaAI.Tests;

public class SessionHoursTests
{
    private const int CustomStartHour = 9;
    private const int CustomEndHour = 18;
    private const int WrapStartHour = 22;
    private const int WrapEndHour = 6;
    private const int ClosedHour = 23;
    private const int WrapIncludedEarly = 2;
    private const int WrapExcludedHour = 10;
    private const int SeedSample = 42;
    private const uint PeopleSampled = 300;
    private const int MinDistinctStarts = 8;
    private const int HoursPerDay = 24;

    [Fact]
    public void Resolve_AuthoredHours_AreKept()
    {
        Assert.Equal((CustomStartHour, CustomEndHour), SessionHours.Resolve(1, CustomStartHour, CustomEndHour));
    }

    [Fact]
    public void Resolve_Unauthored_IsTheSameForOnePerson()
    {
        Assert.Equal(SessionHours.Resolve(SeedSample, null, null), SessionHours.Resolve(SeedSample, null, null));
    }

    [Fact]
    public void Resolve_Unauthored_SpreadsDaysAcrossPeople()
    {
        // Everyone shared one 8-to-23 day and the town emptied on the same hour.
        var starts = new HashSet<int>();

        for (uint serial = 0; serial < PeopleSampled; serial++)
        {
            var (start, end) = SessionHours.Resolve(serial, null, null);
            var length = (end - start + HoursPerDay) % HoursPerDay;

            starts.Add(start);
            Assert.InRange(length, SessionHours.MinActiveHours, SessionHours.MaxActiveHours);
        }

        Assert.True(starts.Count >= MinDistinctStarts, $"only {starts.Count} start hours");
    }

    [Fact]
    public void Resolve_OnlyOneHourAuthored_KeepsItAndSeedsTheOther()
    {
        var (start, _) = SessionHours.Resolve(SeedSample, CustomStartHour, null);
        var (_, end) = SessionHours.Resolve(SeedSample, null, CustomEndHour);

        Assert.Equal(CustomStartHour, start);
        Assert.Equal(CustomEndHour, end);
    }

    [Fact]
    public void IsActive_WrapsMidnight()
    {
        Assert.True(SessionHours.IsActive(ClosedHour, WrapStartHour, WrapEndHour));
        Assert.True(SessionHours.IsActive(WrapIncludedEarly, WrapStartHour, WrapEndHour));
        Assert.False(SessionHours.IsActive(WrapExcludedHour, WrapStartHour, WrapEndHour));
    }

    [Fact]
    public void IsActive_StartEqualsEnd_IsAlwaysOn()
    {
        Assert.True(SessionHours.IsActive(WrapExcludedHour, WrapStartHour, WrapStartHour));
        Assert.True(SessionHours.IsActive(WrapIncludedEarly, WrapEndHour, WrapEndHour));
        Assert.True(SessionHours.IsActive(ClosedHour, 0, 0));
    }
}
