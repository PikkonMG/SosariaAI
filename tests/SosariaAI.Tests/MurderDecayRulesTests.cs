using System;
using SosariaAI.Combat;
using SosariaAI.Population;
using Xunit;

namespace SosariaAI.Tests;

public class MurderDecayRulesTests
{
    private const int NoMurders = 0;
    private const int OneMurder = 1;
    private const int MinutesPerShortTerm = 8 * 60;

    private static readonly DateTime Noon = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ShortTermPeriod = TimeSpan.FromHours(8);
    private static readonly TimeSpan LongTermPeriod = TimeSpan.FromHours(40);
    private static readonly MurderMarks FreshMarks = new(ShortTermPeriod, LongTermPeriod);

    [Fact]
    public void LoggedInSince_FirstCount_IsZero() =>
        Assert.Equal(TimeSpan.Zero, MurderDecayRules.LoggedInSince(default, Noon));

    [Fact]
    public void LoggedInSince_ClockRanBack_IsZero() =>
        Assert.Equal(TimeSpan.Zero, MurderDecayRules.LoggedInSince(Noon, Noon - OneMinute));

    [Fact]
    public void LoggedInSince_AMinuteLater_IsAMinute() =>
        Assert.Equal(OneMinute, MurderDecayRules.LoggedInSince(Noon, Noon + OneMinute));

    [Fact]
    public void Advance_NoMurders_KeepsBothMarks() =>
        Assert.Equal(FreshMarks, MurderDecayRules.Advance(FreshMarks, OneMinute, NoMurders, NoMurders, planRed: false));

    [Fact]
    public void Advance_EarnedRed_MovesBothMarks()
    {
        var marks = MurderDecayRules.Advance(FreshMarks, OneMinute, OneMurder, PkRules.MurdersToRed, planRed: false);

        Assert.Equal(ShortTermPeriod - OneMinute, marks.ShortTermElapse);
        Assert.Equal(LongTermPeriod - OneMinute, marks.LongTermElapse);
    }

    [Fact]
    public void Advance_PlanRedAtTheLine_HoldsLongTermOnly()
    {
        var marks = MurderDecayRules.Advance(FreshMarks, OneMinute, OneMurder, PkRules.MurdersToRed, planRed: true);

        Assert.Equal(ShortTermPeriod - OneMinute, marks.ShortTermElapse);
        Assert.Equal(LongTermPeriod, marks.LongTermElapse);
    }

    [Fact]
    public void Advance_PlanRedAboveTheLine_MovesLongTerm()
    {
        var marks = MurderDecayRules.Advance(FreshMarks, OneMinute, NoMurders, PkRules.MurdersToRed + 1, planRed: true);

        Assert.Equal(ShortTermPeriod, marks.ShortTermElapse);
        Assert.Equal(LongTermPeriod - OneMinute, marks.LongTermElapse);
    }

    [Fact]
    public void Advance_ShortTermPeriodInMinutes_ReachesTheMark()
    {
        var marks = FreshMarks;

        for (var i = 0; i < MinutesPerShortTerm; i++)
        {
            marks = MurderDecayRules.Advance(marks, OneMinute, OneMurder, OneMurder, planRed: false);
        }

        Assert.Equal(TimeSpan.Zero, marks.ShortTermElapse);
        Assert.Equal(LongTermPeriod - ShortTermPeriod, marks.LongTermElapse);
    }

    [Fact]
    public void WentBlue_OnlyWhenCrossingTheLine()
    {
        Assert.True(MurderDecayRules.WentBlue(PkRules.MurdersToRed, PkRules.MurdersToRed - 1));
        Assert.False(MurderDecayRules.WentBlue(PkRules.MurdersToRed + 1, PkRules.MurdersToRed));
        Assert.False(MurderDecayRules.WentBlue(PkRules.MurdersToRed - 1, PkRules.MurdersToRed - 2));
    }
}
