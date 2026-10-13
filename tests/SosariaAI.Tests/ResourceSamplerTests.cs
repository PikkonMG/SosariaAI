using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class ResourceSamplerTests
{
    private const int Centre = 100;

    [Fact]
    public void Roles_EnoughTrees_IsLumber()
    {
        Assert.Equal([ResourceKind.Lumber], ResourceSampler.Roles(Centre, Centre, 0, TreeAtCentre));
    }

    [Fact]
    public void Roles_EnoughOre_IsMine()
    {
        Assert.Equal([ResourceKind.Mine], ResourceSampler.Roles(Centre, Centre, 0, OreAtCentre));
    }

    [Fact]
    public void Roles_SparseOrNull_IsEmpty()
    {
        Assert.Empty(ResourceSampler.Roles(Centre, Centre, 0, None));
        Assert.Empty(ResourceSampler.Roles(Centre, Centre, 0, null));
        Assert.Empty(ResourceSampler.Roles(Centre, Centre, 0, OneTree));
    }

    [Fact]
    public void Roles_WaterNearby_IsAShore()
    {
        Assert.Equal([ResourceKind.Fish], ResourceSampler.Roles(Centre, Centre, 0, WaterWest));
    }

    [Fact]
    public void Roles_AWoodByTheWater_IsBoth()
    {
        Assert.Equal([ResourceKind.Lumber, ResourceKind.Fish], ResourceSampler.Roles(Centre, Centre, 0, TreesEastWaterWest));
    }

    private static ResourceHit TreeAtCentre(int x, int y, int _) =>
        InSample(x, y) ? ResourceHit.Tree : ResourceHit.None;

    private static ResourceHit OreAtCentre(int x, int y, int _) =>
        InSample(x, y) ? ResourceHit.Ore : ResourceHit.None;

    private static ResourceHit WaterWest(int x, int y, int _) =>
        InSample(x, y) && x < Centre ? ResourceHit.Water : ResourceHit.None;

    private static ResourceHit TreesEastWaterWest(int x, int y, int _) =>
        !InSample(x, y) ? ResourceHit.None : x < Centre ? ResourceHit.Water : x > Centre ? ResourceHit.Tree : ResourceHit.None;

    private static ResourceHit None(int x, int y, int z)
    {
        _ = x;
        _ = y;
        _ = z;
        return ResourceHit.None;
    }

    private static ResourceHit OneTree(int x, int y, int _) =>
        x == Centre && y == Centre ? ResourceHit.Tree : ResourceHit.None;

    private static bool InSample(int x, int y)
    {
        var dx = x - Centre;
        if (dx < 0)
        {
            dx = -dx;
        }

        var dy = y - Centre;
        if (dy < 0)
        {
            dy = -dy;
        }

        return dx <= ResourceSampler.SampleRadius && dy <= ResourceSampler.SampleRadius;
    }
}
