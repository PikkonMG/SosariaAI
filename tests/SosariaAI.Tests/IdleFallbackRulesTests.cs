using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class IdleFallbackRulesTests
{
    [Fact]
    public void FleesInstead_WhenAThreatForcesARunItMayTake() =>
        Assert.True(IdleFallbackRules.FleesInstead(threatForcesRun: true, mayStillFlee: true));

    [Fact]
    public void Wanders_WhenNoThreatOrItHasFledEnough()
    {
        Assert.False(IdleFallbackRules.FleesInstead(threatForcesRun: false, mayStillFlee: true));
        Assert.False(IdleFallbackRules.FleesInstead(threatForcesRun: true, mayStillFlee: false));
    }
}
