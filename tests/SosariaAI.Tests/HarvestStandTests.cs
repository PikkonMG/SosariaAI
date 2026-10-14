using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class HarvestStandTests
{
    private const int ResourceX = 10;
    private const int ResourceY = 20;
    private const int ResourceZ = 5;

    private static readonly Point3D Resource = new(ResourceX, ResourceY, ResourceZ);

    [Fact]
    public void Neighbours_CardinalsBeforeDiagonals()
    {
        var neighbours = HarvestStand.Neighbours(Resource);

        Assert.Equal(
            HarvestStand.CardinalNeighbourCount + HarvestStand.DiagonalNeighbourCount,
            neighbours.Count
        );

        Assert.Equal(new Point2D(ResourceX, ResourceY - HarvestStand.Step), neighbours[0]);
        Assert.Equal(new Point2D(ResourceX + HarvestStand.Step, ResourceY), neighbours[1]);
        Assert.Equal(new Point2D(ResourceX, ResourceY + HarvestStand.Step), neighbours[2]);
        Assert.Equal(new Point2D(ResourceX - HarvestStand.Step, ResourceY), neighbours[3]);

        var diagonal = HarvestStand.CardinalNeighbourCount;
        Assert.Equal(new Point2D(ResourceX + HarvestStand.Step, ResourceY - HarvestStand.Step), neighbours[diagonal]);
        Assert.Equal(new Point2D(ResourceX + HarvestStand.Step, ResourceY + HarvestStand.Step), neighbours[diagonal + 1]);
        Assert.Equal(new Point2D(ResourceX - HarvestStand.Step, ResourceY + HarvestStand.Step), neighbours[diagonal + 2]);
        Assert.Equal(new Point2D(ResourceX - HarvestStand.Step, ResourceY - HarvestStand.Step), neighbours[diagonal + 3]);
    }

    [Fact]
    public void Neighbours_DoesNotIncludeResource()
    {
        var neighbours = HarvestStand.Neighbours(Resource);
        var resourceTile = new Point2D(ResourceX, ResourceY);

        for (var i = 0; i < neighbours.Count; i++)
        {
            Assert.NotEqual(resourceTile, neighbours[i]);
        }
    }

    [Fact]
    public void TryBeside_IgnoresResourceTile()
    {
        var north = new Point3D(ResourceX, ResourceY - HarvestStand.Step, ResourceZ);
        Point3D[] candidates = [Resource, north];

        Assert.True(HarvestStand.TryBeside(Resource, candidates, Resource, out var standAt));
        Assert.Equal(north, standAt);
        Assert.NotEqual(Resource, standAt);
    }

    [Fact]
    public void TryBeside_ResourceOnly_ReturnsFalse()
    {
        Point3D[] candidates = [Resource];

        Assert.False(HarvestStand.TryBeside(Resource, candidates, Resource, out _));
    }

    [Fact]
    public void TryBeside_PrefersClosestToFrom()
    {
        var north = new Point3D(ResourceX, ResourceY - HarvestStand.Step, ResourceZ);
        var east = new Point3D(ResourceX + HarvestStand.Step, ResourceY, ResourceZ);
        var from = new Point3D(east.X + HarvestStand.Step, ResourceY, ResourceZ);
        Point3D[] candidates = [north, east];

        Assert.True(HarvestStand.TryBeside(Resource, candidates, from, out var standAt));
        Assert.Equal(east, standAt);
    }

    [Fact]
    public void WalkRange_TheWalkStopsWhereTheResourceIsStillInTheEnginesReach()
    {
        // Woodcutting and mining reach two tiles; a walk that stopped two short of the stand
        // tile beside the tree stood three off, and the engine refused every swing.
        const int harvestReach = 2;
        var besideTree = new Point3D(ResourceX, ResourceY - HarvestStand.Step, ResourceZ);

        Assert.Equal(HarvestStand.Step, HarvestStand.WalkRange(harvestReach, harvestReach, besideTree, Resource));
        Assert.True(
            NavMetric.Chebyshev(besideTree, Resource) +
            HarvestStand.WalkRange(harvestReach, harvestReach, besideTree, Resource) <= harvestReach
        );
    }

    [Fact]
    public void WalkRange_NeverPastTheWalkersOwnRange()
    {
        // A fisher reaches the water at four tiles but stands on the shore beside it.
        const int fishReach = 4;
        var shore = new Point3D(ResourceX - HarvestStand.Step, ResourceY, ResourceZ);

        Assert.Equal(FishSkill.ShoreWalkRange, HarvestStand.WalkRange(FishSkill.ShoreWalkRange, fishReach, shore, Resource));
    }

    [Fact]
    public void WalkRange_AStandTileAtTheEdgeOfReachIsWalkedOnto()
    {
        const int reach = 2;
        var edge = new Point3D(ResourceX + reach, ResourceY, ResourceZ);
        var past = new Point3D(ResourceX + reach + HarvestStand.Step, ResourceY, ResourceZ);

        Assert.Equal(HarvestStand.OnTheTile, HarvestStand.WalkRange(reach, reach, edge, Resource));
        Assert.Equal(HarvestStand.OnTheTile, HarvestStand.WalkRange(reach, reach, past, Resource));
    }

    [Fact]
    public void TryBeside_EqualDistance_PrefersCardinalOrder()
    {
        var northEast = new Point3D(ResourceX + HarvestStand.Step, ResourceY - HarvestStand.Step, ResourceZ);
        var north = new Point3D(ResourceX, ResourceY - HarvestStand.Step, ResourceZ);
        Point3D[] candidates = [northEast, north];

        Assert.True(HarvestStand.TryBeside(Resource, candidates, Resource, out var standAt));
        Assert.Equal(north, standAt);
    }
}
