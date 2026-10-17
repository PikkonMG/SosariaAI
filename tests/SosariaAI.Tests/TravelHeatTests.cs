using System;
using Server;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class TravelHeatTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    static TravelHeatTests() => Timer.Init(0);

    [Fact]
    public void InHeat_HalfAMinuteAfterABlowWithAPlayer()
    {
        Assert.False(TravelHeat.InHeat(default, Now));
        Assert.True(TravelHeat.InHeat(Now - TimeSpan.FromSeconds(3), Now));
        Assert.True(TravelHeat.InHeat(Now - TravelHeat.Delay + TimeSpan.FromSeconds(1), Now));
        Assert.False(TravelHeat.InHeat(Now - TravelHeat.Delay, Now));
    }

    [Fact]
    public void CoolsIn_TheRestOfTheHalfMinute_ZeroOnceCool()
    {
        var tenAgo = TimeSpan.FromSeconds(10);

        Assert.Equal(TravelHeat.Delay - tenAgo, TravelHeat.CoolsIn(Now - tenAgo, Now));
        Assert.Equal(TimeSpan.Zero, TravelHeat.CoolsIn(Now - TravelHeat.Delay, Now));
        Assert.Equal(TimeSpan.Zero, TravelHeat.CoolsIn(default, Now));
    }

    [Fact]
    public void MayTakeMoongate_NotGray_NotInTheHeat()
    {
        Assert.True(TravelHeat.MayTakeMoongate(criminal: false, inHeat: false));
        Assert.False(TravelHeat.MayTakeMoongate(criminal: true, inHeat: false));
        Assert.False(TravelHeat.MayTakeMoongate(criminal: false, inHeat: true));
    }

    [Fact]
    public void CountsBlow_OnlyABlowDealtToAPlayer_AVictimThatOnlyRunsMayTravel()
    {
        Assert.True(TravelHeat.CountsBlow(dealt: true, defenderIsPlayer: true, attackerIsSelf: true, aos: false));
        Assert.False(TravelHeat.CountsBlow(dealt: true, defenderIsPlayer: false, attackerIsSelf: true, aos: false));
        Assert.False(TravelHeat.CountsBlow(dealt: false, defenderIsPlayer: true, attackerIsSelf: false, aos: false));
        Assert.False(TravelHeat.CountsBlow(dealt: false, defenderIsPlayer: true, attackerIsSelf: false, aos: true));
        Assert.True(TravelHeat.CountsBlow(dealt: false, defenderIsPlayer: true, attackerIsSelf: true, aos: true));
    }
}
