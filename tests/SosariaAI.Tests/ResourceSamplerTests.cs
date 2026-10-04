using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class ResourceSamplerTests
{
    private const int Centre = 100;

    [Fact]
    public void Classify_EnoughTrees_IsLumber()
    {
        var role = ResourceSampler.Classify(Centre, Centre, 0, TreeAtCentre);
        Assert.Equal(ResourceKind.Lumber, role);
    }

    [Fact]
    public void Classify_EnoughOre_IsMine()
    {
        var role = ResourceSampler.Classify(Centre, Centre, 0, OreAtCentre);
        Assert.Equal(ResourceKind.Mine, role);
    }

    [Fact]
    public void Classify_SparseOrNull_IsNull()
    {
        Assert.Null(ResourceSampler.Classify(Centre, Centre, 0, None));
        Assert.Null(ResourceSampler.Classify(Centre, Centre, 0, null));
        Assert.Null(ResourceSampler.Classify(Centre, Centre, 0, OneTree));
    }

    private static ResourceHit TreeAtCentre(int x, int y, int _) =>
        InSample(x, y) ? ResourceHit.Tree : ResourceHit.None;

    private static ResourceHit OreAtCentre(int x, int y, int _) =>
        InSample(x, y) ? ResourceHit.Ore : ResourceHit.None;

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
