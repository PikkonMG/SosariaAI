using Server;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class StepTrailTests
{
    private const int StartX = 100;
    private const int StartY = 100;

    [Fact]
    public void Note_KeepsASpotEveryFewTiles()
    {
        var trail = new StepTrail();

        for (var x = 0; x <= StepTrail.SpacingTiles * 2; x++)
        {
            trail.Note(null, new Point3D(StartX + x, StartY, 0));
        }

        Assert.Equal(3, trail.Spots.Count);
        Assert.Equal(new Point3D(StartX, StartY, 0), trail.Spots[0]);
        Assert.Equal(new Point3D(StartX + StepTrail.SpacingTiles, StartY, 0), trail.Spots[1]);
    }

    [Fact]
    public void Note_DropsTheOldestPastCapacity()
    {
        var trail = new StepTrail();

        for (var i = 0; i <= StepTrail.Capacity; i++)
        {
            trail.Note(null, new Point3D(StartX + i * StepTrail.SpacingTiles, StartY, 0));
        }

        Assert.Equal(StepTrail.Capacity, trail.Spots.Count);
        Assert.Equal(new Point3D(StartX + StepTrail.SpacingTiles, StartY, 0), trail.Spots[0]);
    }

    [Fact]
    public void Note_AJumpNoWalkMakesStartsANewTrail()
    {
        var trail = new StepTrail();
        trail.Note(null, new Point3D(StartX, StartY, 0));
        trail.Note(null, new Point3D(StartX + StepTrail.SpacingTiles, StartY, 0));

        var gate = new Point3D(StartX + StepTrail.JumpTiles * 10, StartY, 0);
        trail.Note(null, gate);

        Assert.Equal(gate, Assert.Single(trail.Spots));
    }
}
