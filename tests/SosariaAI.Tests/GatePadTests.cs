using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class GatePadTests
{
    /// <summary>The height of the tile a ModernUO teleporter shows: one.</summary>
    private const int PadHeight = 1;

    private const int PadZ = 5;
    private const int RaisedFloorZ = 6;
    private static readonly Point3D Pad = new(1406, 3996, PadZ);

    // Despise, second level. Each stair is a one-way pad; its twin going the other way
    // stands a tile east. The route graph named the landing tile as a gate.
    private static readonly Point3D Landing = new(5503, 570, 51);
    private static readonly Point3D UpPad = new(5504, 570, 46);
    private static readonly Point3D UpLands = new(5574, 629, 37);
    private static readonly Point3D PlannedLanding = new(5573, 629, 42);

    [Fact]
    public void Pick_PadUnderTheCharacter_Wins()
    {
        var under = new GatePad.Pad(Landing, new Point3D(100, 100, 0), SameMap: true);
        var beside = new GatePad.Pad(UpPad, UpLands, SameMap: true);

        Assert.Equal(1, GatePad.Pick(Landing, PlannedLanding, [beside, under]));
    }

    /// <summary>
    /// The Fire stair home: three pads a tile apart land side by side. The dead one at 1415
    /// was picked first, and walkers gave the stair up though its twin carried people.
    /// </summary>
    [Fact]
    public void Pick_ALivePadGoesBeforeADeadTwin_TheDeadOneOnlyAlone()
    {
        var at = new Point3D(5791, 1416, 41);
        var planned = new Point3D(5758, 2908, 15);
        var dead = new GatePad.Pad(new Point3D(5792, 1415, 41), new Point3D(5758, 2907, 15), SameMap: true, Fires: false);
        var live = new GatePad.Pad(new Point3D(5792, 1416, 41), planned, SameMap: true);

        Assert.Equal(1, GatePad.Pick(at, planned, [dead, live]));
        Assert.Equal(0, GatePad.Pick(at, planned, [dead]));
    }

    [Fact]
    public void Pick_ADeadPadUnderfoot_DoesNotWinOverALiveTwinBeside()
    {
        var dead = new GatePad.Pad(Landing, UpLands, SameMap: true, Fires: false);
        var live = new GatePad.Pad(UpPad, UpLands, SameMap: true);

        Assert.Equal(1, GatePad.Pick(Landing, PlannedLanding, [dead, live]));
    }

    [Fact]
    public void Pick_BarePlannedTile_UsesThePadBesideThatGoesThere()
    {
        Assert.Equal(0, GatePad.Pick(Landing, PlannedLanding, [new GatePad.Pad(UpPad, UpLands, SameMap: true)]));
    }

    [Fact]
    public void Pick_PadBesideThatGoesElsewhere_IsNotUsed()
    {
        var elsewhere = new GatePad.Pad(UpPad, new Point3D(1296, 1080, 0), SameMap: true);
        var otherMap = new GatePad.Pad(UpPad, UpLands, SameMap: false);

        Assert.Equal(-1, GatePad.Pick(Landing, PlannedLanding, [elsewhere, otherMap]));
    }

    [Fact]
    public void Pick_PadTooFarOrOnAnotherFloor_IsNotUsed()
    {
        var far = new GatePad.Pad(new Point3D(Landing.X + GatePad.ReachTiles + 1, Landing.Y, Landing.Z), UpLands, SameMap: true);
        var below = new GatePad.Pad(new Point3D(UpPad.X, UpPad.Y, Landing.Z - GatePad.PadClimbZ - 1), UpLands, SameMap: true, Reachable: false);
        var underFoot = new GatePad.Pad(Landing with { Z = Landing.Z - GatePad.PadClimbZ - 1 }, UpLands, SameMap: true);

        Assert.Equal(-1, GatePad.Pick(Landing, PlannedLanding, [far, below, underFoot]));
        Assert.Equal(-1, GatePad.Pick(Landing, PlannedLanding, null));
    }

    [Fact]
    public void Pick_APadUpAStairThatAStepReaches_IsUsed_AfterOneOnTheFloor()
    {
        // The Britain sewer: the walk to the pad at (6031,1499,42) ended at the foot of its
        // stair, twenty below, and the pad was not looked at.
        var at = new Point3D(6032, 1498, 22);
        var planned = new Point3D(1491, 1640, 24);
        var upTheStair = new GatePad.Pad(new Point3D(6031, 1499, 42), planned, SameMap: true);
        var onTheFloor = new GatePad.Pad(new Point3D(6033, 1496, 22), planned, SameMap: true);

        Assert.Equal(0, GatePad.Pick(at, planned, [upTheStair]));
        Assert.Equal(1, GatePad.Pick(at, planned, [upTheStair, onTheFloor]));
        Assert.Equal(-1, GatePad.Pick(at, planned, [upTheStair with { Reachable = false }]));
    }

    [Fact]
    public void StepOffTile_SkipsAPadBeside_AndNeedsTheStepBack()
    {
        // Covetous: (2420,883) is open only east, onto the pad at (2421,883), and south.
        var pad = new Point3D(10, 10, 0);
        var padBeside = new Point3D(11, 10, 0);
        var south = new Point3D(10, 11, 0);
        var walker = TestWalkers.Ground((x, y, _) => y > pad.Y || y == pad.Y && x >= pad.X, null);

        Assert.Equal(south, GatePad.StepOffTile(walker, pad, (x, y, _) => x == padBeside.X && y == padBeside.Y));
        Assert.Equal(padBeside, GatePad.StepOffTile(walker, pad, (_, _, _) => false));
        Assert.Null(GatePad.StepOffTile(walker, pad, (x, y, _) => x != pad.X || y != pad.Y));
        Assert.Null(GatePad.StepOffTile(null, pad, (_, _, _) => false));
    }

    [Fact]
    public void IsInReach_OneTileOffThePad_IsTrue()
    {
        var pad = new Point3D(643, 2067, 5);
        var beside = new Point3D(642, 2067, 5);
        var far = new Point3D(643 + GatePad.ReachTiles + 1, 2067, 5);

        Assert.True(GatePad.IsInReach(pad, pad));
        Assert.True(GatePad.IsInReach(beside, pad));
        Assert.False(GatePad.IsInReach(far, pad));
    }

    [Fact]
    public void Pick_StairPadAtTheFootOfItsFlight_IsFound()
    {
        // (5153,808) stands at -25; the stair beside it is walked at -13, a floor apart by
        // the walk's own measure but one step onto the pad.
        var at = new Point3D(5152, 808, -13);
        var pad = new Point3D(5153, 808, -25);
        var lands = new Point3D(5134, 984, 17);

        Assert.Equal(0, GatePad.Pick(at, lands, [new GatePad.Pad(pad, lands, SameMap: true)]));
        Assert.True(GatePad.IsInReach(at, pad));
        Assert.False(GatePad.OnPadFloor(new Point3D(5152, 808, -25 + GatePad.PadClimbZ + 1), pad));
    }

    [Fact]
    public void Pick_StairUpFourTilesOffItsLanding_IsInReach()
    {
        var landing = new Point3D(6320, 23, -20);
        var pad = new Point3D(6319, 19, -20);
        var lands = new Point3D(6165, 74, 0);

        Assert.Equal(0, GatePad.Pick(landing, lands, [new GatePad.Pad(pad, lands, SameMap: true)]));
    }

    [Fact]
    public void Pick_DespiseOverlandEntrance_UsesPadThreeTilesWest()
    {
        // [go Despise Entrance] is (1298,1080). The real pad is (1296,1080).
        var at = new Point3D(1299, 1080, 0);
        var pad = new Point3D(1296, 1080, 0);
        var lands = new Point3D(5588, 630, 30);
        var planned = new Point3D(5587, 631, 30);

        Assert.Equal(0, GatePad.Pick(at, planned, [new GatePad.Pad(pad, lands, SameMap: true)]));
    }

    [Fact]
    public void Pick_APadAStepReaches_GoesBeforeOneNoStepReaches()
    {
        var at = new Point3D(2400, 199, 0);
        var noFloorTwin = new GatePad.Pad(new Point3D(2400, 198, 0), new Point3D(5754, 436, 80), SameMap: true, Reachable: false);
        var pad = new GatePad.Pad(new Point3D(2399, 198, 0), new Point3D(5753, 436, 79), SameMap: true);
        var planned = new Point3D(5753, 436, 78);

        Assert.Equal(1, GatePad.Pick(at, planned, [noFloorTwin, pad]));
        Assert.Equal(0, GatePad.Pick(at, planned, [noFloorTwin]));
    }

    [Theory]
    [InlineData(PadZ, PadZ, true)]
    [InlineData(PadZ, RaisedFloorZ, false)]
    [InlineData(PadZ, PadZ - GatePad.PadClimbZ + 1, true)]
    [InlineData(PadZ, PadZ - GatePad.PadClimbZ, false)]
    [InlineData(PadZ, PadZ - PadHeight, true)]
    public void Fires_AsTheEngineMovesOntoAnItem(int padZ, int standZ, bool fires) =>
        Assert.Equal(fires, GatePad.Fires(padZ, PadHeight, standZ));

    [Fact]
    public void NeverFires_APadUnderARaisedTile_FiresForNoStep()
    {
        // Jhelom: the pad lies at 5 and the tile's floor is 6, round a platform at 5.
        var walker = TestWalkers.From((x, y, _) => x == Pad.X && y == Pad.Y ? RaisedFloorZ : PadZ);

        Assert.True(GatePad.NeverFires(walker, Pad, PadHeight));
    }

    [Fact]
    public void NeverFires_APadLevelWithItsFloor_IsLive()
    {
        var walker = TestWalkers.From((_, _, _) => PadZ);

        Assert.False(GatePad.NeverFires(walker, Pad, PadHeight));
    }

    [Fact]
    public void NeverFires_APadNoStepReaches_IsNotJudged()
    {
        var walker = TestWalkers.From((x, y, _) => x == Pad.X && y == Pad.Y ? null : PadZ);

        Assert.False(GatePad.NeverFires(walker, Pad, PadHeight));
        Assert.False(GatePad.NeverFires((TileWalker)null, Pad, PadHeight));
    }

    [Fact]
    public void FiringZ_APadUnderARaisedTile_FiresLevelWithIt()
    {
        var walker = TestWalkers.From((x, y, _) => x == Pad.X && y == Pad.Y ? RaisedFloorZ : PadZ);
        var leveled = new Point3D(Pad.X, Pad.Y, RaisedFloorZ);

        Assert.Equal(RaisedFloorZ, GatePad.FiringZ(walker, Pad, PadHeight));
        Assert.False(GatePad.NeverFires(walker, leveled, PadHeight));
    }

    [Fact]
    public void FiringZ_IsNullForALivePad_AndForOneNoStepReaches()
    {
        Assert.Null(GatePad.FiringZ(TestWalkers.From((_, _, _) => PadZ), Pad, PadHeight));
        Assert.Null(GatePad.FiringZ(TestWalkers.From((x, y, _) => x == Pad.X && y == Pad.Y ? null : PadZ), Pad, PadHeight));
        Assert.Null(GatePad.FiringZ(null, Pad, PadHeight));
    }

    [Fact]
    public void FiringZ_IsNullWhenTheFloorLiesPastTheClimb()
    {
        const int farFloorZ = PadZ + GatePad.PadClimbZ + 2;
        var walker = TestWalkers.From((_, _, _) => farFloorZ);

        Assert.True(GatePad.NeverFires(walker, Pad, PadHeight));
        Assert.Null(GatePad.FiringZ(walker, Pad, PadHeight));
    }

    [Fact]
    public void SomeStepReaches_APadOnATileWithNoFloor_IsFalse()
    {
        var noFloor = TestWalkers.From((x, y, _) => x == Pad.X && y == Pad.Y ? null : PadZ);

        Assert.False(GatePad.SomeStepReaches(noFloor, Pad, PadHeight));
        Assert.True(GatePad.SomeStepReaches(TestWalkers.From((_, _, _) => PadZ), Pad, PadHeight));
        Assert.False(GatePad.SomeStepReaches(null, Pad, PadHeight));
    }

    [Fact]
    public void EntryStep_StraightOntoThePad_IsThePad()
    {
        var pad = new Point3D(10, 10, 0);

        Assert.Equal(pad, GatePad.EntryStep(TestWalkers.Flat, new Point3D(11, 11, 0), pad));
        Assert.Equal(pad, GatePad.EntryStep(TestWalkers.Flat, new Point3D(10, 11, 0), pad));
    }

    [Fact]
    public void EntryStep_DiagonalPastABlockedCorner_StepsRoundIt()
    {
        // The pad's east side is bare of floor, as at (2400,198): the diagonal from the
        // south-east needs that corner, so the walker steps west first, then north.
        var pad = new Point3D(10, 10, 0);
        var at = new Point3D(11, 11, 0);
        var walker = TestWalkers.Ground((x, y, _) => !(x == pad.X + 1 && y == pad.Y), null);

        Assert.False(walker.Step(at.X, at.Y, at.Z, pad.X, pad.Y, out _));
        Assert.Equal(new Point3D(pad.X, at.Y, 0), GatePad.EntryStep(walker, at, pad));
    }

    [Fact]
    public void EntryStep_NoStepOntoThePadFromHereOrBeside_IsNull()
    {
        // Only the walker's tile and the pad stand: the diagonal has no corners.
        var pad = new Point3D(10, 10, 0);
        var at = new Point3D(11, 11, 0);
        var walker = TestWalkers.Ground((x, y, _) => x == pad.X && y == pad.Y || x == at.X && y == at.Y, null);

        Assert.Null(GatePad.EntryStep(walker, at, pad));
        Assert.Null(GatePad.EntryStep(null, at, pad));
    }

    [Fact]
    public void NeverFires_AStairPadAFloorBelowTheStair_IsLive()
    {
        // (5153,808) stands at -25 with its stair walked at -13: the step lands on the pad's floor.
        const int stairZ = -13;
        const int padZ = -25;
        var pad = new Point3D(5153, 808, padZ);
        var walker = TestWalkers.From((x, y, _) => x == pad.X && y == pad.Y ? padZ : stairZ);

        Assert.False(GatePad.NeverFires(walker, pad, PadHeight));
    }
}
