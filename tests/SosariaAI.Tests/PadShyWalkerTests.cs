using System.Linq;
using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class PadShyWalkerTests
{
    private const int Floor = 0;
    private const int PadX = 5139;
    private const int PadY = 2015;
    private const int TrapX = 5130;
    private const int Approach = 4;

    private static readonly TrapZone ExitPad = new(PadX, PadY, Floor, TrapRules.OnTileReach);
    private static readonly TrapZone Spike = new(TrapX, PadY, Floor, TrapRules.OnTileReach);

    private static bool Open(int x, int y, int z) => true;

    [Fact]
    public void ShunningPads_APadHurtsItsTile_AndTheTrapsStillHurt()
    {
        var trapped = TestWalkers.Ground(Open, null) with { Harms = new TrapField([Spike]).Harms };

        var walker = Standable.ShunningPads(trapped, new TrapField([ExitPad]));

        Assert.True(walker.IsHarmed(PadX, PadY, Floor));
        Assert.True(walker.IsHarmed(TrapX, PadY, Floor));
        Assert.False(walker.IsHarmed(PadX + 1, PadY, Floor));
    }

    [Fact]
    public void ShunningPads_NoPads_IsTheSameWalker()
    {
        var walker = TestWalkers.Flat;

        Assert.Same(walker, Standable.ShunningPads(walker, TrapField.Empty));
        Assert.Same(walker, Standable.ShunningPads(walker, null));
    }

    [Fact]
    public void TileRoute_GoesRoundAnExitPadOnTheWay()
    {
        // The Orc Cave walk from the landing stepped onto the exit pad beside it.
        var walker = Standable.ShunningPads(TestWalkers.Ground(Open, null), new TrapField([ExitPad]));
        var from = new Point3D(PadX - Approach, PadY, Floor);
        var to = new Point3D(PadX + Approach, PadY, Floor);

        var trail = TileRoute.Trail(from, to, walker, null);

        Assert.NotEmpty(trail);
        Assert.DoesNotContain(trail, tile => tile.X == PadX && tile.Y == PadY);
    }

    [Fact]
    public void TileRoute_KeepsEveryTileBesideThePad_SoNoLegCutsAcrossIt()
    {
        // The game pathfinder walks each leg its own way: a leg past the pad must be one step.
        var walker = Standable.ShunningPads(TestWalkers.Ground(Open, null), new TrapField([ExitPad]));
        var from = new Point3D(PadX - Approach, PadY, Floor);
        var to = new Point3D(PadX + Approach, PadY, Floor);

        var trail = TileRoute.Trail(from, to, walker, null);
        var waypoints = TileRoute.Find(from, to, walker);

        Assert.All(
            trail.Where(tile => NavMetric.Chebyshev(tile, new Point3D(PadX, PadY, Floor)) == 1),
            tile => Assert.Contains(tile, waypoints)
        );
    }

    [Fact]
    public void TileRoute_StillReachesAGoalOnAPad()
    {
        var walker = Standable.ShunningPads(TestWalkers.Ground(Open, null), new TrapField([ExitPad]));
        var pad = new Point3D(PadX, PadY, Floor);

        var trail = TileRoute.Trail(new Point3D(PadX - Approach, PadY, Floor), pad, walker, null);

        Assert.Equal(pad, trail[^1]);
    }
}
