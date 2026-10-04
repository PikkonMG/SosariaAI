using Server;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TamingAreaTests
{
    private const string Destard = "Destard";
    private const string Shame = "Shame";
    private const string OpenGround = null;

    private static readonly Point3D DestardUpper = new(5236, 963, -31);
    private static readonly Point3D DestardLower = new(5143, 907, 0);
    private static readonly Point3D ForestGlade = new(1000, 1000, 0);

    [Fact]
    public void SameArea_EverySpotOfOneDungeonIsOneArea()
    {
        // Destard's many spawn spots drew a dozen tamers at once.
        Assert.True(TamingGrounds.SameArea(DestardUpper, Destard, DestardLower, Destard));
        Assert.False(TamingGrounds.SameArea(DestardUpper, Destard, DestardUpper, Shame));
    }

    [Fact]
    public void SameArea_OpenGroundWithinTheAreaReach()
    {
        var near = new Point3D(ForestGlade.X + TamingGrounds.TamingAreaTiles, ForestGlade.Y, 0);
        var far = new Point3D(ForestGlade.X + TamingGrounds.TamingAreaTiles + 1, ForestGlade.Y, 0);

        Assert.True(TamingGrounds.SameArea(ForestGlade, OpenGround, near, OpenGround));
        Assert.False(TamingGrounds.SameArea(ForestGlade, OpenGround, far, OpenGround));
    }

    [Fact]
    public void SameArea_ADungeonIsNotTheLandAroundIt()
    {
        Assert.False(TamingGrounds.SameArea(DestardUpper, Destard, DestardUpper, OpenGround));
    }
}
