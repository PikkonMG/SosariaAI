using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class TravelerTests
{
    [Theory]
    [InlineData(Traveler.WhyNoPath, true)]
    [InlineData(Traveler.WhyGoalApart, true)]
    [InlineData(Traveler.WhyNoStart, true)]
    [InlineData(Traveler.WhyFirstHopsBlocked, true)]
    [InlineData(Traveler.WhyNoGoalNode, false)]
    [InlineData(Traveler.WhyRecentFailure, false)]
    [InlineData(Traveler.WhySearchRefused, false)]
    [InlineData(Traveler.WhyNoGraph, false)]
    [InlineData(null, false)]
    public void IsNoRoad_OnlyASearchThatFoundNoWayFromHere(string why, bool noRoad) =>
        Assert.Equal(noRoad, Traveler.IsNoRoad(why));

    [Fact]
    public void PlanNames_FromNearFirstNode_ToLastNode_WalksEveryNode()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0, "C"),
                Node("C", 20, 0, 0)
            ]
        );

        var names = Plan(graph, new Point3D(1, 0, 0), "C", Open);
        Assert.Equal(["A", "B", "C"], names);
    }

    [Fact]
    public void PlanNames_WallHop_IsRejectedByTheWalk()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("inside", 5, 10, 20, "north", "south"),
                Node("north", 5, 6, 0, "inside", "bank"),
                Node("south", 5, 14, 20, "inside"),
                Node("bank", 5, 0, 0, "north")
            ]
        );
        var from = new Point3D(5, 10, 20);
        bool CanStand(int x, int y, int z) => y != 8;

        var path = Plan(graph, from, "south", CanStand);

        Assert.Contains("south", path);
        Assert.DoesNotContain("north", path);
    }

    [Fact]
    public void PlanNames_TrustsGeneratedEdges()
    {
        // The world generator walks every edge, so a node-to-node hop this walker
        // refuses is no reason to reject a route: only the walker's own hop is proved.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("gate", 0, 0, 0, "ledge"),
                Node("ledge", 20, 0, 20, "gate", "town"),
                Node("town", 30, 0, 20, "ledge")
            ]
        );

        var path = Plan(graph, new Point3D(0, 0, 0), "town", static (x, _, _) => x < 10);

        Assert.Equal(["gate", "ledge", "town"], path);
    }

    [Fact]
    public void PlanNames_WalkersOwnHopBlocked_SaysSo()
    {
        // A wall band from x 10 to 15 cuts the walker off from the only start node.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("start", 20, 0, 0, "goal"),
                Node("goal", 30, 0, 0)
            ]
        );

        var path = Traveler.PlanNames(
            graph,
            new Point3D(0, 0, 0),
            "goal",
            TestWalkers.Ground(WallBand, groundZ: null),
            isIndoor: null,
            avoid: null,
            out var whyNone
        );

        Assert.Empty(path);
        Assert.Equal(Traveler.WhyFirstHopsBlocked, whyNone);
    }

    [Fact]
    public void PlanNames_WalkersLineBlocked_TileRouteRoundTheWallStillStarts()
    {
        // A short wall across the straight walk to the start node: the tile router
        // walks round its end, so the start is in reach after all.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("start", 20, 0, 0, "goal"),
                Node("goal", 30, 0, 0)
            ]
        );

        var path = Plan(graph, new Point3D(0, 0, 0), "goal", static (x, y, _) => x != 10 || y is < -3 or > 3);

        Assert.Equal(["start", "goal"], path);
    }

    [Fact]
    public void PreferredGoal_NearestStartInAScrap_StillFindsTheGoal()
    {
        // The Britain bank tile: the single nearest node is a two-node scrap, while the
        // town's main piece is only a few tiles further.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("scrap-a", 1, 0, 0, "scrap-b"),
                Node("scrap-b", 2, 0, 0),
                Node("street", 5, 0, 0, "far"),
                Node("far", NavLimits.MaxLegDistance * 2, 0, 0)
            ]
        );

        Assert.Equal("far", Traveler.PreferredGoal(graph, Point3D.Zero, new Point3D(NavLimits.MaxLegDistance * 2, 0, 0))?.Name);
    }

    [Fact]
    public void PreferredGoal_GoalInAnotherPiece_IsRefused()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("start", 1, 0, 0),
                Node("island", NavLimits.MaxLegDistance * 2, 0, 0)
            ]
        );

        Assert.Null(Traveler.PreferredGoal(graph, Point3D.Zero, new Point3D(NavLimits.MaxLegDistance * 2, 0, 0)));
    }

    [Fact]
    public void WhyNoGoal_NodesNearTheGoalInAnotherPiece_SaysThePiecesAreApart()
    {
        // Cove's homes had nodes two tiles away in a piece of their own, and the log
        // said the goal had no node at all.
        var goal = new Point3D(NavLimits.MaxLegDistance * 2, 0, 0);
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("start", 1, 0, 0),
                Node("island", goal.X + 2, 0, 0)
            ]
        );

        Assert.Null(Traveler.PreferredGoal(graph, Point3D.Zero, goal));
        Assert.Equal(Traveler.WhyGoalApart, Traveler.WhyNoGoal(graph, goal));
        Assert.Equal(Traveler.WhyNoGoalNode, Traveler.WhyNoGoal(graph, new Point3D(NavLimits.MaxLegDistance * 4, 0, 0)));
    }

    [Fact]
    public void GoalApart_AShopOnASealedIsland_IsApart()
    {
        // The south Jhelom island: its shops have nodes, in a piece no road from town reaches.
        var shop = new Point3D(NavLimits.MaxLegDistance * 2, 0, 0);
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("town", 1, 0, 0),
                Node("island", shop.X + 2, 0, 0)
            ]
        );

        Assert.True(Traveler.GoalApart(graph, Point3D.Zero, shop));
    }

    [Fact]
    public void GoalApart_AShopOnTheWalkersRoads_IsNotApart()
    {
        var shop = new Point3D(NavLimits.MaxLegDistance * 2, 0, 0);
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("town", 1, 0, 0, "shop"),
                Node("shop", shop.X + 2, 0, 0)
            ]
        );

        Assert.False(Traveler.GoalApart(graph, Point3D.Zero, shop));
    }

    [Fact]
    public void GoalApart_AGoalWithNoNodeOrNoGraph_IsNotApart()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("town", 1, 0, 0)]);

        Assert.False(Traveler.GoalApart(graph, Point3D.Zero, new Point3D(NavLimits.MaxLegDistance * 4, 0, 0)));
        Assert.False(Traveler.GoalApart(null, Point3D.Zero, new Point3D(1, 0, 0)));
    }

    [Fact]
    public void PlanNames_EveryNodeBeyondOneLeg_ReturnsEmpty()
    {
        // The first leg runs from the character to a node and has the same cap as any
        // other leg. A start the pathfinder cannot reach is no start.
        // Each search gets its own graph: a failed one cools its 16-tile cell down.
        static NavGraph Graph() =>
            new(
                FacetNames.Felucca,
                [
                    Node("A", NavLimits.MaxLegDistance + 1, 0, 0, "B"),
                    Node("B", NavLimits.MaxLegDistance + 11, 0, 0)
                ]
            );

        Assert.Empty(Traveler.PlanNames(Graph(), Point3D.Zero, "B", TestWalkers.Flat, null, null, out var whyNone));
        Assert.Equal(Traveler.WhyNoStart, whyNone);
        Assert.NotEmpty(Plan(Graph(), new Point3D(1, 0, 0), "B", Open));
    }

    [Fact]
    public void PlanNames_IndoorFrom_CanStartFromIndoorNode()
    {
        // The traveller stands inside a room off the doorway column: every outdoor
        // node's straight walk meets the wall, only same-room nodes are reached.
        // Refusing indoor starts strands them; allowing them finds the door edges.
        var names = Plan(IndoorGraph(), IndoorFrom, "street", Stand, Indoor);

        // The search keeps indoor nodes out of the middle of a path, so the
        // traveller binds the indoor door node itself as the first step out.
        Assert.Equal("door", names[0]);
        Assert.Equal("street", names[^1]);
    }

    [Fact]
    public void PlanNames_OutdoorFrom_StillSkipsIndoorStart()
    {
        // The relaxation only applies to an indoor traveller: an outdoor position
        // nearer the door node than the street must not bind through the wall.
        var names = Plan(IndoorGraph(), new Point3D(10, 19, 0), "street", Stand, Indoor);

        Assert.Equal("mid", names[0]);
    }

    [Fact]
    public void NearestRoutableStart_SealedNearestNode_PicksOpenOne()
    {
        // The yard node is in the main component through a wall edge the walk
        // cannot take. Only the tile-route proof tells it from the open street.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("yard", 6, 0, 0, "street"),
                Node("street", 20, 0, 0, "yard", "road"),
                Node("road", 30, 0, 0, "street")
            ]
        );

        static bool OpenButWall(int x, int y, int z) => x != 10;

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(6, 0, 0),
            new Point3D(30, 0, 0),
            OpenButWall,
            walker: TestWalkers.Ground(OpenButWall, null),
            isIndoor: null
        );

        Assert.Equal("street", node?.Name);
    }

    // A flat roof at height 20 over tiles 2 to 6, ringed by a parapet no one fits past,
    // with no stair: nothing leads off it.
    private const int RoofZ = 20;
    private const int RoofWest = 2;
    private const int RoofEast = 6;
    private static readonly int[] RoofOnly = [RoofZ];

    private static int[] RoofWithNoWayDown(int x, int y)
    {
        if (x is >= RoofWest and <= RoofEast && y <= RoofEast)
        {
            return RoofOnly;
        }

        return x is >= RoofWest - 1 and <= RoofEast + 1 && y <= RoofEast + 1 ? ColumnWorld.Solid : ColumnWorld.GroundOnly;
    }

    [Fact]
    public void NearestRoutableStart_RoofWithNoWayDown_IsSkipped()
    {
        // A roof node is open air — not indoor — but placing them there strands them on
        // the roof. The walk from it finds no way down, so the street wins.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("roof", 4, 0, RoofZ, "street"),
                Node("street", 12, 0, 0, "roof")
            ]
        );
        var walker = ColumnWorld.Walker(RoofWithNoWayDown);

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(5, 0, RoofZ),
            new Point3D(12, 0, 0),
            NodeHeight.Stands(walker.FloorNear),
            walker,
            isIndoor: null
        );

        Assert.Equal("street", node?.Name);
    }

    [Fact]
    public void NearestRoutableStart_EverythingSealed_ReturnsNull()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("yard-a", 6, 0, 0, "yard-b"),
                Node("yard-b", 8, 0, 0, "yard-a")
            ]
        );

        static bool OpenButWall(int x, int y, int z) => x != 10;

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(6, 0, 0),
            new Point3D(30, 0, 0),
            OpenButWall,
            walker: TestWalkers.Ground(OpenButWall, null),
            isIndoor: null
        );

        Assert.Null(node);
    }

    [Fact]
    public void NearestRoutableStart_FilterRejectsNearest_PicksNext()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("bank", 6, 0, 0, "street"),
                Node("street", 20, 0, 0, "bank", "road"),
                Node("road", 30, 0, 0, "street")
            ]
        );

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(6, 0, 0),
            new Point3D(30, 0, 0),
            (_, _, _) => true,
            walker: TestWalkers.Flat,
            isIndoor: null,
            nodeFilter: n => n.Name != "bank"
        );

        Assert.Equal("street", node?.Name);
    }

    [Fact]
    public void NearestRoutableStart_FilterRejectsAll_ReturnsNull()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a", 6, 0, 0, "b"),
                Node("b", 8, 0, 0, "a")
            ]
        );

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(6, 0, 0),
            new Point3D(30, 0, 0),
            (_, _, _) => true,
            walker: TestWalkers.Flat,
            isIndoor: null,
            nodeFilter: _ => false
        );

        Assert.Null(node);
    }

    [Fact]
    public void PlanNames_NullGraphOrEmptyDestination_ReturnsEmpty()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0, 0)]);

        Assert.Empty(Plan(null, new Point3D(0, 0, 0), "A", Open));
        Assert.Empty(Plan(graph, new Point3D(0, 0, 0), "", Open));
    }

    [Fact]
    public void HasClearStart_NodeAboveWalker_IsNotAStart()
    {
        // The wall-top node one tile away stands a storey up. The walk to it ends on
        // the ground under it, so it cannot begin a route. The sealed yard below must
        // not read as a clear start.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("walltop", 11, 10, 20, "road"),
                Node("road", 40, 10, 0, "walltop")
            ]
        );

        static bool Stand(int x, int y, int z) => !(x == 20 && y == 10);

        Assert.False(Traveler.HasClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Ground(Stand, null)));
    }

    [Fact]
    public void HasClearStart_NodeBelowWalker_CanBeAStart()
    {
        // Dropping off a ledge is legal movement, so a node below the walker is a
        // real start. Only a climb the step cannot make is refused.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("road", 11, 10, 0)
            ]
        );

        Assert.True(Traveler.HasClearStart(graph, new Point3D(10, 10, 20), TestWalkers.Flat));
    }

    private static readonly int[] WallTopOnly = [20];

    [Fact]
    public void PlanNames_StartAboveWalker_IsSkipped()
    {
        // walltop is the nearer candidate once its floor penalty is paid, so the
        // planner binds it first unless the walk to it is refused.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("walltop", 1, 0, 20, "goal"),
                Node("street", 33, 0, 0, "goal"),
                Node("goal", 60, 0, 0)
            ]
        );

        // The wall top is a floor of its own a full storey over the ground beside it.
        var walker = ColumnWorld.Walker(static (x, y) => x == 1 && y == 0 ? WallTopOnly : ColumnWorld.GroundOnly);

        var names = Traveler.PlanNames(graph, Point3D.Zero, "goal", walker, isIndoor: null, avoid: null, out _);

        Assert.Equal("street", names[0]);
    }

    [Fact]
    public void HasClearStart_SameRoomNode_DoesNotCountAsClear()
    {
        // The bank node is a straight walk away inside the sealed room, so the old
        // "any node reachable" check passed while every plan out failed. Only a
        // formed route to a probe outside the pocket counts as clear.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("room", 10, 10, 0, "street"),
                Node("street", 60, 10, 0, "room")
            ]
        );

        static bool Stand(int x, int y, int z) => x != 20;

        Assert.False(Traveler.HasClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Ground(Stand, null)));
    }

    [Fact]
    public void HasClearStart_PlanToProbe_IsAStart()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a", 10, 10, 0, "b"),
                Node("b", 60, 10, 0, "a")
            ]
        );

        Assert.True(Traveler.HasClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Flat));
    }

    [Fact]
    public void HasClearStart_NoProbe_FallsBackToTheStraightWalk()
    {
        // A sparse area has no far node to plan against, so the straight walk
        // decides: a straight walk to a nearby node is a clear start.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("near", 12, 10, 0)
            ]
        );

        Assert.True(Traveler.HasClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Flat));
    }

    [Fact]
    public void NearestRoutableStart_FarAnchor_UsesEscapeProbe()
    {
        // Home sits across the map: a tile walk cannot prove the spot reaches it,
        // so a qualifying node outside the yard stands in as the escape probe.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("yard", 6, 0, 0, "street"),
                Node("street", 20, 0, 0, "yard", "far"),
                Node("far", 60, 0, 0, "street")
            ]
        );

        static bool OpenButWall(int x, int y, int z) => x != 10;

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(6, 0, 0),
            new Point3D(900, 0, 0),
            OpenButWall,
            walker: TestWalkers.Ground(OpenButWall, null),
            isIndoor: null
        );

        Assert.Equal("street", node?.Name);
    }

    [Fact]
    public void NearestRoutableStart_DenseScanHidesProbe_FindsComponentProbe()
    {
        // The live First Bank of Moonglow fault: all 48 nodes nearest the sealed
        // spawn sat inside the probe radius, so the scan offered no escape probe
        // and the anchor — inside the same pocket — was the only proof target.
        var nodes = new List<NavNode> { Node("inside", 10, 10, 0, "near-0") };

        // 49 nearby nodes fill the rescue scan and push the far probe out of it.
        for (var i = 0; i < 49; i++)
        {
            nodes.Add(Node($"near-{i}", 20 + (i % 7) * 4, 4 + (i / 7) * 4, 0, "far"));
        }

        nodes.Add(Node("far", 70, 10, 0, "inside", "near-0"));

        var graph = new NavGraph(FacetNames.Felucca, nodes);

        // A sealed 9x9 yard around (10,10): the ring is unwalkable, inside and
        // outside are open.
        static bool SealedBox(int x, int y, int z) =>
            x is not (>= 6 and <= 14) || y is not (>= 6 and <= 14) ||
            (x != 6 && x != 14 && y != 6 && y != 14);

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(10, 10, 0),
            new Point3D(10, 10, 0),
            SealedBox,
            walker: TestWalkers.Ground(SealedBox, null),
            isIndoor: null
        );

        Assert.NotNull(node);
        Assert.StartsWith("near-", node.Name);
    }

    [Fact]
    public void NearestRoutableStart_PocketNode_DoesNotWinOnAnchorProof()
    {
        // A node inside the sealed pocket reaches a pocketed anchor trivially.
        // When an outside probe exists it must be proved against, so the pocket
        // node cannot pass for a rescue spot.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("inside", 10, 10, 0, "street"),
                Node("street", 20, 10, 0, "inside", "far"),
                Node("far", 70, 10, 0, "street")
            ]
        );

        static bool SealedBox(int x, int y, int z) =>
            x is not (>= 6 and <= 14) || y is not (>= 6 and <= 14) ||
            (x != 6 && x != 14 && y != 6 && y != 14);

        var node = Traveler.NearestRoutableStart(
            graph,
            new Point3D(10, 10, 0),
            new Point3D(10, 10, 0),
            SealedBox,
            walker: TestWalkers.Ground(SealedBox, null),
            isIndoor: null
        );

        Assert.Equal("street", node?.Name);
    }

    [Fact]
    public void HasClearStart_PocketNodeLine_IsNotClearWhenProbeFails()
    {
        // A same-room node is a straight walk away, so a dense scan with
        // no probe in it read a sealed spawn as a clear start. The component
        // probe exists but no plan out of the pocket does.
        var nodes = new List<NavNode> { Node("inside", 10, 10, 0, "near-0") };

        for (var i = 0; i < 49; i++)
        {
            nodes.Add(Node($"near-{i}", 20 + (i % 7) * 4, 4 + (i / 7) * 4, 0, "far"));
        }

        nodes.Add(Node("far", 70, 10, 0, "inside", "near-0"));

        var graph = new NavGraph(FacetNames.Felucca, nodes);

        static bool SealedBox(int x, int y, int z) =>
            x is not (>= 6 and <= 14) || y is not (>= 6 and <= 14) ||
            (x != 6 && x != 14 && y != 6 && y != 14);

        Assert.False(Traveler.HasClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Ground(SealedBox, null)));
    }

    [Fact]
    public void RoadBack_OffTheRoad_WalksToANodeTheSearchRoutes()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 60, 0, 0, "B"),
                Node("B", 70, 0, 0, "C"),
                Node("C", 80, 0, 0)
            ]
        );

        var tiles = Traveler.RoadBack(graph, new Point3D(0, 0, 0), "C", SearchFrom(graph, "C"), TestWalkers.Flat, isIndoor: null);

        Assert.NotEmpty(tiles);
        Assert.Equal(new Point3D(60, 0, 0), tiles[^1]);
    }

    [Fact]
    public void RoadBack_SealedRoom_DoesNotPickThePocketNode()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("room", 10, 10, 0, "street"),
                Node("street", 60, 10, 0, "room")
            ]
        );

        static bool Stand(int x, int y, int z) => x != 20;

        var tiles = Traveler.RoadBack(
            graph,
            new Point3D(12, 10, 0),
            "street",
            SearchFrom(graph, "street"),
            TestWalkers.Ground(Stand, null),
            isIndoor: null
        );

        Assert.DoesNotContain(new Point3D(10, 10, 0), tiles);
    }

    [Fact]
    public void RoadBack_BehindAWall_GivesUpWithinTheCellCap()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 200, 0, 0, "B"),
                Node("B", 210, 0, 0)
            ]
        );
        var probes = 0;

        // Open ground wider than the cap, cut off from the road by a wall with no gap.
        bool Walled(int x, int y, int z)
        {
            probes++;
            return x != 150;
        }

        var walker = TestWalkers.Ground(Walled, null);
        var prev = SearchFrom(graph, "B");
        probes = 0;

        var from = new Point3D(0, 0, 0);

        Assert.Empty(Traveler.RoadBack(graph, from, "B", prev, walker, isIndoor: null));
        Assert.True(probes <= ProbesPerCell * (TileRoute.WorldCellBudget(NavMetric.Chebyshev(from, graph.NodeAt(0).Location)) + 1), $"{probes} probes");
    }

    [Theory]
    [InlineData(Traveler.WhyNoStart, true)]
    [InlineData(Traveler.WhyFirstHopsBlocked, true)]
    [InlineData(Traveler.WhyNoPath, false)]
    [InlineData(Traveler.WhyGoalApart, false)]
    [InlineData(Traveler.WhyNoGoalNode, false)]
    [InlineData(null, false)]
    public void IsGroundFailure_OnlyTheWalkersOwnGround(string why, bool ground) =>
        Assert.Equal(ground, Traveler.IsGroundFailure(why));

    [Fact]
    public void NoteGroundRest_RestsEveryGoalFromTheCell_AndNoOtherCell()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0, 0)]);
        var from = new Point3D(2161, 460, 0);
        var otherCell = new Point3D(2261, 460, 0);

        Traveler.NoteGroundRest(graph, from);

        Assert.True(Traveler.RestsOnGround(graph, from));
        Assert.True(Traveler.InFailedCooldown(graph, from, "any goal"));
        Assert.True(Traveler.InFailedCooldown(graph, from, Traveler.FailTarget(null, new Point3D(2528, 575, 0))));
        Assert.False(Traveler.RestsOnGround(graph, otherCell));
        Assert.False(Traveler.InFailedCooldown(graph, otherCell, "any goal"));
    }

    [Fact]
    public void PlanNames_FailedCell_DoesNotSearchAgain()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0),
                Node("C", 400, 0, 0)
            ]
        );
        var from = new Point3D(0, 0, 0);

        Assert.Empty(Plan(graph, from, "C", Open));
        Assert.True(Traveler.InFailedCooldown(graph, from, "C"));
        Assert.Empty(Traveler.PlanNames(graph, from, "C", TestWalkers.Flat, null, null, out var whyNone));
        Assert.Equal(Traveler.WhyRecentFailure, whyNone);
    }

    [Fact]
    public void NoteFailedRoute_BlocksASecondSearchOnTheSameGraph()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0)
            ]
        );
        var from = new Point3D(0, 0, 0);
        Traveler.NoteFailedRoute(graph, from, "B");

        Assert.True(Traveler.InFailedCooldown(graph, from, "B"));
        Assert.Empty(Traveler.PlanNames(graph, from, "B", TestWalkers.Flat, null, null, out var whyNone));
        Assert.Equal(Traveler.WhyRecentFailure, whyNone);
    }

    [Fact]
    public void PrepareClearStart_NearbyStreetTiles_ShareOneSearch()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a", 10, 10, 0, "b"),
                Node("b", 60, 10, 0, "a")
            ]
        );

        var proof = Traveler.PrepareClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Flat);

        Assert.Equal(1, proof.PlanSearches);
        Assert.True(proof.At(new Point3D(10, 10, 0)));
        Assert.True(proof.At(new Point3D(11, 10, 0)));
        Assert.True(proof.At(new Point3D(12, 10, 0)));
        Assert.Equal(1, proof.PlanSearches);
    }

    [Fact]
    public void PrepareClearStart_SealedRoom_NearbyTilesStaySealed()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("room", 10, 10, 0, "street"),
                Node("street", 60, 10, 0, "room")
            ]
        );

        static bool Stand(int x, int y, int z) => x != 20;

        var proof = Traveler.PrepareClearStart(graph, new Point3D(10, 10, 0), TestWalkers.Ground(Stand, null));

        Assert.Equal(1, proof.PlanSearches);
        Assert.False(proof.At(new Point3D(10, 10, 0)));
        Assert.False(proof.At(new Point3D(11, 10, 0)));
    }

    // A roofed bank, x and y 0 to 20, walled round but for a door at (20, 15). Its counter
    // node and the yard outside are joined by a road that bends through the door, so no
    // straight line from inside reaches the yard; the road runs on east past the probe radius.
    private const int BankWall = 20;
    private const int BankDoorY = 15;
    private static readonly Point3D BankFloor = new(5, 5, 0);

    private static NavGraph BankGraph()
    {
        var counter = Node("counter", 12, 12, 0, "yard");
        counter.Indoor = true;

        return new NavGraph(
            FacetNames.Felucca,
            [
                counter,
                Node("yard", 26, BankDoorY, 0, "counter", "road"),
                Node("road", 80, BankDoorY, 0, "yard")
            ]
        );
    }

    private static bool BankWalls(int x, int y, int z) =>
        !InBank(x, y) || x is > 0 and < BankWall && y is > 0 and < BankWall || x == BankWall && y == BankDoorY;

    private static bool BankDoorShut(int x, int y, int z) => BankWalls(x, y, z) && !(x == BankWall && y == BankDoorY);

    private static bool UnderBankRoof(int x, int y, int z) => InBank(x, y);

    private static bool InBank(int x, int y) => x is >= 0 and <= BankWall && y is >= 0 and <= BankWall;

    [Fact]
    public void HasClearStart_UnderARoof_LeavesByTheDoor()
    {
        // The Jhelom and Skara Brae town sites are bank tiles. No straight line from the
        // floor reaches a street, and every copy homed there was refused its spawn.
        Assert.True(Traveler.HasClearStart(BankGraph(), BankFloor, TestWalkers.Ground(BankWalls, null), UnderBankRoof));
    }

    [Fact]
    public void HasClearStart_UnderARoofWithNoWayOut_IsNotClear() =>
        Assert.False(Traveler.HasClearStart(BankGraph(), BankFloor, TestWalkers.Ground(BankDoorShut, null), UnderBankRoof));

    // A roofed room (y <= 16) walled off from the street except a door gap at
    // x = 10 between y 15 and 16. room and door sit inside; mid and street out.
    private static readonly Point3D IndoorFrom = new(8, 10, 0);

    private static NavGraph IndoorGraph()
    {
        var room = Node("room", 10, 10, 0, "door");
        var door = Node("door", 10, 14, 0, "room", "mid");
        room.Indoor = true;
        door.Indoor = true;

        return new NavGraph(
            FacetNames.Felucca,
            [
                room,
                door,
                Node("mid", 10, 25, 0, "door", "street"),
                Node("street", 30, 40, 0, "mid")
            ]
        );
    }

    private static bool Stand(int x, int y, int z) => !(y is >= 15 and <= 16) || x == 10;

    private static bool Indoor(int x, int y, int z) => y <= 16;

    [Fact]
    public void FailTarget_NoNodeNamesTheGoal_CoolsDownUnderTheTile()
    {
        // A ghost whose shrine had no node near it searched again every ten seconds.
        var goal = new Point3D(4212, 563, 0);
        var from = new Point3D(10, 10, 0);
        var target = Traveler.FailTarget(null, goal);

        Assert.StartsWith(Traveler.TileTargetPrefix, target);
        Assert.Equal("bank", Traveler.FailTarget("bank", goal));
        Assert.Null(Traveler.FailTarget(null, Point3D.Zero));

        Assert.False(Traveler.InFailedCooldown(null, from, target));
        Traveler.NoteFailedRoute(null, from, target);
        Assert.True(Traveler.InFailedCooldown(null, from, target));
    }

    private static bool Open(int x, int y, int z) => true;

    private static bool WallBand(int x, int y, int z) => x is < 10 or > 15;

    private static IReadOnlyList<string> Plan(
        NavGraph graph,
        Point3D from,
        string destination,
        System.Func<int, int, int, bool> stand,
        System.Func<int, int, int, bool> indoor = null
    ) =>
        Traveler.PlanNames(graph, from, destination, TestWalkers.Ground(stand, groundZ: null), indoor, avoid: null, out _);

    private static NavNode Node(string name, int x, int y, int z, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            Connects = [.. connects]
        };

    [Fact]
    public void NeedsTileLeg_PastOneWaypointSpacing()
    {
        var from = new Point3D(100, 100, 0);

        Assert.False(TravelSkill.NeedsTileLeg(from, new Point3D(100 + TileRoute.WaypointSpacing, 100, 0)));
        Assert.True(TravelSkill.NeedsTileLeg(from, new Point3D(100 + TileRoute.WaypointSpacing + 1, 100, 0)));
        Assert.True(TravelSkill.NeedsTileLeg(from, new Point3D(100, 100 + NavLimits.MaxLegDistance + 1, 0)));
    }

    /// <summary>Stand probes one tile search cell makes at most: one per straight step, three per diagonal with its two side tiles.</summary>
    private const int ProbesPerCell = 16;

    /// <summary>The path worker's search from <paramref name="destination"/>.</summary>
    private static Dictionary<string, string> SearchFrom(NavGraph graph, string destination)
    {
        var cost = new Dictionary<string, double>(System.StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        NavSearch.Explore(graph, destination, stopAt: null, NavSearch.DefaultGateCost, avoid: null, indoorKeep: null, cost, prev);
        return prev;
    }
}
