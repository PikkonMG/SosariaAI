using System;
using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class DoorTilesTests
{
    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    [InlineData(Direction.North | Direction.Running)]
    public void OpensStraightAhead_AStraightStepIntoAClosedDoor(Direction direction)
    {
        Assert.True(DoorTiles.OpensStraightAhead(direction, closedDoorAhead: true));
        Assert.False(DoorTiles.OpensStraightAhead(direction, closedDoorAhead: false));
    }

    [Theory]
    [InlineData(Direction.Up)]
    [InlineData(Direction.Right)]
    [InlineData(Direction.Down)]
    [InlineData(Direction.Left)]
    public void OpensStraightAhead_NeverADiagonalIntoADoorway(Direction direction) =>
        // The Britain bank walkers cut the doorway corner at (1439,1693) and the engine refused every step.
        Assert.False(DoorTiles.OpensStraightAhead(direction, closedDoorAhead: true));

    [Fact]
    public void StepOff_TakesTheFirstClearWayRoundFromTheFacing()
    {
        Span<bool> clear = stackalloc bool[8];
        clear[(int)Direction.South] = true;
        clear[(int)Direction.West] = true;

        Assert.Equal((int)Direction.South, DoorTiles.StepOff(clear, (int)Direction.North));
        Assert.Equal((int)Direction.West, DoorTiles.StepOff(clear, (int)Direction.West));
        Assert.Equal((int)Direction.South, DoorTiles.StepOff(clear, (int)Direction.Down));
    }

    [Fact]
    public void StepOff_NoClearWay_IsNoStep()
    {
        Span<bool> clear = stackalloc bool[8];

        Assert.Equal(DoorTiles.NoStep, DoorTiles.StepOff(clear, (int)Direction.North));
    }

    [Fact]
    public void NoMap_HasNoDoorway()
    {
        Assert.False(DoorTiles.IsDoorway(null, 0, 0));
        Assert.False(DoorTiles.IsDoorway(Map.Internal, 0, 0));
        Assert.Null(DoorTiles.ClosedDoorAt(null, 0, 0, 0));
        Assert.Null(DoorTiles.ClosedDoorAt(Map.Internal, 0, 0, 0));
    }
}
