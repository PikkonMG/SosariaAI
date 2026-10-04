using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class StealthRulesTests
{
    private const string ExpectedKind = "Stealth";
    private const double ClassicHidingRequirement = 80;
    private const int RingPoints = 8;

    [Fact]
    public void Kind_IsStealth()
    {
        Assert.Equal(ExpectedKind, StealthRules.Kind);
        Assert.Equal(ExpectedKind, new StealthSkill().Name);
    }

    [Fact]
    public void MayBegin_Null_IsFalse() =>
        Assert.False(StealthRules.MayBegin(null, ClassicHidingRequirement));

    [Fact]
    public void HidingMeets_NeedsTheEngineRequirement()
    {
        Assert.False(StealthRules.HidingMeets(ClassicHidingRequirement - 1, ClassicHidingRequirement));
        Assert.True(StealthRules.HidingMeets(ClassicHidingRequirement, ClassicHidingRequirement));
    }

    [Fact]
    public void MayCreep_NeedsTheHidingAndArmorTheEngineAccepts()
    {
        Assert.True(StealthRules.MayCreep(ClassicHidingRequirement, ClassicHidingRequirement, StealthRules.ArmorLimit - 1));
        Assert.False(StealthRules.MayCreep(ClassicHidingRequirement - 1, ClassicHidingRequirement, 0));
        Assert.False(StealthRules.MayCreep(ClassicHidingRequirement, ClassicHidingRequirement, StealthRules.ArmorLimit));
    }

    [Fact]
    public void RingPoint_CirclesTheAnchorAndWraps()
    {
        var anchor = new Point3D(1400, 1600, 10);

        for (var i = 0; i < RingPoints; i++)
        {
            var point = StealthRules.RingPoint(anchor, i);
            Assert.Equal(StealthRules.RingRadius, NavMetric.Chebyshev(anchor, point));
        }

        Assert.Equal(StealthRules.RingPoint(anchor, 0), StealthRules.RingPoint(anchor, RingPoints));
        Assert.NotEqual(StealthRules.RingPoint(anchor, 0), StealthRules.RingPoint(anchor, 1));
    }
}
