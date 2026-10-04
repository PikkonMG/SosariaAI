using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PartyGateRulesTests
{
    private const int LeaderIndex = 0;

    [Fact]
    public void PickGater_TheLeaderWhenItCan_ElseTheFirstMemberWhoCan()
    {
        Assert.Equal(LeaderIndex, PartyGateRules.PickGater([true, true, false], LeaderIndex));
        Assert.Equal(2, PartyGateRules.PickGater([false, false, true, true], LeaderIndex));
        Assert.Equal(PartyGateRules.NoOne, PartyGateRules.PickGater([false, false], LeaderIndex));
        Assert.Equal(PartyGateRules.NoOne, PartyGateRules.PickGater(null, LeaderIndex));
        Assert.Equal(1, PartyGateRules.PickGater([false, true], PartyGateRules.NoOne));
    }

    [Fact]
    public void CastAgain_AFewTimesThenGiveUp()
    {
        Assert.True(PartyGateRules.CastAgain(1));
        Assert.True(PartyGateRules.CastAgain(PartyGateRules.MaxCasts - 1));
        Assert.False(PartyGateRules.CastAgain(PartyGateRules.MaxCasts));
    }

    [Fact]
    public void WaitsToRecast_OnlyWhileTheEnginesPauseAfterASpellRuns()
    {
        const long Now = 1000;

        Assert.True(PartyGateRules.WaitsToRecast(Now, Now + 1));
        Assert.False(PartyGateRules.WaitsToRecast(Now, Now));
        Assert.False(PartyGateRules.WaitsToRecast(Now, 0));
    }

    [Fact]
    public void MayStep_TheLeaderFirst_TheRestAfterIt()
    {
        Assert.True(PartyGateRules.MayStep(isLeader: true, leaderThrough: false));
        Assert.False(PartyGateRules.MayStep(isLeader: false, leaderThrough: false));
        Assert.True(PartyGateRules.MayStep(isLeader: false, leaderThrough: true));
    }

    [Fact]
    public void CasterGoes_WhenEveryoneIsThrough_OrItsHoldIsUp()
    {
        Assert.False(PartyGateRules.CasterGoes(waitingFor: 4, through: 3, TimeSpan.Zero));
        Assert.True(PartyGateRules.CasterGoes(waitingFor: 4, through: 4, TimeSpan.Zero));
        Assert.True(PartyGateRules.CasterGoes(waitingFor: 4, through: 1, PartyGateRules.HoldLimit));
        Assert.True(PartyGateRules.HoldLimit < PartyGateRules.GateLife);
    }

    [Fact]
    public void Expired_CastsAndOpenGatesHaveTheirOwnLimits()
    {
        Assert.False(PartyGateRules.Expired(open: false, PartyGateRules.CastLimit - TimeSpan.FromSeconds(1)));
        Assert.True(PartyGateRules.Expired(open: false, PartyGateRules.CastLimit));
        Assert.False(PartyGateRules.Expired(open: true, PartyGateRules.CastLimit));
        Assert.True(PartyGateRules.Expired(open: true, PartyGateRules.GateLife));
    }

    [Fact]
    public void Lines_AreEasyToCount()
    {
        Assert.Equal("Mira's party of 4 gated to Covetous", PartyGateRules.GatedLine("Mira", 4, "Covetous"));
        Assert.Equal(
            "Mira's party gate to Covetous fell through: the gate fizzled",
            PartyGateRules.FellThroughLine("Mira", "Covetous", "the gate fizzled")
        );
    }

    [Theory]
    [InlineData("on me, gating to {place} soon", true)]
    [InlineData("gate to shame up in a sec", true)]
    [InlineData("GATED there, come", true)]
    [InlineData("gather up on me", false)]
    [InlineData("get over here, we leaving", false)]
    [InlineData("gatekeeper says hi", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SpeaksOfGate_OnlyALineThatPromisesAGate(string line, bool speaks) =>
        Assert.Equal(speaks, PartyGateRules.SpeaksOfGate(line));

    [Fact]
    public void CrewTiles_ReachesPastTheMuster() => Assert.True(PartyGateRules.CrewTiles > LfgRules.MusterRange);
}
