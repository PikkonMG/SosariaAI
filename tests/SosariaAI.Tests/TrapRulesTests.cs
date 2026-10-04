using System;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;
using Moves = Server.Movement.Movement;

namespace SosariaAI.Tests;

public class TrapRulesTests
{
    private const int Floor = 0;
    private const int TrapX = 10;
    private const int TrapY = 10;
    private const int OneTile = 1;
    private const int FarAbove = 30;
    private const int CorridorY = 5;
    private const int CorridorLength = 20;
    private const int Facing = 2;
    private const int ClearSide = 5;
    private const int HurtSide = 6;
    private const int DirectionCount = 8;
    private const int OpenRun = 20;
    private const int Approach = 3;
    private const int TwoTiles = 2;
    private const int HurtingTraps = 2;

    private static readonly TrapZone SpikeAt = new(TrapX, TrapY, Floor, TrapRules.OnTileReach);

    private static bool Open(int x, int y, int z) => true;

    /// <summary>A one-tile-wide corridor along <see cref="CorridorY"/>.</summary>
    private static bool Corridor(int x, int y, int z) => y == CorridorY;

    private static TileWalker Trapped(Func<int, int, int, bool> canStand, params TrapZone[] traps) =>
        TestWalkers.Ground(canStand, null) with { Harms = new TrapField(traps).Harms };

    [Fact]
    public void ReachesHeight_FollowsTheEngineTrapRange()
    {
        Assert.True(TrapRules.ReachesHeight(Floor, Floor));
        Assert.True(TrapRules.ReachesHeight(Floor, TrapRules.ReachAbove));
        Assert.False(TrapRules.ReachesHeight(Floor, TrapRules.ReachAbove + OneTile));
        Assert.False(TrapRules.ReachesHeight(Floor, -TrapRules.ReachBelow));
        Assert.True(TrapRules.ReachesHeight(Floor, -TrapRules.ReachBelow + OneTile));
    }

    [Fact]
    public void MayEnter_NeverFromSafeOntoHurt_AlwaysOffAHurtTile()
    {
        Assert.True(TrapRules.MayEnter(fromHarmed: false, toHarmed: false));
        Assert.False(TrapRules.MayEnter(fromHarmed: false, toHarmed: true));
        Assert.True(TrapRules.MayEnter(fromHarmed: true, toHarmed: true));
        Assert.True(TrapRules.MayEnter(fromHarmed: true, toHarmed: false));
    }

    [Fact]
    public void ShouldStepOff_StandingOnOrIdleBeside_NotWhileWalkingOrFightingBeside()
    {
        Assert.True(TrapRules.ShouldStepOff(walking: false, fighting: false, onHarmed: true, besideTrap: false));
        Assert.True(TrapRules.ShouldStepOff(walking: false, fighting: true, onHarmed: true, besideTrap: true));
        Assert.True(TrapRules.ShouldStepOff(walking: false, fighting: false, onHarmed: false, besideTrap: true));
        Assert.False(TrapRules.ShouldStepOff(walking: false, fighting: true, onHarmed: false, besideTrap: true));
        Assert.False(TrapRules.ShouldStepOff(walking: true, fighting: false, onHarmed: true, besideTrap: true));
        Assert.False(TrapRules.ShouldStepOff(walking: false, fighting: false, onHarmed: false, besideTrap: false));
    }

    [Fact]
    public void StepOff_PrefersClearTile_ThenAnyUnhurtTile_ElseNone()
    {
        var options = new TrapStepOption[DirectionCount];
        options[Facing] = new TrapStepOption(CanStep: true, Harmed: false, BesideTrap: true);
        options[ClearSide] = new TrapStepOption(CanStep: true, Harmed: false, BesideTrap: false);
        options[HurtSide] = new TrapStepOption(CanStep: true, Harmed: true, BesideTrap: true);

        Assert.Equal(ClearSide, TrapRules.StepOff(options, Facing));

        options[ClearSide] = default;
        Assert.Equal(Facing, TrapRules.StepOff(options, Facing));

        options[Facing] = default;
        Assert.Equal(TrapRules.NoStep, TrapRules.StepOff(options, Facing));
    }

    [Fact]
    public void TrapField_HurtsItsReach_AtItsHeight()
    {
        var field = new TrapField([
            SpikeAt,
            new TrapZone(TrapX + OpenRun, TrapY, Floor, TrapRules.MushroomReach),
            new TrapZone(TrapX, TrapY + OpenRun, Floor, TrapRules.Harmless)
        ]);

        Assert.Equal(HurtingTraps, field.Count);
        Assert.True(field.Harms(TrapX, TrapY, Floor));
        Assert.False(field.Harms(TrapX + OneTile, TrapY, Floor));
        Assert.False(field.Harms(TrapX, TrapY, FarAbove));
        Assert.True(field.Harms(TrapX + OpenRun + TrapRules.MushroomReach, TrapY - TrapRules.MushroomReach, Floor));
        Assert.False(field.Harms(TrapX + OpenRun + TrapRules.MushroomReach + OneTile, TrapY, Floor));
        Assert.False(field.Harms(TrapX, TrapY + OpenRun, Floor));
        Assert.True(field.BesideTrap(TrapX + OneTile, TrapY + OneTile, Floor));
        Assert.False(field.BesideTrap(TrapX + TwoTiles, TrapY, Floor));
        Assert.True(field.AnyNear(new Point3D(0, TrapY, Floor), new Point3D(TrapX - TwoTiles, TrapY, Floor), 0));
        Assert.False(field.AnyNear(new Point3D(0, 0, Floor), new Point3D(TwoTiles, TwoTiles, Floor), 0));
    }

    [Fact]
    public void SafeStep_RefusesOnlyTheStepOntoAHurtTile()
    {
        var walker = Trapped(Open, SpikeAt);

        Assert.True(walker.Step(TrapX - OneTile, TrapY, Floor, TrapX, TrapY, out _));
        Assert.False(walker.SafeStep(TrapX - OneTile, TrapY, Floor, TrapX, TrapY, out _));
        Assert.True(walker.SafeStep(TrapX, TrapY, Floor, TrapX + OneTile, TrapY, out _));
        Assert.Null(walker.SafeFloorNear(TrapX, TrapY, Floor));
        Assert.Equal(Floor, walker.SafeFloorNear(TrapX + OneTile, TrapY, Floor));
    }

    [Fact]
    public void WalkLine_StopsAtATrap()
    {
        var from = new Point3D(TrapX - Approach, TrapY, Floor);
        var to = new Point3D(TrapX + Approach, TrapY, Floor);

        Assert.True(WalkLine.Reaches(TestWalkers.Flat, from, to));
        Assert.False(WalkLine.Reaches(Trapped(Open, SpikeAt), from, to));
    }

    [Fact]
    public void TileRoute_GoesRoundATrapOnOpenGround()
    {
        var from = new Point3D(TrapX - Approach, TrapY, Floor);
        var to = new Point3D(TrapX + Approach, TrapY, Floor);
        var walker = Trapped(Open, SpikeAt);

        var trail = TileRoute.Trail(from, to, walker, null);

        Assert.NotEmpty(trail);
        Assert.DoesNotContain(trail, tile => walker.IsHarmed(tile.X, tile.Y, tile.Z));
    }

    [Fact]
    public void TileRoute_CrossesACorridorTrappedWallToWall()
    {
        var trap = new TrapZone(CorridorLength / 2, CorridorY, Floor, TrapRules.OnTileReach);
        var walker = Trapped(Corridor, trap);

        var trail = TileRoute.Trail(new Point3D(0, CorridorY, Floor), new Point3D(CorridorLength, CorridorY, Floor), walker, null);

        Assert.Contains(trail, tile => walker.IsHarmed(tile.X, tile.Y, tile.Z));
        Assert.Equal(new Point3D(CorridorLength, CorridorY, Floor), trail[^1]);
    }

    [Fact]
    public void FloorStepsAround_WithTheSafeStep_WalksRoundTheTrap()
    {
        var walker = Trapped(Open, SpikeAt);
        var from = new Point3D(TrapX - Approach, TrapY, Floor);
        var goal = new Point3D(TrapX + Approach, TrapY, Floor);

        var steps = FloorSteps.Around(from, goal, 0, walker.SafeStep);

        Assert.NotNull(steps);
        var at = from;

        foreach (var direction in steps)
        {
            var x = at.X;
            var y = at.Y;
            Moves.Offset(direction, ref x, ref y);
            at = new Point3D(x, y, Floor);
            Assert.False(walker.IsHarmed(at.X, at.Y, at.Z), $"stepped on the trap at {at}");
        }

        Assert.Equal(goal, at);
    }

    [Fact]
    public void FloorStepsAround_TrappedCorridor_FindsNoSafeWay()
    {
        var trap = new TrapZone(CorridorLength / 2, CorridorY, Floor, TrapRules.OnTileReach);
        var walker = Trapped(Corridor, trap);

        var steps = FloorSteps.Around(new Point3D(0, CorridorY, Floor), new Point3D(CorridorLength, CorridorY, Floor), 0, walker.SafeStep);

        Assert.Null(steps);
    }
}
