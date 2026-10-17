using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class GroupBurstRulesTests
{
    private const long LandAt = 50000;
    private const long Earlier = 49000;
    private const long Later = 51000;

    [Fact]
    public void IsOpen_OnlyWhileTheLandingTickIsStillToCome()
    {
        Assert.True(GroupBurstRules.IsOpen(LandAt, Earlier));
        Assert.False(GroupBurstRules.IsOpen(LandAt, LandAt));
        Assert.False(GroupBurstRules.IsOpen(LandAt, Later));
    }

    [Fact]
    public void IsExpired_ShortlyAfterTheSpellsLanded()
    {
        Assert.False(GroupBurstRules.IsExpired(LandAt, LandAt));
        Assert.False(GroupBurstRules.IsExpired(LandAt, LandAt + GroupBurstRules.ExpireAfterMs - 1));
        Assert.True(GroupBurstRules.IsExpired(LandAt, LandAt + GroupBurstRules.ExpireAfterMs));
    }

    [Fact]
    public void Keep_TheEarliestOpenCall_ElseTheNewOne()
    {
        Assert.Equal(LandAt, GroupBurstRules.Keep(open: true, LandAt, Later));
        Assert.Equal(Earlier, GroupBurstRules.Keep(open: true, LandAt, Earlier));
        Assert.Equal(Later, GroupBurstRules.Keep(open: false, Earlier, Later));
    }
}
