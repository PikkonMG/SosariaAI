using System;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class WorldPlayWarnTests
{
    static WorldPlayWarnTests() => Timer.Init(0);

    public WorldPlayWarnTests() => TestMap.EnsureInternal();

    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);
    private static readonly Serial Red = (Serial)0x100u;
    private static readonly Serial OtherRed = (Serial)0x101u;

    [Fact]
    public void SameTargetDue_SameVictimInsideRetargetWait_IsNotDue()
    {
        Assert.False(WorldPlay.SameTargetDue(Red, Now, Red, Now.AddMinutes(1), WorldPlay.RetargetWait));
        Assert.True(WorldPlay.SameTargetDue(Red, Now, OtherRed, Now.AddMinutes(1), WorldPlay.RetargetWait));
        Assert.True(WorldPlay.SameTargetDue(Red, Now, Red, Now.Add(WorldPlay.RetargetWait), WorldPlay.RetargetWait));
    }

    [Fact]
    public void IsWarnVictim_AMonster_IsNot() =>
        Assert.False(WorldPlay.IsWarnVictim(new WaterElemental((Serial)0x7B01)));

    [Fact]
    public void IsWarnVictim_ALivingPerson_Is() =>
        Assert.True(WorldPlay.IsWarnVictim(new SosariaCharacter((Serial)0x7B02)));

    [Fact]
    public void PairKey_IsTheSameEitherWay() =>
        Assert.Equal(WorldPlay.PairKey(Red, OtherRed), WorldPlay.PairKey(OtherRed, Red));

    // The rest itself is TimeRules.Rested (CommonRulesTests); the engine clock does not run here.
    [Fact]
    public void PairReady_APairThatNeverFoughtIsReady() =>
        Assert.True(WorldPlay.PairReady((Serial)0x7F3A, (Serial)0x7F3B, DateTime.UtcNow));

    [Fact]
    public void OutOfDeathGrace_TenMinutes()
    {
        Assert.True(WorldPlay.OutOfDeathGrace(default, Now));
        Assert.False(WorldPlay.OutOfDeathGrace(Now, Now.AddMinutes(WorldPlay.DeathGrace.TotalMinutes - 1)));
        Assert.True(WorldPlay.OutOfDeathGrace(Now, Now.Add(WorldPlay.DeathGrace)));
    }

    [Fact]
    public void InDeathGrace_EitherSideFreshlyDead()
    {
        var red = new SosariaCharacter((Serial)0x7B03);
        var raised = new SosariaCharacter((Serial)0x7B04);
        var monster = new WaterElemental((Serial)0x7B05);
        var insideGrace = Now.Add(WorldPlay.DeathGrace).AddMinutes(-1);
        var pastGrace = Now.Add(WorldPlay.DeathGrace);

        Assert.False(WorldPlay.InDeathGrace(red, raised, insideGrace));

        raised.MarkDeath(Now);

        Assert.True(WorldPlay.InDeathGrace(red, raised, insideGrace));
        Assert.True(WorldPlay.InDeathGrace(raised, red, insideGrace));
        Assert.True(WorldPlay.InDeathGrace(raised, monster, insideGrace));
        Assert.False(WorldPlay.InDeathGrace(red, monster, insideGrace));
        Assert.False(WorldPlay.InDeathGrace(red, raised, pastGrace));
    }
}
