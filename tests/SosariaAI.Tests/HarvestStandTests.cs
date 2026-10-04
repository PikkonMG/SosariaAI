using Server;
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
    public void TryBeside_EqualDistance_PrefersCardinalOrder()
    {
        var northEast = new Point3D(ResourceX + HarvestStand.Step, ResourceY - HarvestStand.Step, ResourceZ);
        var north = new Point3D(ResourceX, ResourceY - HarvestStand.Step, ResourceZ);
        Point3D[] candidates = [northEast, north];

        Assert.True(HarvestStand.TryBeside(Resource, candidates, Resource, out var standAt));
        Assert.Equal(north, standAt);
    }
}
