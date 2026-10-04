using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class ArrivalHeightTests
{
    [Fact]
    public void IsRoofAbove_BritainSmithRoof()
    {
        Assert.True(ArrivalHeight.IsRoofAbove(20, 0));
        Assert.False(ArrivalHeight.IsRoofAbove(0, 0));
        Assert.False(ArrivalHeight.IsRoofAbove(ArrivalHeight.RoofRise - 1, 0));
    }

    [Fact]
    public void PreferGround_DropsARoofMarkerToTheStreet()
    {
        var roof = new Point3D(1507, 1579, 20);

        Assert.Equal(new Point3D(1507, 1579, 0), ArrivalHeight.PreferGround(roof, 0, floorAtGoal: true));
        Assert.Equal(roof, ArrivalHeight.PreferGround(roof, 20, floorAtGoal: true));
        Assert.Equal(Point3D.Zero, ArrivalHeight.PreferGround(Point3D.Zero, 0, floorAtGoal: true));
    }

    [Fact]
    public void PreferGround_HeightWithNoFloor_TakesTheGround()
    {
        // A Trinsic home written at the moongate's -20 over ground at 0.
        var sunk = new Point3D(1815, 2917, -20);

        Assert.Equal(new Point3D(1815, 2917, 0), ArrivalHeight.PreferGround(sunk, 0, floorAtGoal: false));
        Assert.Equal(sunk, ArrivalHeight.PreferGround(sunk, -20, floorAtGoal: true));
    }

    [Fact]
    public void IsRoofOverBlockedLand_TheRoofOverACounterIsNoFloor()
    {
        // Moonglow bank (4471,1156): stone at 0 under the counter, slate roof at 20.
        Assert.True(ArrivalHeight.IsRoofOverBlockedLand(20, 0, landWalkable: true));

        // A bridge deck 26 over the river is the floor there.
        Assert.False(ArrivalHeight.IsRoofOverBlockedLand(26, 0, landWalkable: false));
        Assert.False(ArrivalHeight.IsRoofOverBlockedLand(ArrivalHeight.RoofRise - 1, 0, landWalkable: true));
    }
}
