using System.Collections.Generic;
using Server;
using SosariaAI.Skills;
using Xunit;
using Moves = Server.Movement.Movement;

namespace SosariaAI.Tests;

public class FloorStepsTests
{
    private const int UpperFloor = 20;
    private const int StairX = 104;
    private const int StairRiseX = 105;
    private const int StairStepZ = 10;
    private const int OneTile = 1;

    /// <summary>
    /// A balcony at z 20 east of x 104 over the ground floor at z 0: the only way up climbs
    /// x 104 to z 10, then onto the balcony. A step east from the ground floor elsewhere
    /// is refused.
    /// </summary>
    private static bool Stair(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
    {
        int? landed = fromZ >= UpperFloor
            ? toX >= StairRiseX ? UpperFloor : null
            : toX switch
            {
                StairX => StairStepZ,
                > StairX when fromZ >= StairStepZ => UpperFloor,
                > StairX => null,
                _ => 0
            };

        toZ = landed ?? fromZ;
        return landed != null;
    }

    private static bool GroundOnly(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
    {
        toZ = 0;
        return true;
    }

    [Fact]
    public void Find_ClimbsTheStairToTheGoalFloor()
    {
        var from = new Point3D(102, 100, 0);
        var goal = new Point3D(106, 100, UpperFloor);

        var steps = FloorSteps.Find(from, goal, OneTile, Stair);

        Assert.NotNull(steps);
        Assert.NotEmpty(steps);
        Assert.True(Walk(from, steps).Z >= UpperFloor);
    }

    [Fact]
    public void Find_AlreadyOnTheFloor_IsEmpty() =>
        Assert.Empty(FloorSteps.Find(new Point3D(100, 100, 0), new Point3D(100, 100, 0), OneTile, Stair));

    [Fact]
    public void Find_NoStair_IsNull() =>
        Assert.Null(FloorSteps.Find(
            new Point3D(100, 100, 0),
            new Point3D(100, 100, UpperFloor),
            OneTile,
            GroundOnly
        ));

    /// <summary>A wall along y 110 from x 60 to x 140, open only at the door x 100.</summary>
    private const int WallY = 110;
    private const int WallWest = 60;
    private const int WallEast = 140;
    private const int DoorX = 100;

    private static bool DoorWall(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
    {
        toZ = 0;
        return toY != WallY || toX is < WallWest or > WallEast || toX == DoorX;
    }

    [Fact]
    public void Around_WalksThroughTheDoorPastTheStairSearchReach()
    {
        // A home behind the one door of a long wall: the engine path plans round a shut
        // door as round the wall, and the stair search stops short of the detour.
        var from = new Point3D(80, WallY - 1, 0);
        var goal = new Point3D(80, WallY + 1, 0);

        Assert.Null(FloorSteps.Find(from, goal, 0, DoorWall));

        var steps = FloorSteps.Around(from, goal, 0, DoorWall);

        Assert.NotNull(steps);
        Assert.True(steps.Count > FloorSteps.MaxSteps);
        Assert.True(steps.Count <= FloorSteps.MaxAroundSteps);
        Assert.Equal(goal, Walk(from, steps, DoorWall));
    }

    [Fact]
    public void Around_NoWayWithinALeg_IsNull()
    {
        static bool Sealed(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
        {
            toZ = 0;
            return toY != WallY;
        }

        Assert.Null(FloorSteps.Around(new Point3D(80, WallY - 1, 0), new Point3D(80, WallY + 1, 0), 0, Sealed));
    }

    private static Point3D Walk(Point3D from, IReadOnlyList<Direction> steps) => Walk(from, steps, Stair);

    private static Point3D Walk(Point3D from, IReadOnlyList<Direction> steps, SosariaAI.Navigation.TileStep step)
    {
        var at = from;

        foreach (var direction in steps)
        {
            var x = at.X;
            var y = at.Y;
            Moves.Offset(direction, ref x, ref y);
            Assert.True(step(at.X, at.Y, at.Z, x, y, out var z), $"step {direction} from {at} is refused");
            at = new Point3D(x, y, z);
        }

        return at;
    }
}
