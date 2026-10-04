using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class GuardedLegsTests
{
    // A town whose guards cover x 40 to 60, every y.
    private const int TownWest = 40;
    private const int TownEast = 60;

    private static bool Town(int x, int y, int z) => x >= TownWest && x <= TownEast;

    [Fact]
    public void Crosses_ALegThroughTheTownBetweenTwoOpenEnds()
    {
        Assert.True(GuardedLegs.Crosses(new Point3D(0, 0, 0), new Point3D(100, 0, 0), Town));
        Assert.True(GuardedLegs.Crosses(new Point3D(100, 30, 0), new Point3D(0, 0, 0), Town));
    }

    [Fact]
    public void Crosses_NotALegThatKeepsOutOfTheTown()
    {
        Assert.False(GuardedLegs.Crosses(new Point3D(0, 0, 0), new Point3D(TownWest - 1, 80, 0), Town));
        Assert.False(GuardedLegs.Crosses(new Point3D(TownEast + 1, 0, 0), new Point3D(TownEast + 1, 0, 0), Town));
    }

    [Fact]
    public void Crosses_OnlyTheTilesBetweenTheEndsCount()
    {
        // An end on the town's edge is the node's own business: the node bars see to it.
        Assert.False(GuardedLegs.Crosses(new Point3D(TownWest, 0, 0), new Point3D(TownWest - 1, 0, 0), Town));
        Assert.True(GuardedLegs.Crosses(new Point3D(TownWest - 2, 0, 0), new Point3D(TownWest + 1, 0, 0), Town));
    }

    [Theory]
    [InlineData(true, false, true, true, true)]
    [InlineData(false, false, true, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, true, false, false)]
    public void Shows_OnlyALivingRedThatMeetsTheGuardsOnARoadPlannedClearOfThem(
        bool murderer,
        bool ghost,
        bool roadClearOfGuards,
        bool metGuards,
        bool shows
    ) =>
        Assert.Equal(shows, GuardedLegs.Shows(murderer, ghost, roadClearOfGuards, metGuards));
}
