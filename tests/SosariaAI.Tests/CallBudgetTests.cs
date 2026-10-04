using System;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class CallBudgetTests
{
    private static readonly DateTime Noon = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CanSpend_HourLimit_BlocksThenRecovers()
    {
        var budget = new CallBudget(maxPaidCallsPerDay: 1000, maxPaidCallsPerHour: 2);

        Assert.True(budget.CanSpend(Noon));
        budget.Record(Noon);
        budget.Record(Noon.AddMinutes(1));
        Assert.False(budget.CanSpend(Noon.AddMinutes(2)));
        Assert.Equal(2, budget.PaidCallsThisHour);

        Assert.True(budget.CanSpend(Noon.AddHours(1).AddMinutes(2)));
    }

    [Fact]
    public void CanSpend_DayLimit_BlocksUntilRollingDayPasses()
    {
        var budget = new CallBudget(maxPaidCallsPerDay: 3, maxPaidCallsPerHour: 100);

        budget.Record(Noon);
        budget.Record(Noon.AddHours(2));
        budget.Record(Noon.AddHours(4));
        Assert.False(budget.CanSpend(Noon.AddHours(5)));
        Assert.Equal(3, budget.PaidCallsToday);

        Assert.True(budget.CanSpend(Noon.AddHours(25)));
    }

    [Fact]
    public void NewInstance_ResetsCounters()
    {
        var spent = new CallBudget(1, 1);
        spent.Record(Noon);
        Assert.False(spent.CanSpend(Noon.AddMinutes(1)));

        var fresh = new CallBudget(1, 1);
        Assert.True(fresh.CanSpend(Noon.AddMinutes(1)));
        Assert.Equal(0, fresh.PaidCallsToday);
    }
}
