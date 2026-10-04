using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using Xunit;
using static SosariaAI.Tests.ColumnWorld;

namespace SosariaAI.Tests;

public class BuildingExitTests
{
    // A small copy of The Cat's Lair: a raised floor at height 20, walled on the north
    // and west, with a ramp going down on the east side. The street lies north.
    private const int FloorZ = 20;
    private const int RoomWest = 0;
    private const int RoomEast = 10;
    private const int RoomNorth = 0;
    private const int RoomSouth = 10;
    private const int RampRow = 5;
    private const int RampLength = 5;

    private static bool InRoom(int x, int y) => x is >= RoomWest and <= RoomEast && y is >= RoomNorth and <= RoomSouth;

    private static bool OnRamp(int x, int y) => y == RampRow && x > RoomEast && x <= RoomEast + RampLength;

    private static int RampZ(int x) => FloorZ - (x - RoomEast) * FloorZ / RampLength;

    private static IReadOnlyList<int> Floors(int x, int y)
    {
        if (InRoom(x, y))
        {
            return [FloorZ];
        }

        return OnRamp(x, y) ? [RampZ(x)] : [0];
    }

    // The room edge is a wall except where the ramp leaves it. Outside, the ground is
    // open everywhere, but a character on the raised floor cannot drop 20 to reach it
    // except by the ramp, because a wall stands in the way on every other side.
    private static bool CanFit(int x, int y, int z)
    {
        var edge = x is RoomWest - 1 or RoomEast + 1 || y is RoomNorth - 1 or RoomSouth + 1;
        return !edge || OnRamp(x, y);
    }

    // The walker's step: the floor nearest the current height that it fits on, at most
    // the test world's climb up and any height down, as ColumnWorld judges a step.
    private static Func<int, int, int, int?> StepOver(Func<int, int, int, bool> canFit) =>
        (x, y, z) =>
        {
            var options = Floors(x, y);
            int? best = null;

            for (var i = 0; i < options.Count; i++)
            {
                var candidate = options[i];

                if (candidate - z > Climb || !canFit(x, y, candidate))
                {
                    continue;
                }

                if (best is not { } chosen || Math.Abs(candidate - z) < Math.Abs(chosen - z))
                {
                    best = candidate;
                }
            }

            return best;
        };

    private static readonly TileWalker Step = TestWalkers.From(StepOver(CanFit));

    private static bool IsIndoor(int x, int y, int z) => InRoom(x, y);

    private static bool AnyStreet(int x, int y, int z) => true;

    [Fact]
    public void Find_RaisedTavernFloor_LeavesByTheRamp()
    {
        var from = new Point3D(2, 2, FloorZ);

        var path = BuildingExit.Find(from, Step, IsIndoor, AnyStreet);

        Assert.NotEmpty(path);
        var exit = path[^1];
        Assert.False(IsIndoor(exit.X, exit.Y, exit.Z));
        Assert.True(exit.X > RoomEast, $"left at {exit}, not by the east ramp");
    }

    [Fact]
    public void Find_WalledCourtyard_IsNotOutYet()
    {
        // Beyond the ramp the ground is open, but only the far end of the ramp run can
        // see a street node. The walker must keep going until the planner can start.
        const int streetX = RoomEast + RampLength + 3;
        static bool SeesStreet(int x, int y, int z) => x >= streetX;

        var path = BuildingExit.Find(new Point3D(2, 2, FloorZ), Step, IsIndoor, SeesStreet);

        Assert.NotEmpty(path);
        Assert.True(path[^1].X >= streetX);
    }

    [Fact]
    public void Find_AlreadyOutside_ReturnsNothing()
    {
        var path = BuildingExit.Find(new Point3D(50, 50, 0), Step, IsIndoor, AnyStreet);

        Assert.Empty(path);
    }

    [Fact]
    public void Find_SealedRoom_ReturnsNothing()
    {
        static bool Sealed(int x, int y, int z) => InRoom(x, y);

        var path = BuildingExit.Find(new Point3D(2, 2, FloorZ), TestWalkers.From(StepOver(Sealed)), IsIndoor, AnyStreet);

        Assert.Empty(path);
    }

    [Fact]
    public void Find_WaypointsStayWithinWalkingReach()
    {
        var from = new Point3D(2, 2, FloorZ);

        var path = BuildingExit.Find(from, Step, IsIndoor, AnyStreet);

        var previous = from;

        foreach (var point in path)
        {
            Assert.True(NavMetric.Chebyshev(previous, point) <= BuildingExit.WaypointSpacing);
            previous = point;
        }
    }

    [Fact]
    public void Find_NoStepCallback_ReturnsNothing()
    {
        Assert.Empty(BuildingExit.Find(new Point3D(2, 2, FloorZ), null, IsIndoor, AnyStreet));
    }

    // A raised room with a roofless deck on its east side at the same height, and no
    // way down. Whether the deck is out is the route planner's question, not its height.
    private const int DeckEast = RoomEast + 4;

    private static int[] RoomAndDeck(int x, int y) =>
        x is >= RoomWest and <= DeckEast && y is >= RoomNorth and <= RoomSouth ? [FloorZ] : Solid;

    [Fact]
    public void Find_RooflessDeck_IsOutWhenThePlannerCanStartThere()
    {
        var from = new Point3D(2, 2, FloorZ);
        static bool FromDeck(int x, int y, int z) => x > RoomEast + BuildingExit.RoofClearance;

        var path = BuildingExit.Find(from, Walker(RoomAndDeck), IsIndoor, FromDeck);

        Assert.NotEmpty(path);
        Assert.Equal(FloorZ, path[^1].Z);
        Assert.True(path[^1].X > RoomEast);
        Assert.Empty(BuildingExit.Find(from, Walker(RoomAndDeck), IsIndoor, static (_, _, _) => false));
    }

    // A two-storey house on tiles 0 to 10. Walls stand on its border except a ground
    // floor door on the east side. The upper floor at 20 covers the ground floor, with a
    // stair well on row StairRow leading down to the west.
    private const int HouseEdge = 10;
    private const int DoorRow = 2;
    private const int StairRow = 8;
    private const int StairWest = 4;
    private const int StairEast = 6;

    private static readonly int[] TwoFloors = [0, FloorZ];

    private static bool InHouse(int x, int y) => x <= HouseEdge && y <= HouseEdge;

    private static int[] House(int x, int y)
    {
        if (!InHouse(x, y))
        {
            return GroundOnly;
        }

        if (x == HouseEdge || y == HouseEdge)
        {
            return x == HouseEdge && y == DoorRow ? GroundOnly : Solid;
        }

        if (y == StairRow && x is >= StairWest and <= StairEast)
        {
            return [(x - StairWest + 1) * StairRise];
        }

        return TwoFloors;
    }

    [Fact]
    public void Find_UpperStorey_WalksDownTheStairsAndUnderItself()
    {
        // Every ground floor tile lies under an upper floor tile walked first. Each tile
        // at each height is its own spot, so the walk down still reaches the door.
        var path = BuildingExit.Find(
            new Point3D(DoorRow, DoorRow, FloorZ),
            Walker(House),
            (x, y, _) => InHouse(x, y),
            AnyStreet
        );

        Assert.NotEmpty(path);
        Assert.True(path[^1].X > HouseEdge, $"left at {path[^1]}, not by the door");
        Assert.Equal(0, path[^1].Z);
    }
}
