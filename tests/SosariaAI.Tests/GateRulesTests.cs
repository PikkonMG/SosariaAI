using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class GateRulesTests
{
    private const string ExpectedKind = "Gate";
    private const double ExpectedMinMagery = 70;

    [Fact]
    public void Kind_IsGate()
    {
        Assert.Equal(ExpectedKind, GateRules.Kind);
        Assert.Equal(ExpectedKind, new GateSkill().Name);
    }

    [Fact]
    public void MinMagery_AnAdeptsHandFromABook()
    {
        Assert.Equal(ExpectedMinMagery, GateRules.MinMagery);
        Assert.True(MarkRules.MinMagery < GateRules.MinMagery);
    }

    [Fact]
    public void Scroll_BringsTheCircleDown()
    {
        Assert.True(GateRules.ScrollMinMagery < GateRules.MinMagery);
        Assert.Equal(GateRules.ScrollMinMagery, TravelSpells.ScrollMinMagery(TravelSpellKind.Gate));
        Assert.Equal(GateRules.MinMagery, TravelSpells.BookMinMagery(TravelSpellKind.Gate));
    }

    [Theory]
    [InlineData(true, false, 0, true)]
    [InlineData(false, false, GateRules.AudienceForPublicGate, true)]
    [InlineData(false, false, GateRules.AudienceForPublicGate - 1, false)]
    [InlineData(false, true, 0, true)]
    public void PrefersGate_ForAPartyPetsOrACrowdElseRecall(bool leadsParty, bool bringsPets, int bystanders, bool gate) =>
        Assert.Equal(gate, GateRules.PrefersGate(leadsParty, bringsPets, bystanders));

    [Fact]
    public void PartyApi_NoLeader_OpensNothing()
    {
        var goal = new Point3D(1434, 1699, 0);

        Assert.False(GateRules.CanOpenToward(null, goal));
        Assert.False(GateRules.TryOpenToward(null, goal));
        Assert.Equal(0, GateRules.Bystanders(null));
    }

    [Fact]
    public void PartyGate_HoldsTheGateBeforeTheLeaderStepsIn()
    {
        var skill = new GateSkill(new Point3D(1434, 1699, 0), GateRules.PartyHold);

        Assert.Equal(ExpectedKind, skill.Name);
        Assert.True(GateRules.PartyHold > System.TimeSpan.Zero);
        Assert.False(skill.Begin(null));
    }
}
