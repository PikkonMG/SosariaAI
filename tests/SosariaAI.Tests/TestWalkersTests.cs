using Xunit;

namespace SosariaAI.Tests;

public class TestWalkersTests
{
    private const int Wall = 1;
    private const int HighGround = 40;

    [Fact]
    public void Ground_LandsOnTheGroundHeight()
    {
        var walker = TestWalkers.Ground(static (_, _, _) => true, static (x, _) => x);

        Assert.True(walker.Step(0, 0, 0, 1, 0, out var z));
        Assert.Equal(1, z);
        Assert.Equal(HighGround, walker.FloorNear(HighGround, 0, 0));
    }

    [Fact]
    public void Ground_ClimbsOnlyTheStepHeight_AndDropsAnyHeight()
    {
        var walker = TestWalkers.Ground(static (_, _, _) => true, static (x, _) => x == 0 ? 0 : HighGround);

        Assert.False(walker.Step(0, 0, 0, 1, 0, out _));
        Assert.True(walker.Step(1, 0, HighGround, 0, 0, out var z));
        Assert.Equal(0, z);

        var stair = TestWalkers.Ground(static (_, _, _) => true, static (x, _) => x * TestWalkers.StepClimb);

        Assert.True(stair.Step(0, 0, 0, 1, 0, out _));
    }

    [Fact]
    public void Ground_Diagonal_NeedsBothSideTiles()
    {
        // A wall tile at (1, 0) closes the corner between (0, 0) and (1, 1).
        var walker = TestWalkers.Ground(static (x, y, _) => x != Wall || y != 0, null);

        Assert.False(walker.Step(0, 0, 0, 1, 1, out _));
        Assert.True(walker.Step(0, 0, 0, 0, 1, out _));
    }
}
