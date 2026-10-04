using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class WalkLineTests
{
    private const int WallX = 1;
    private const int FarX = 20;
    private const int SlopePerTile = 2;
    private const int CliffX = 10;
    private const int CliffHeight = 30;
    private const int UpperFloor = 20;

    [Fact]
    public void TryWalk_Slope_CarriesTheHeight()
    {
        var walker = TestWalkers.Ground(static (_, _, _) => true, static (x, _) => x * SlopePerTile);

        Assert.True(WalkLine.TryWalk(walker, 0, 0, 0, FarX, 0, null, out var z));
        Assert.Equal(FarX * SlopePerTile, z);
    }

    [Fact]
    public void TryWalk_Cliff_StopsTheWalk()
    {
        var walker = TestWalkers.Ground(static (_, _, _) => true, static (x, _) => x >= CliffX ? CliffHeight : 0);

        Assert.False(WalkLine.TryWalk(walker, 0, 0, 0, FarX, 0, null, out _));
    }

    [Fact]
    public void TryWalk_KeepTest_RejectsATile()
    {
        var walker = TestWalkers.Ground(static (_, _, _) => true, null);

        Assert.False(WalkLine.TryWalk(walker, 0, 0, 0, FarX, 0, static (x, _, _) => x != WallX, out _));
        Assert.False(WalkLine.TryWalk(null, 0, 0, 0, FarX, 0, null, out _));
    }

    [Fact]
    public void Reaches_OneTileWall_IsBlocked()
    {
        var walled = TestWalkers.Ground(static (x, _, _) => x != WallX, null);

        Assert.False(WalkLine.Reaches(walled, Point3D.Zero, new Point3D(FarX, 0, 0)));
        Assert.True(WalkLine.Reaches(TestWalkers.Flat, Point3D.Zero, new Point3D(FarX, 0, 0)));
    }

    [Fact]
    public void Reaches_EndsOnAnotherFloor_IsNotThere()
    {
        // The walk arrives under a goal that stands on the floor above.
        Assert.False(WalkLine.Reaches(TestWalkers.Flat, Point3D.Zero, new Point3D(FarX, 0, UpperFloor)));
        Assert.False(WalkLine.Reaches(TestWalkers.Flat, Point3D.Zero, new Point3D(0, 0, UpperFloor)));
        Assert.True(WalkLine.Reaches(TestWalkers.Flat, Point3D.Zero, Point3D.Zero));
    }

    [Fact]
    public void Reaches_DropOffALedge_LandsOnTheFloorBelow()
    {
        var ledge = TestWalkers.Ground(static (_, _, _) => true, static (x, _) => x < CliffX ? CliffHeight : 0);

        Assert.True(WalkLine.Reaches(ledge, new Point3D(0, 0, CliffHeight), new Point3D(FarX, 0, 0)));
        Assert.False(WalkLine.Reaches(ledge, new Point3D(FarX, 0, 0), new Point3D(0, 0, CliffHeight)));
    }

    [Fact]
    public void OutdoorKeep_RefusesRoofsOnlyBetweenTwoOutdoorSpots()
    {
        static bool Roofed(int x, int y, int z) => x == WallX;

        Assert.False(WalkLine.OutdoorKeep(fromIndoor: false, toIndoor: false, Roofed)(WallX, 0, 0));
        Assert.True(WalkLine.OutdoorKeep(fromIndoor: false, toIndoor: false, Roofed)(FarX, 0, 0));
        Assert.Null(WalkLine.OutdoorKeep(fromIndoor: true, toIndoor: false, Roofed));
        Assert.Null(WalkLine.OutdoorKeep(fromIndoor: false, toIndoor: true, Roofed));
        Assert.Null(WalkLine.OutdoorKeep(fromIndoor: false, toIndoor: false, null));
    }

    [Fact]
    public void Lerp_WalksTheLineEvenly()
    {
        Assert.Equal(0, WalkLine.Lerp(0, FarX, 0, FarX));
        Assert.Equal(FarX / 2, WalkLine.Lerp(0, FarX, FarX / 2, FarX));
        Assert.Equal(FarX, WalkLine.Lerp(0, FarX, FarX, FarX));
        Assert.Equal(WallX, WalkLine.Lerp(WallX, FarX, 0, 0));
    }
}
