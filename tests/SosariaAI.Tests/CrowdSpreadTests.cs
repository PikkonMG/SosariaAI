using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class CrowdSpreadTests
{
    [Fact]
    public void IsPacked_FourBodiesInTightRange()
    {
        Assert.False(CrowdSpread.IsPacked(CrowdSpread.PackedCount - 1));
        Assert.True(CrowdSpread.IsPacked(CrowdSpread.PackedCount));
        Assert.True(CrowdSpread.IsPacked(CrowdSpread.PackedCount + 3));
    }

    [Fact]
    public void CountWithin_CountsOnlyTheTightRing()
    {
        var from = new Point3D(100, 100, 0);
        IReadOnlyList<Point3D> others =
        [
            new(100, 101, 0),
            new(102, 100, 0),
            new(100, 100 + CrowdSpread.TightRange + 1, 0)
        ];

        Assert.Equal(2, CrowdSpread.CountWithin(from, others, CrowdSpread.TightRange));
        Assert.Equal(0, CrowdSpread.CountWithin(from, null, CrowdSpread.TightRange));
    }

    [Fact]
    public void StepAway_LeavesThePackedTile()
    {
        var from = new Point3D(50, 50, 0);
        IReadOnlyList<Point3D> others =
        [
            from,
            new(50, 51, 0),
            new(51, 50, 0),
            new(51, 51, 0)
        ];

        var step = CrowdSpread.StepAway(from, others);

        Assert.NotEqual(from, step);
        Assert.True(NavMetric.Chebyshev(from, step) <= CrowdSpread.SpreadRadius);
        Assert.True(CrowdSpread.CountWithin(step, others, CrowdSpread.TightRange) <
                    CrowdSpread.CountWithin(from, others, CrowdSpread.TightRange));
    }

    [Fact]
    public void StepAway_EmptyCrowd_StaysPut()
    {
        var from = new Point3D(10, 10, 0);

        Assert.Equal(from, CrowdSpread.StepAway(from, null));
        Assert.Equal(from, CrowdSpread.StepAway(from, []));
    }
}
