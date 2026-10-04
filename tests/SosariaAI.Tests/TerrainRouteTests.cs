using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class TerrainRouteTests
{
    private const int Far = 300;

    private static NavNode Node(string name, int x, int y) =>
        new() { Name = name, X = x, Y = y, Z = 0, Connects = [] };

    /// <summary>Ground where a corridor of open tiles joins two places, with a wall between.</summary>
    private static Func<int, int, int, bool> Corridor(int wallX) =>
        (x, y, _) => x != wallX || y == 0;

    private static NavGraph Rebuild(List<NavNode> nodes) => new("test", nodes);

    private static int Connect(
        List<NavNode> nodes,
        Func<int, int, int, bool> canStand,
        Func<int, int, int> groundZ = null
    ) =>
        TerrainRoute.ConnectComponents(nodes, TestWalkers.Ground(canStand, groundZ), canStand);

    [Fact]
    public void ConnectComponents_JoinsTwoPiecesThatTheGroundConnects()
    {
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", Far, 0) };

        var joined = Connect(nodes, static (_, _, _) => true);

        Assert.True(joined > 0);
        Assert.True(Rebuild(nodes).SameComponent("a", "b"));
    }

    [Fact]
    public void ConnectComponents_MoongateLinkDoesNotHideMissingLocalRoad()
    {
        var town = Node("town", 0, 0);
        var remoteGate = Node("moon-remote", Far, 0);
        var localGate = Node("moon-local", 600, 0);
        NavGates.Add(remoteGate, localGate, NavGateKind.Moongate);
        var nodes = new List<NavNode> { town, remoteGate, localGate };

        var joined = Connect(nodes, static (_, _, _) => true);

        Assert.True(joined > 0);
        Assert.Contains(localGate.Connects, name =>
            NavGates.KindBetween(localGate, name) == NavGateKind.None);
    }

    [Fact]
    public void ConnectComponents_IslandMoongateSeedsTheLocalWalk()
    {
        // An island pad stands in a piece of one: its only edge is the moongate jump,
        // which ground pieces ignore, and the flood cannot cross the water to reach it.
        // The pad still counts as ground a traveller can stand on, so it must seed the
        // walk that joins the town around it.
        var a = Node("a", 0, 0);
        var b = Node("b", 20, 0);
        a.Connects.Add("b");
        b.Connects.Add("a");
        var pad = Node("moon-pad", 100, 0);
        var town = Node("town", 112, 0);
        NavGates.Add(a, pad, NavGateKind.Moongate);
        var nodes = new List<NavNode> { a, b, pad, town };

        var joined = Connect(
            nodes,
            static (x, y, _) => x < 50 || (x >= 96 && Math.Abs(y) <= 24)
        );

        Assert.True(joined > 0);
        Assert.Contains(pad.Connects, name =>
            NavGates.KindBetween(pad, name) == NavGateKind.None);
        Assert.True(Rebuild(nodes).SameComponent("a", "town"));
    }

    [Fact]
    public void ConnectComponents_AsksTheStandTestAtTheGroundHeight()
    {
        // A stand test that looks near a wanted height needs the real ground height, not
        // zero. On a mountain at 60 a probe at 0 finds a wall everywhere.
        const int plateau = 60;
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", Far, 0) };
        nodes[0].Z = plateau;
        nodes[1].Z = plateau;

        var joined = Connect(
            nodes,
            static (_, _, z) => z == plateau,
            static (_, _) => plateau
        );

        Assert.True(joined > 0);
        Assert.True(Rebuild(nodes).SameComponent("a", "b"));
        Assert.All(nodes, node => Assert.Equal(plateau, node.Z));
    }

    [Fact]
    public void ConnectComponents_LaysRoadNodesAlongTheWay()
    {
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", Far, 0) };

        Connect(nodes, static (_, _, _) => true);

        var roads = nodes.Count(n => n.Name.StartsWith(TerrainRoute.RoadNamePrefix, StringComparison.Ordinal));
        Assert.True(roads > 0);
        Assert.All(nodes, n => Assert.NotEmpty(n.Name));
    }

    [Fact]
    public void ConnectComponents_RunTwice_NeverReusesARoadName()
    {
        // The repair after load walks the ground again. A reused name makes the graph
        // resolve a link to the older road node, hundreds of tiles away, and the
        // traveller then treats that long edge as a teleport.
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", Far, 0) };
        Connect(nodes, static (_, _, _) => true);
        nodes.Add(Node("c", 0, Far));

        Connect(nodes, static (_, _, _) => true);

        var names = nodes.Select(n => n.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ConnectComponents_EveryRoadEdgeIsShortEnoughForThePathfinder()
    {
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", Far, 0) };

        Connect(nodes, static (_, _, _) => true);

        var byName = nodes.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);
        var walker = TestWalkers.Ground(LShape, null);

        foreach (var node in nodes)
        {
            foreach (var other in node.Connects)
            {
                if (byName.TryGetValue(other, out var target))
                {
                    Assert.True(NavMetric.Chebyshev(node.Location, target.Location) <= NavLimits.MaxLegDistance);
                }
            }
        }
    }

    [Fact]
    public void ConnectComponents_LeavesAPieceAloneWhenNoGroundReachesIt()
    {
        // "b" sits behind a wall with no way around it, like an island.
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", 100, 40) };

        var joined = Connect(nodes, static (x, _, _) => x < 50);

        Assert.Equal(0, joined);
        Assert.False(Rebuild(nodes).SameComponent("a", "b"));
    }

    // Two shores with water between: the home shore west of HomeShoreEast, an island east
    // of IslandShoreWest.
    private const int HomeShoreEast = 50;
    private const int IslandShoreWest = 100;
    private const int IslandFar = 400;

    private static bool TwoShores(int x, int y, int z) => x < HomeShoreEast || x >= IslandShoreWest;

    [Fact]
    public void ConnectComponents_JoinsTheIslandPiecesNoTravelReaches()
    {
        // The largest piece stands on the home shore; nothing leads onto the island, as
        // nothing leads onto the Deceit or the Valor island. Its two pieces share its
        // ground and must still be one piece.
        var home = Node("home", 0, 0);
        var homeEast = Node("home-east", 20, 0);
        GraphConnect.LinkForRoad(home, homeEast);
        var nodes = new List<NavNode> { home, homeEast, Node("shrine", IslandShoreWest + 20, 0), Node("door", IslandFar, 0) };

        var joined = Connect(nodes, TwoShores);

        Assert.Equal(1, joined);
        Assert.True(RoadChecks.WalkJoined(nodes, "shrine", "door"));
        Assert.False(Rebuild(nodes).SameComponent("home", "door"));
    }

    [Fact]
    public void ConnectComponents_JoinsTwoPadPiecesOnOneFloor_ThoughTravelReachesBoth()
    {
        // A pad carries walkers from home onto the island, and a one-way pad on the island
        // sends them home. Both island pieces are travel from home, which kept the Fire
        // dungeon's exit pads apart from the floor they stand on.
        var home = Node("home", 0, 0);
        var homeEast = Node("home-east", 20, 0);
        GraphConnect.LinkForRoad(home, homeEast);
        var landing = Node("landing", IslandShoreWest + 20, 0);
        var exit = Node("exit", IslandFar, 0);
        NavGates.AddOneWay(home, landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(exit, homeEast, NavGateKind.Teleporter);
        var nodes = new List<NavNode> { home, homeEast, landing, exit };

        var joined = Connect(nodes, TwoShores);

        Assert.Equal(1, joined);
        Assert.True(RoadChecks.WalkJoined(nodes, "landing", "exit"));
        Assert.False(RoadChecks.WalkJoined(nodes, "home", "landing"));
    }

    [Fact]
    public void ConnectComponents_IslandRoadEdgesWalkBothWays()
    {
        var home = Node("home", 0, 0);
        var homeEast = Node("home-east", 20, 0);
        GraphConnect.LinkForRoad(home, homeEast);
        var nodes = new List<NavNode> { home, homeEast, Node("shrine", IslandShoreWest + 20, 0), Node("door", IslandFar, 0) };
        var walker = TestWalkers.Ground(TwoShores, null);

        TerrainRoute.ConnectComponents(nodes, walker, TwoShores);

        RoadChecks.AssertWalksBothWays(nodes, walker, TwoShores);
    }

    [Fact]
    public void ConnectComponents_TwoPiecesInOneCell_JoinByOneEdge()
    {
        // Both island nodes stand in the same cell of the walk, so only one of them starts a
        // walk there; the other joins it straight.
        var home = Node("home", 0, 0);
        var homeEast = Node("home-east", 20, 0);
        GraphConnect.LinkForRoad(home, homeEast);
        var west = Node("west", IslandShoreWest + TerrainRoute.GridStep * 2, 0);
        var east = Node("east", west.X + 1, 0);
        var nodes = new List<NavNode> { home, homeEast, west, east };

        var joined = Connect(nodes, TwoShores);

        Assert.Equal(1, joined);
        Assert.Contains(east.Name, west.Connects);
        Assert.Equal(4, nodes.Count);
    }

    [Fact]
    public void ConnectComponents_FollowsAWayAroundAnObstacle()
    {
        // A wall at x = 40 with a single gap at y = 0. A straight line cannot pass.
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", 120, 60) };

        var joined = Connect(nodes, Corridor(40));

        Assert.True(joined > 0);
        Assert.True(Rebuild(nodes).SameComponent("a", "b"));
    }

    // A town wall from x 10 to x 17 on ground from y 0 to y 12, with one gate that bends
    // between the walls every two tiles, as Cove's does. No straight four-tile walk passes.
    private const int GateWallWest = 10;
    private const int GateWallEast = 17;
    private const int GateGroundNorth = 12;

    private static readonly HashSet<(int X, int Y)> BentGate =
    [
        (10, 5), (11, 5), (11, 6), (11, 7), (12, 7), (13, 7), (13, 6), (13, 5),
        (14, 5), (15, 5), (15, 6), (15, 7), (16, 7), (17, 7)
    ];

    private static bool BentGateTown(int x, int y, int z) =>
        x >= 0 && y is >= 0 and <= GateGroundNorth &&
        (x is < GateWallWest or > GateWallEast || BentGate.Contains((x, y)));

    [Fact]
    public void ConnectComponents_ThreadsABentGateOnTiles()
    {
        var nodes = new List<NavNode>
        {
            Node("field-a", 2, 6), Node("field-b", 5, 6), Node("field-c", 8, 6),
            Node("town-a", 22, 6), Node("town-b", 26, 6)
        };
        GraphConnect.LinkForRoad(nodes[0], nodes[1]);
        GraphConnect.LinkForRoad(nodes[1], nodes[2]);
        GraphConnect.LinkForRoad(nodes[3], nodes[4]);

        var joined = Connect(nodes, BentGateTown);

        Assert.Equal(1, joined);
        Assert.True(Rebuild(nodes).SameComponent("field-a", "town-b"));

        var byName = nodes.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);
        var walker = TestWalkers.Ground(BentGateTown, null);

        foreach (var node in nodes)
        {
            foreach (var other in node.Connects)
            {
                Assert.True(
                    WalkLine.Reaches(walker, node.Location, byName[other].Location) &&
                    WalkLine.Reaches(walker, byName[other].Location, node.Location),
                    $"edge {node.Location} -> {byName[other].Location} is no straight walk both ways"
                );
            }
        }
    }

    [Fact]
    public void ConnectComponents_UsesTheGroundHeightItIsGiven()
    {
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", Far, 0) };
        const int hill = 25;

        Connect(nodes, static (_, _, _) => true, (_, _) => hill);

        var roads = nodes.Where(n => n.Name.StartsWith(TerrainRoute.RoadNamePrefix, StringComparison.Ordinal));
        Assert.All(roads, r => Assert.Equal(hill, r.Z));
    }

    [Fact]
    public void ConnectComponents_IgnoresEmptyInput()
    {
        Assert.Equal(0, Connect(null, static (_, _, _) => true));
        Assert.Equal(0, Connect([], static (_, _, _) => true));
        Assert.Equal(0, TerrainRoute.ConnectComponents([Node("a", 0, 0)], walker: null, static (_, _, _) => true));
        Assert.Equal(0, TerrainRoute.ConnectComponents([Node("a", 0, 0)], TestWalkers.Ground(static (_, _, _) => true, null), null));
    }

    private const int CliffX = 100;
    private const int CliffHeight = 30;

    [Fact]
    public void ConnectComponents_DoesNotClimbACliff()
    {
        // Every tile stands, but the ground east of the cliff sits thirty higher with no
        // slope up. The walk carries its height and cannot step up the face.
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", CliffX + Far, 0) };
        nodes[1].Z = CliffHeight;

        var joined = Connect(
            nodes,
            static (_, _, _) => true,
            static (x, _) => x >= CliffX ? CliffHeight : 0
        );

        Assert.Equal(0, joined);
    }

    // An L-shaped road: a corridor east along y = 0, then north along x = BendX.
    private const int BendX = 120;
    private const int CorridorHalfWidth = 1;
    private const int ArmLength = 120;

    private static bool LShape(int x, int y, int z) =>
        y <= CorridorHalfWidth && x <= BendX + CorridorHalfWidth ||
        Math.Abs(x - BendX) <= CorridorHalfWidth && y <= ArmLength;

    [Fact]
    public void ConnectComponents_EveryRoadEdgeIsAStraightWalk()
    {
        var nodes = new List<NavNode> { Node("a", 0, 0), Node("b", BendX, ArmLength) };

        var joined = Connect(nodes, LShape);

        Assert.True(joined > 0);
        Assert.True(Rebuild(nodes).SameComponent("a", "b"));

        var byName = nodes.ToDictionary(n => n.Name, StringComparer.OrdinalIgnoreCase);
        var walker = TestWalkers.Ground(LShape, null);

        foreach (var node in nodes)
        {
            foreach (var other in node.Connects)
            {
                var target = byName[other];
                Assert.True(
                    WalkLine.Reaches(walker, node.Location, target.Location),
                    $"edge {node.Location} -> {target.Location} crosses a wall"
                );
            }
        }
    }

    [Fact]
    public void PlanStops_StraightTrail_StopsWithinRoadSpacing()
    {
        var trail = Enumerable.Range(1, Far).Select(x => new Point3D(x, 0, 0)).ToList();

        var stops = TerrainRoute.PlanStops(Point3D.Zero, trail, null, TestWalkers.Flat, null, out var end);

        Assert.NotNull(stops);
        Assert.Equal(trail.Count - 1, end);
        var previous = Point3D.Zero;

        foreach (var at in stops.Select(i => trail[i]).Append(trail[end]))
        {
            Assert.True(NavMetric.Chebyshev(previous, at) <= TerrainRoute.RoadSpacing);
            previous = at;
        }
    }

    [Fact]
    public void PlanStops_Bend_GetsItsOwnStop()
    {
        // East along y = 0 to the bend, then north: the chord across the corner is wall.
        const int armTiles = 10;
        var trail = Enumerable.Range(1, armTiles).Select(x => new Point3D(x, 0, 0))
            .Concat(Enumerable.Range(1, armTiles).Select(y => new Point3D(armTiles, y, 0)))
            .ToList();
        static bool Corner(int x, int y, int z) => y == 0 || x == armTiles;

        var stops = TerrainRoute.PlanStops(Point3D.Zero, trail, null, TestWalkers.Ground(Corner, null), null, out var end);

        Assert.NotNull(stops);
        Assert.Single(stops);
        Assert.Equal(armTiles, trail[stops[0]].X);
        Assert.Equal(trail.Count - 1, end);
    }

    [Fact]
    public void PlanStops_MeetsAnEarlierRoad_EndsThere()
    {
        const int roadTile = 5;
        var trail = Enumerable.Range(1, Far).Select(x => new Point3D(x, 0, 0)).ToList();

        var stops = TerrainRoute.PlanStops(Point3D.Zero, trail, at => at.X == roadTile, TestWalkers.Flat, null, out var end);

        Assert.Empty(stops);
        Assert.Equal(roadTile - 1, end);
    }

    private const int LedgeZ = 10;
    private const int LedgeEdgeX = 100;
    private const int RampTiles = 4;
    private const int RampNorth = 56;
    private const int RampSouth = 71;

    /// <summary>
    /// A plateau at <see cref="LedgeZ"/> west of <see cref="LedgeEdgeX"/> with a sheer drop
    /// to open ground east of it, except where a ramp climbs back up two at a time.
    /// </summary>
    private static int LedgeGround(int x, int y)
    {
        if (x <= LedgeEdgeX)
        {
            return LedgeZ;
        }

        var intoRamp = x - LedgeEdgeX;
        return y is >= RampNorth and <= RampSouth && intoRamp <= RampTiles
            ? LedgeZ - intoRamp * TestWalkers.StepClimb
            : 0;
    }

    [Fact]
    public void ConnectComponents_DropOnTheShortWay_RoadTakesTheRampBothWays()
    {
        // The short way east steps off the ledge, and nobody climbs back up there. A road
        // laid that way only works outward, so the road must take the ramp. Laying roads
        // over one-way moves kept the Despise door off the main graph.
        var walker = TestWalkers.Ground(static (_, _, _) => true, LedgeGround);
        var home = Node("home", 0, 0);
        var homeEast = Node("home-east", 8, 0);
        home.Z = LedgeZ;
        homeEast.Z = LedgeZ;
        home.Connects.Add(homeEast.Name);
        homeEast.Connects.Add(home.Name);
        var door = Node("door", Far, 0);
        var nodes = new List<NavNode> { home, homeEast, door };

        var joined = TerrainRoute.ConnectComponents(nodes, walker, null);

        Assert.True(joined > 0);
        Assert.True(Rebuild(nodes).SameComponent("home", "door"));
        var byName = nodes.ToDictionary(n => n.Name);

        foreach (var node in nodes)
        {
            foreach (var other in node.Connects.Select(name => byName[name]))
            {
                Assert.True(
                    WalkLine.Reaches(walker, node.Location, other.Location),
                    $"edge {node.Location} -> {other.Location} does not walk"
                );
            }
        }
    }

    [Fact]
    public void PlanStops_HopThatOnlyWalksOut_IsNull()
    {
        // Off the ledge is a step down; the way back is a wall. A road edge is used both ways.
        var walker = TestWalkers.Ground(static (_, _, _) => true, LedgeGround);
        var start = new Point3D(LedgeEdgeX - 2, 0, LedgeZ);
        var trail = new List<Point3D> { new(LedgeEdgeX + 4, 0, 0) };

        Assert.True(WalkLine.Reaches(walker, start, trail[0]));
        Assert.Null(TerrainRoute.PlanStops(start, trail, null, walker, null, out _));
    }

    [Fact]
    public void PlanStops_NoClearFirstHop_IsNull()
    {
        var trail = new List<Point3D> { new(1, 0, 0), new(2, 0, 0) };

        Assert.Null(TerrainRoute.PlanStops(Point3D.Zero, trail, null, TestWalkers.Ground(static (x, _, _) => x != 1, null), null, out _));
    }
}
