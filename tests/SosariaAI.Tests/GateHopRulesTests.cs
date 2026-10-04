using System;
using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class GateHopRulesTests
{
    private static readonly Point3D BritainMoon = new(1336, 1997, 5);
    private static readonly Point3D MinocMoon = new(2701, 692, 5);
    private static readonly DateTime Noon = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ShouldHop_SameGate_IsFalse()
    {
        Assert.False(GateHopRules.ShouldHop(BritainMoon, BritainMoon, sameMap: true));
        Assert.False(GateHopRules.ShouldHop(BritainMoon, new Point3D(1338, 1998, 5), sameMap: true));
    }

    [Fact]
    public void ShouldHop_FarTownOnTheSameMap_IsTrue()
    {
        Assert.True(GateHopRules.ShouldHop(BritainMoon, MinocMoon, sameMap: true));
    }

    [Fact]
    public void MayHop_WaitsOutTheRest()
    {
        Assert.True(GateHopRules.MayHop(default, Noon));
        Assert.False(GateHopRules.MayHop(Noon, Noon.AddSeconds(GateHopRules.RestSeconds - 1)));
        Assert.True(GateHopRules.MayHop(Noon, Noon.AddSeconds(GateHopRules.RestSeconds)));
    }

    [Fact]
    public void StepOff_LeavesThePad()
    {
        var off = GateHopRules.StepOff(BritainMoon, MinocMoon);
        Assert.True(NavMetric.Chebyshev(off, BritainMoon) >= GateHopRules.StepOffTiles);
        Assert.NotEqual(BritainMoon, off);
    }

    [Fact]
    public void Landed_NearThePlannedLandingOnTheSameFloor()
    {
        var landing = new Point3D(5243, 1007, 0);

        Assert.True(GateHopRules.Landed(new Point3D(5244, 1008, 0), landing));
        Assert.False(GateHopRules.Landed(new Point3D(5243, 1007, 40), landing));
        Assert.False(GateHopRules.Landed(new Point3D(1176, 2635, 0), landing));
        Assert.False(GateHopRules.Landed(landing, Point3D.Zero));
    }

    [Fact]
    public void Carried_AJumpFurtherThanAWalk()
    {
        Assert.False(GateHopRules.Carried(BritainMoon, new Point3D(1337, 1998, 5), sameMap: true));
        Assert.True(GateHopRules.Carried(BritainMoon, MinocMoon, sameMap: true));
        Assert.True(GateHopRules.Carried(BritainMoon, BritainMoon, sameMap: false));
    }

    [Fact]
    public void GateLandsNear_ALeadersGateLandsCloseToTheLeader()
    {
        Assert.True(GateHopRules.GateLandsNear(MinocMoon, new Point3D(2710, 700, 0)));
        Assert.False(GateHopRules.GateLandsNear(MinocMoon, BritainMoon));
    }

    [Fact]
    public void LeftThePad_OnlyPastTheReachOfThePad()
    {
        var trinsicMoon = new Point3D(1828, 2948, -20);

        Assert.False(GateHopRules.LeftThePad(trinsicMoon, trinsicMoon));
        Assert.False(GateHopRules.LeftThePad(new Point3D(1828 + GatePad.ReachTiles, 2948, -20), trinsicMoon));
        Assert.True(GateHopRules.LeftThePad(new Point3D(1828 + GatePad.ReachTiles + 1, 2948, -20), trinsicMoon));
        Assert.True(GateHopRules.LeftThePad(new Point3D(1813, 3409, 0), trinsicMoon));
    }
}
