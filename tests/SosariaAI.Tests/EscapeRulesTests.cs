using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class EscapeRulesTests
{
    private const char Open = '.';
    private const int Lookahead = EscapeRules.KiteLookahead;

    // Water to the west (x 0 to 2), open shore and land from x 3 on: the Buccaneer's Den coast.
    private static readonly Grid Coast = new(
        "###........",
        "###........",
        "###........",
        "###........",
        "###........",
        "###........",
        "###........",
        "###........",
        "###........",
        "###........",
        "###........"
    );

    // A two-tile pocket to the west of (4,4), walled round; open ground north and south.
    private static readonly Grid Pocket = new(
        "####......",
        "####......",
        "####......",
        "####......",
        "##........",
        "####......",
        "####......",
        "####......",
        "####......"
    );

    private static readonly Grid Field = new(
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "...................."
    );

    [Fact]
    public void WayOutOfGuards_TheNearestOpenGroundByAStraightWalk()
    {
        // The guards cover x 0 to 12; open ground starts three steps east.
        var from = new Point3D(10, 7, 0);
        var way = EscapeRules.WayOutOfGuards(Field.Step, (x, _, _) => x <= 12, from, EscapeRules.GuardExitProbeTiles);

        Assert.NotNull(way);
        Assert.Equal(13, way.Value.X);
        Assert.Equal(3, NavMetric.Chebyshev(from, way.Value));
    }

    [Fact]
    public void WayOutOfGuards_NotThroughTheWater()
    {
        // On the Den coast with the guards on x 3 to 5: the water to the west is no way out.
        var from = new Point3D(4, 5, 0);
        var way = EscapeRules.WayOutOfGuards(Coast.Step, (x, _, _) => x <= 5, from, EscapeRules.GuardExitProbeTiles);

        Assert.NotNull(way);
        Assert.Equal(6, way.Value.X);
    }

    [Fact]
    public void WayOutOfGuards_NoneWithinReach()
    {
        var from = new Point3D(10, 7, 0);

        Assert.Null(EscapeRules.WayOutOfGuards(Field.Step, (x, _, _) => x <= 12, from, maxSteps: 2));
        Assert.Null(EscapeRules.WayOutOfGuards(Field.Step, (_, _, _) => true, from, EscapeRules.GuardExitProbeTiles));
    }

    [Fact]
    public void ChooseStep_OnTheCoast_TurnsAlongItInsteadOfIntoTheWater()
    {
        var from = new Point3D(3, 5, 0);
        var direction = EscapeRules.ChooseStep(Coast.Step, from, 6, 5, Lookahead);

        Assert.True(direction is (int)Direction.North or (int)Direction.South, $"chose {(Direction)direction}");
    }

    [Fact]
    public void ChooseStep_OnOpenGround_StepsAwayFromTheThreat()
    {
        var from = new Point3D(10, 7, 0);
        var direction = EscapeRules.ChooseStep(Field.Step, from, 13, 7, Lookahead);

        Assert.True(
            direction is (int)Direction.West or (int)Direction.Up or (int)Direction.Left,
            $"chose {(Direction)direction}"
        );
    }

    [Fact]
    public void ChooseStep_LeavesByOpenGroundRatherThanIntoADeadEnd()
    {
        var from = new Point3D(4, 4, 0);
        var direction = EscapeRules.ChooseStep(Pocket.Step, from, 8, 4, Lookahead);

        Assert.True(direction is (int)Direction.North or (int)Direction.South, $"chose {(Direction)direction}");
    }

    [Fact]
    public void ChooseStep_InTheBackOfADeadEnd_IsPinned()
    {
        var from = new Point3D(2, 4, 0);

        Assert.Equal(EscapeRules.NoChoice, EscapeRules.ChooseStep(Pocket.Step, from, 8, 4, Lookahead));
    }

    [Fact]
    public void Probe_WalksUntilBlockedAndCountsTheWaysOn()
    {
        var west = EscapeRules.Probe(Pocket.Step, 4, 4, 0, (int)Direction.West, EscapeRules.LegTiles);

        Assert.Equal(2, west.Reach);
        Assert.Equal(new Point3D(2, 4, 0), west.At);
        Assert.Equal(1, west.Exits);
        Assert.True(EscapeRules.IsDeadEnd(west.Reach, EscapeRules.LegTiles, west.Exits));

        var north = EscapeRules.Probe(Pocket.Step, 4, 4, 0, (int)Direction.North, EscapeRules.LegTiles);

        Assert.Equal(4, north.Reach);
        Assert.Equal(new Point3D(4, 0, 0), north.At);
    }

    [Fact]
    public void GoalScore_TooLittleGainIsNoOption() =>
        Assert.Equal(int.MinValue, EscapeRules.GoalScore(EscapeRules.MinGainTiles - 1, 3, 8, known: true, towardSafety: true));

    [Fact]
    public void PickGoal_KnownGroundBeatsTheSameGainUnwalked()
    {
        var from = new Point3D(10, 10, 0);
        var options = new List<EscapeOption>
        {
            new(new Point3D(2, 10, 0), Exits: 8, Known: false),
            new(new Point3D(2, 12, 0), Exits: 8, Known: true)
        };

        Assert.Equal(1, EscapeRules.PickGoal(options, from, 13, 10, safety: null));
    }

    [Fact]
    public void PickGoal_OpenGroundBeatsADeadEnd()
    {
        var from = new Point3D(10, 10, 0);
        var options = new List<EscapeOption>
        {
            new(new Point3D(2, 10, 0), Exits: 1, Known: true),
            new(new Point3D(2, 12, 0), Exits: 6, Known: true)
        };

        Assert.Equal(1, EscapeRules.PickGoal(options, from, 13, 10, safety: null));
    }

    [Fact]
    public void PickGoal_LeansTowardSafety()
    {
        var from = new Point3D(10, 10, 0);
        var options = new List<EscapeOption>
        {
            new(new Point3D(10, 2, 0), Exits: 8, Known: false),
            new(new Point3D(10, 18, 0), Exits: 8, Known: false)
        };

        Assert.Equal(1, EscapeRules.PickGoal(options, from, 10, 10, safety: new Point3D(10, 40, 0)));
    }

    [Fact]
    public void PickGoal_NothingGainsGround_NoChoice()
    {
        var from = new Point3D(10, 10, 0);
        var options = new List<EscapeOption> { new(new Point3D(12, 10, 0), Exits: 8, Known: true) };

        Assert.Equal(EscapeRules.NoChoice, EscapeRules.PickGoal(options, from, 13, 10, safety: null));
    }

    [Fact]
    public void Stalled_AGoalNextToOneThatStalledIsTheSameGoal()
    {
        var stalled = new List<Point3D> { new(5, 5, 0) };

        Assert.True(EscapeRules.Stalled(new Point3D(6, 6, 0), stalled));
        Assert.False(EscapeRules.Stalled(new Point3D(8, 5, 0), stalled));
    }

    /// <summary>A map of open ('.') and blocked tiles; one step goes onto any open tile.</summary>
    private sealed class Grid(params string[] rows)
    {
        public bool Step(int fromX, int fromY, int fromZ, int toX, int toY, out int toZ)
        {
            toZ = fromZ;
            return toY >= 0 && toY < rows.Length && toX >= 0 && toX < rows[toY].Length && rows[toY][toX] == Open;
        }
    }
}
