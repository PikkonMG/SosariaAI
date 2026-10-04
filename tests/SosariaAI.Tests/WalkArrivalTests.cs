using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// One arrival rule for every walk. The floor counts only on a trip's final goal: a floor
/// check on every graph node left 959 walkers beside one Britain node that the engine's
/// PathFollower had already called reached.
/// </summary>
public class WalkArrivalTests
{
    private const int Range = 8;
    private const int OneTile = 1;

    /// <summary>A gap the PathFollower still calls arrived (under 16) but a floor check does not.</summary>
    private const int EngineArrivedGap = NavLimits.SameFloorMaxDeltaZ + 3;

    private static readonly Point3D Goal = new(100, 100, 0);

    [Fact]
    public void TileRouteEndRange_KeepsWhatIsLeftOfTheRange()
    {
        // The Britain inn walk: range six, the route's last tile six tiles short of the
        // keeper. Walking to that tile with the full six again stopped cooks twelve off.
        const int ShopRange = 6;
        var inn = new Point3D(1427, 1716, 20);
        var routeEnd = new Point3D(inn.X + ShopRange, inn.Y, inn.Z);
        var halfway = new Point3D(inn.X + ShopRange / 2, inn.Y, inn.Z);

        Assert.Equal(CharactersFile.DefaultGoToRange, WalkArrival.TileRouteEndRange(routeEnd, inn, ShopRange));
        Assert.Equal(ShopRange - ShopRange / 2, WalkArrival.TileRouteEndRange(halfway, inn, ShopRange));
        Assert.Equal(ShopRange, WalkArrival.TileRouteEndRange(inn, inn, ShopRange));
    }

    [Fact]
    public void TileRouteEndRange_AnExactWalkStaysExact()
    {
        const int ExactRange = 0;

        Assert.Equal(ExactRange, WalkArrival.TileRouteEndRange(Goal, Goal, ExactRange));
    }

    [Fact]
    public void Arrived_FinalGoal_RequiresSameFloor()
    {
        Assert.False(WalkArrival.Arrived(new Point3D(100, 100, 20), Goal, Range, checkFloor: true));
        Assert.True(WalkArrival.Arrived(new Point3D(102, 100, 0), Goal, Range, checkFloor: true));
        Assert.True(WalkArrival.Arrived(new Point3D(102, 100, 5), Goal, Range, checkFloor: true));
        Assert.False(WalkArrival.Arrived(new Point3D(200, 200, 0), Goal, Range, checkFloor: true));
    }

    [Fact]
    public void Arrived_GraphNode_IgnoresTheFloorLikeThePathFollower()
    {
        var beside = new Point3D(Goal.X + OneTile, Goal.Y, Goal.Z + EngineArrivedGap);

        Assert.True(WalkArrival.Arrived(beside, Goal, OneTile, checkFloor: false));
        Assert.False(WalkArrival.NeedsFloorSteps(beside, Goal, OneTile, checkFloor: false));
    }

    [Fact]
    public void NeedsFloorSteps_OnlyBesideTheFinalGoalOnAnotherFloor()
    {
        var beside = new Point3D(Goal.X + OneTile, Goal.Y, Goal.Z + EngineArrivedGap);
        var sameFloor = new Point3D(Goal.X + OneTile, Goal.Y, Goal.Z);
        var far = new Point3D(Goal.X + Range * 2, Goal.Y, Goal.Z + EngineArrivedGap);

        Assert.True(WalkArrival.NeedsFloorSteps(beside, Goal, OneTile, checkFloor: true));
        Assert.False(WalkArrival.NeedsFloorSteps(sameFloor, Goal, OneTile, checkFloor: true));
        Assert.False(WalkArrival.NeedsFloorSteps(far, Goal, OneTile, checkFloor: true));
    }
}
