using System.Linq;
using Server;
using SosariaAI.Navigation;
using Xunit;
using static SosariaAI.Tests.ColumnWorld;

namespace SosariaAI.Tests;

public class TileRouteTests
{
    private const int CorridorWest = 10;
    private const int CorridorEast = 12;
    private const int CorridorMid = 11;
    private const int RunLength = 80;

    [Fact]
    public void Find_Corridor_CentresOnTheRoad()
    {
        var path = TileRoute.Find(new Point3D(CorridorMid, 0, 0), new Point3D(CorridorMid, 40, 0), TestWalkers.Ground(Corridor, null));

        Assert.NotEmpty(path);

        foreach (var point in path)
        {
            Assert.Equal(CorridorMid, point.X);
        }
    }

    [Fact]
    public void Find_LongOpenRun_HopsAreAtMostTwelve()
    {
        var path = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(RunLength, 0, 0), TestWalkers.Ground(Open, null));

        Assert.NotEmpty(path);

        for (var i = 1; i < path.Count; i++)
        {
            Assert.True(NavMetric.Chebyshev(path[i - 1], path[i]) <= TileRoute.WaypointSpacing);
        }
    }

    [Fact]
    public void Find_WallWithNoGap_IsEmpty()
    {
        static bool Split(int x, int y, int z) => x < 10 || x > 10;

        var path = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(20, 0, 0), TestWalkers.Ground(Split, null));

        Assert.Empty(path);
    }

    [Fact]
    public void Find_DoorTile_IsUsed()
    {
        static bool Stand(int x, int y, int z) => x != 4;

        bool WithDoor(int x, int y, int z) => x != 4 || y == 0;

        var blocked = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(8, 0, 0), TestWalkers.Ground(Stand, null));
        var open = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(8, 0, 0), TestWalkers.Ground(WithDoor, null));

        Assert.Empty(blocked);
        Assert.NotEmpty(open);
    }

    [Fact]
    public void Find_BeyondMaxTrip_IsEmptyWithoutAProbe()
    {
        var probes = 0;

        bool Counting(int x, int y, int z)
        {
            probes++;
            return true;
        }

        var path = TileRoute.Find(Point3D.Zero, new Point3D(TileRoute.MaxTripTiles + 1, 0, 0), TestWalkers.Ground(Counting, null));

        Assert.Empty(path);
        Assert.Equal(0, probes);
    }

    [Fact]
    public void CellBudget_GrowsWithDistanceUpToMaxCells()
    {
        Assert.Equal(TileRoute.CellsPerTile, TileRoute.CellBudget(0));
        Assert.Equal(TileRoute.CellsPerTile * 2, TileRoute.CellBudget(2));
        Assert.True(TileRoute.CellBudget(TileRoute.MaxTripTiles) <= TileRoute.MaxCells);
        Assert.Equal(TileRoute.MaxCells, TileRoute.CellBudget(TileRoute.MaxCells));
    }

    [Fact]
    public void Find_EndsOnTheExactDestination()
    {
        // The destination must stay where it is, or an arrival with range 1 is missed.
        var destination = new Point3D(7, 0, 0);

        var path = TileRoute.Find(Point3D.Zero, destination, TestWalkers.Ground(Open, null));

        Assert.NotEmpty(path);
        Assert.Equal(destination, path[^1]);
    }

    [Fact]
    public void Find_SameCell_ReturnsTheExactDestination()
    {
        var destination = new Point3D(1, 0, 0);

        var path = TileRoute.Find(Point3D.Zero, destination, TestWalkers.Ground(Open, null));

        Assert.Equal([destination], path);
    }

    [Fact]
    public void Find_StartCellCornerIsAWallTop_WalksOutOnTheFloor()
    {
        // The Britain bank: the cell's corner tile is the top of the north wall, twenty
        // above the floor the character stands on.
        var route = TileRoute.Find(
            new Point3D(WallTopX, WallTopRow + 1, 0),
            new Point3D(WallTopX + WallTopTrip, WallTopRow + 1, 0),
            TestWalkers.Ground(WallTopStand, WallTopGround)
        );

        Assert.NotEmpty(route);
    }

    [Fact]
    public void Find_StartDeepInsideALargeBuilding_WalksOutOfIt()
    {
        var route = TileRoute.Find(
            new Point3D(HallWest, HallRow, 0),
            new Point3D(HallEast + HallTripBeyond, HallRow, 0),
            TestWalkers.Ground(Open, null),
            (x, y, z) => x <= HallEast
        );

        Assert.NotEmpty(route);
    }

    private const int HallWest = 2;
    private const int HallEast = 40;
    private const int HallRow = 6;
    private const int HallTripBeyond = 30;

    private const int WallTopRow = 10;
    private const int WallTopX = 10;
    private const int WallTopTrip = 20;
    private const int WallTopZ = 20;

    private static int WallTopGround(int x, int y) => y == WallTopRow ? WallTopZ : 0;

    private static bool WallTopStand(int x, int y, int z) => x >= 0 && y >= WallTopRow && z == WallTopGround(x, y);

    private static bool Corridor(int x, int y, int z) => x is >= CorridorWest and <= CorridorEast;

    private static bool Open(int x, int y, int z) => x >= 0 && y >= 0;

    [Fact]
    public void Find_CliffBesideTheRoad_WalksTheRoad()
    {
        // Every tile is standable, but the tiles at x >= 10 sit 30 higher, a cliff. The
        // route must stay on the low ground and never step onto the high side.
        const int cliffX = 10;
        const int cliffHeight = 30;
        static int Ground(int x, int y) => x >= cliffX ? cliffHeight : 0;

        var route = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(0, 40, 0), TestWalkers.Ground((_, _, _) => true, Ground));

        Assert.NotEmpty(route);
        Assert.All(route, p => Assert.True(p.X < cliffX, $"route climbed the cliff at {p}"));
    }

    [Fact]
    public void Find_OneTileWall_IsNotHoppedOver()
    {
        // A wall one tile thick at x = 9 sits between the grid cells at 8 and 10.
        const int wallX = 9;
        static bool Stand(int x, int y, int z) => x != wallX;

        var path = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(20, 0, 0), TestWalkers.Ground(Stand, null));

        Assert.Empty(path);
    }

    [Fact]
    public void Find_RoofOnTheWay_IsAvoided()
    {
        // A roofed block sits across the straight line. The route must go round it,
        // never through, while still ending on the goal.
        static bool Roof(int x, int y, int z) => x is >= 20 and <= 30 && y is >= -10 and <= 10;

        var path = TileRoute.Find(new Point3D(0, 0, 0), new Point3D(50, 0, 0), TestWalkers.Ground((_, _, _) => true, null), Roof);

        Assert.NotEmpty(path);
        Assert.All(path, p => Assert.False(Roof(p.X, p.Y, p.Z), $"route went under a roof at {p}"));
    }

    [Fact]
    public void Find_GoalBehindACounter_ArrivesWithinRange()
    {
        // A bank walk aimed at a tile behind the counter, or at a scattered tile in a
        // wall, had no route at all, five tiles from the banker. Arriving within the
        // bank's reach is arriving.
        static bool Counter(int x, int y, int z) => x != 10;

        var from = new Point3D(0, 0, 0);
        var goal = new Point3D(12, 0, 0);

        Assert.Empty(TileRoute.Find(from, goal, TestWalkers.Ground(Counter, null)));

        var path = TileRoute.Find(from, goal, TestWalkers.Ground(Counter, null), arrivalRange: NavLimits.BankArrivalRange);

        Assert.NotEmpty(path);
        Assert.True(NavMetric.Chebyshev(path[^1], goal) <= NavLimits.BankArrivalRange);

        foreach (var point in path)
        {
            Assert.True(Counter(point.X, point.Y, point.Z));
        }
    }

    // A house east of the stairs: its upper floor at 20 sits over open ground, and a
    // stair of four pieces on row StairRow climbs to it from the west.
    private const int UpperZ = 20;
    private const int StairWest = 4;
    private const int StairEast = 7;
    private const int StairRow = 5;
    private const int HouseWest = StairEast + 1;
    private const int RoomX = 14;

    private static readonly int[] TwoFloors = [0, UpperZ];

    private static int[] Upstairs(int x, int y)
    {
        if (y == StairRow && x is >= StairWest and <= StairEast)
        {
            return [(x - StairWest + 1) * StairRise];
        }

        return x >= HouseWest ? TwoFloors : GroundOnly;
    }

    [Fact]
    public void Find_UpperFloor_ClimbsTheStairsAndArrivesUpstairs()
    {
        var goal = new Point3D(RoomX, 0, UpperZ);

        var path = TileRoute.Find(Point3D.Zero, goal, Walker(Upstairs));

        Assert.NotEmpty(path);
        Assert.Equal(goal, path[^1]);
        Assert.Contains(path, p => p.Z is > 0 and < UpperZ);
    }

    [Fact]
    public void Find_GroundUnderTheUpperFloor_StaysOnTheGround()
    {
        var goal = new Point3D(RoomX, 0, 0);

        var path = TileRoute.Find(Point3D.Zero, goal, Walker(Upstairs));

        Assert.NotEmpty(path);
        Assert.All(path, p => Assert.Equal(0, p.Z));
    }

    [Fact]
    public void Find_EveryWaypointHeightIsTheFloorReached()
    {
        var path = TileRoute.Find(Point3D.Zero, new Point3D(RoomX, 0, UpperZ), Walker(Upstairs));
        var floors = Enumerable.Range(0, UpperZ / StairRise + 1).Select(step => step * StairRise);

        Assert.NotEmpty(path);
        Assert.All(path, p => Assert.Contains(p.Z, floors));
    }

    // A raised floor west of RaisedEdge, as in a Trinsic shop: a walker steps down off
    // it anywhere, but no step leads back up.
    private const int RaisedZ = Climb + 1;
    private const int RaisedEdge = 10;
    private const int RaisedTrip = 20;

    private static int[] RaisedFloor(int x, int y) => x < RaisedEdge ? [RaisedZ] : GroundOnly;

    [Fact]
    public void Find_RaisedFloor_StepsDownEvenFromANodeAtGroundHeight()
    {
        // The node sits at 0 though its floor is higher. The walk starts on the floor.
        var path = TileRoute.Find(new Point3D(RaisedEdge / 2, 0, 0), new Point3D(RaisedTrip, 0, 0), Walker(RaisedFloor));

        Assert.NotEmpty(path);
        Assert.Equal(0, path[^1].Z);
    }

    [Fact]
    public void Find_RaisedFloor_NoStepLeadsBackUp()
    {
        var path = TileRoute.Find(new Point3D(RaisedTrip, 0, 0), new Point3D(RaisedEdge / 2, 0, RaisedZ), Walker(RaisedFloor));

        Assert.Empty(path);
    }

    /// <summary>A shrine's heart: a block from x 9 to 13 and y 3 to 7 that no one stands in.</summary>
    private static bool ShrineBlock(int x, int y, int z) => Open(x, y, z) && !(x is >= 9 and <= 13 && y is >= 3 and <= 7);

    private static readonly Point3D ShrineHeart = new(11, 5, 0);

    /// <summary>The first ring round the heart outside the block.</summary>
    private const int ShrineRingThatStands = 3;

    [Fact]
    public void Find_GoalStandsNowhere_EndsOnTheNearestTileThatStands()
    {
        // The Spirituality shrine's arrival sits inside the shrine. A last leg aimed at it
        // stalled every ghost that walked there.
        var path = TileRoute.Find(new Point3D(0, 5, 0), ShrineHeart, TestWalkers.Ground(ShrineBlock, null));

        Assert.NotEmpty(path);
        Assert.All(path, point => Assert.True(ShrineBlock(point.X, point.Y, point.Z), $"{point} does not stand"));
        Assert.Equal(ShrineRingThatStands, NavMetric.Chebyshev(path[^1], ShrineHeart));
    }

    [Fact]
    public void Find_RangeWithNoTileThatStands_EndsOnTheNearestTileThatStands()
    {
        const int AnkhRange = 2;
        var walker = TestWalkers.Ground(ShrineBlock, null);

        var path = TileRoute.Find(new Point3D(0, 5, 0), ShrineHeart, walker, isIndoor: null, AnkhRange, out var reason);

        Assert.Null(reason);
        Assert.NotEmpty(path);
        Assert.True(ShrineBlock(path[^1].X, path[^1].Y, path[^1].Z));
        Assert.Equal(ShrineRingThatStands, NavMetric.Chebyshev(path[^1], ShrineHeart));
    }

    [Fact]
    public void Trail_StepsOneTileAtATimeToTheGoal()
    {
        var goal = new Point3D(20, 5, 0);

        var trail = TileRoute.Trail(Point3D.Zero, goal, TestWalkers.Ground(Open, null), isIndoor: null);

        Assert.Equal(goal, trail[^1]);
        Assert.Equal(NavMetric.Chebyshev(Point3D.Zero, goal), trail.Count);
        Assert.Equal(1, NavMetric.Chebyshev(Point3D.Zero, trail[0]));

        for (var i = 1; i < trail.Count; i++)
        {
            Assert.Equal(1, NavMetric.Chebyshev(trail[i - 1], trail[i]));
        }
    }

    [Fact]
    public void Trail_NoWay_IsEmpty() =>
        Assert.Empty(TileRoute.Trail(new Point3D(CorridorMid, 0, 0), new Point3D(CorridorMid, 40, 0), TestWalkers.Ground(WallAcross, null), isIndoor: null));

    private static bool WallAcross(int x, int y, int z) => Open(x, y, z) && y != 20 && x < 30;

    [Fact]
    public void Find_NullWalker_IsEmpty()
    {
        Assert.Empty(TileRoute.Find(Point3D.Zero, new Point3D(4, 0, 0), walker: null));
    }

    [Fact]
    public void FindAny_EndsOnTheNearestGoalItReaches()
    {
        // The nearer goal lies behind a wall with no gap; the farther one is open.
        static bool Walled(int x, int y, int z) => !(x == 10 && y <= 20);

        var near = new Point3D(12, 0, 0);
        var far = new Point3D(0, 30, 0);
        var path = TileRoute.FindAny(Point3D.Zero, [near, far], TestWalkers.Ground(Walled, null), null, out _);

        Assert.NotEmpty(path);
        Assert.Equal(far, path[^1]);
    }

    [Theory]
    [InlineData(RunLength)]
    [InlineData(TileRoute.MaxTripTiles - 1)]
    public void FindAndFindAny_UnreachableGoal_StopAtTheWorldCellBudget(int length)
    {
        var probes = 0;

        // Open ground wider than any budget, walled off from the goal with no gap.
        bool Counting(int x, int y, int z)
        {
            probes++;
            return x != length;
        }

        var walker = TestWalkers.Ground(Counting, null);
        var goal = new Point3D(length + 1, 0, 0);
        var budget = TileRoute.WorldCellBudget(length + 1);

        Assert.Empty(TileRoute.Find(Point3D.Zero, goal, walker, null, 0, out var why));
        Assert.Contains($"cell budget of {budget}", why);
        Assert.True(probes <= ProbesPerCell * (budget + 1), $"{probes} probes");

        probes = 0;
        Assert.Empty(TileRoute.FindAny(Point3D.Zero, [goal], walker, null, out why));
        Assert.Contains($"cell budget of {budget}", why);
        Assert.True(probes <= ProbesPerCell * (budget + 1), $"{probes} probes");
    }

    [Theory]
    [InlineData(0, TileRoute.WorldMinCells)]
    [InlineData(TileRoute.MaxTripTiles, TileRoute.MaxTripTiles * TileRoute.WorldCellsPerTile)]
    [InlineData(TileRoute.MaxTripTiles * 10, TileRoute.WorldMaxCells)]
    public void WorldCellBudget_GrowsWithTheTripBetweenItsBounds(int distance, int cells) =>
        Assert.Equal(cells, TileRoute.WorldCellBudget(distance));

    [Fact]
    public void FindForBuild_UnreachableGoal_SearchesTheBuildBudget()
    {
        static bool Split(int x, int y, int z) => x != RunLength;

        var path = TileRoute.FindForBuild(Point3D.Zero, new Point3D(RunLength + 1, 0, 0), TestWalkers.Ground(Split, null), null);

        Assert.Empty(path);
        Assert.True(TileRoute.CellBudget(RunLength + 1) > TileRoute.WorldCellBudget(RunLength + 1));
    }

    [Fact]
    public void Find_WithAnAllowance_SharesItsCellsAndStopsOnceSpent()
    {
        var probes = 0;

        bool Counting(int x, int y, int z)
        {
            probes++;
            return x != RunLength;
        }

        var walker = TestWalkers.Ground(Counting, null);
        var goal = new Point3D(RunLength + 1, 0, 0);
        var cells = TileRoute.WorldCellBudget(RunLength + 1) + TileRoute.WorldMinCells / 2;
        var allowance = new TileRoute.CellAllowance(cells);

        Assert.Empty(TileRoute.Find(Point3D.Zero, goal, walker, null, allowance));
        Assert.Equal(cells - TileRoute.WorldCellBudget(RunLength + 1), allowance.Remaining);

        Assert.Empty(TileRoute.Find(Point3D.Zero, goal, walker, null, allowance));
        Assert.True(allowance.Spent);

        probes = 0;
        Assert.Empty(TileRoute.Find(Point3D.Zero, goal, walker, null, allowance));
        Assert.Equal(0, probes);

        var open = new TileRoute.CellAllowance(TileRoute.WorldMinCells);
        Assert.NotEmpty(TileRoute.Find(Point3D.Zero, new Point3D(RunLength, 0, 0), TestWalkers.Flat, null, open));
        Assert.True(open.Remaining > 0);
    }

    [Fact]
    public void FindAny_NoGoal_IsEmpty() =>
        Assert.Empty(TileRoute.FindAny(Point3D.Zero, [], TestWalkers.Flat, null, out _));

    /// <summary>Stand probes one search cell makes at most: one per straight step, three per diagonal with its two side tiles.</summary>
    private const int ProbesPerCell = 16;

    // A long shop on the west, its counter far from its one door on the east wall, and a
    // house on the street east of it with a door on each side, square in the way.
    private const int ShopWest = 10;
    private const int ShopEast = 30;
    private const int WayHouseWest = 40;
    private const int WayHouseEast = 50;
    private const int BuildingNorth = 8;
    private const int BuildingSouth = 16;
    private const int DoorRow = 12;
    private const int StreetEnd = 70;

    private static readonly Point3D ShopCounter = new(ShopWest + 1, DoorRow, 0);
    private static readonly Point3D StreetStart = new(StreetEnd - 10, DoorRow, 0);

    private static bool InShop(int x, int y) => x is >= ShopWest and <= ShopEast && y is >= BuildingNorth and <= BuildingSouth;

    private static bool InWayHouse(int x, int y) => x is >= WayHouseWest and <= WayHouseEast && y is >= BuildingNorth and <= BuildingSouth;

    private static bool UnderRoof(int x, int y, int z) => InShop(x, y) || InWayHouse(x, y);

    private static bool WallOf(int x, int y, int west, int east) =>
        (x == west - 1 || x == east + 1) && y is >= BuildingNorth - 1 and <= BuildingSouth + 1 ||
        (y == BuildingNorth - 1 || y == BuildingSouth + 1) && x >= west - 1 && x <= east + 1;

    private static bool ShopStreet(int x, int y, int z)
    {
        if (y == DoorRow && (x == ShopEast + 1 || x == WayHouseWest - 1 || x == WayHouseEast + 1))
        {
            return true;
        }

        return x <= StreetEnd && y <= BuildingSouth + 10 && !WallOf(x, y, ShopWest, ShopEast) && !WallOf(x, y, WayHouseWest, WayHouseEast);
    }

    [Fact]
    public void Trail_CounterDeepInAShop_WalksInByTheDoor()
    {
        Assert.True(ShopEast + 1 - ShopCounter.X > TileRoute.IndoorAllowanceTiles);

        var trail = TileRoute.Trail(StreetStart, ShopCounter, TestWalkers.Ground(ShopStreet, null), UnderRoof);

        Assert.NotEmpty(trail);
        Assert.Equal(ShopCounter, trail[^1]);
    }

    [Fact]
    public void Trail_IntoAShop_DoesNotCutThroughAHouseOnTheWay()
    {
        var trail = TileRoute.Trail(StreetStart, ShopCounter, TestWalkers.Ground(ShopStreet, null), UnderRoof);

        Assert.NotEmpty(trail);
        Assert.DoesNotContain(trail, step => InWayHouse(step.X, step.Y));
    }
}
