using System;
using System.Collections.Generic;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class GraphConnectTests
{
    private const int JoinPairDistance = 20;
    private const int BridgePairDistance = 40;
    private const int BlockedSampleX = 8;
    private const int OneTileWallX = 1;

    [Fact]
    public void Join_TwoNodesTwentyTilesWalkable_AddsUndirectedEdge()
    {
        var a = Node("A", 0, 0, 0);
        var b = Node("B", JoinPairDistance, 0, 0);
        var nodes = new List<NavNode> { a, b };

        GraphConnect.Join(nodes, TestWalkers.AnyFloor);
        GraphConnect.Bridge(nodes, TestWalkers.AnyFloor);

        Assert.Equal(2, nodes.Count);
        Assert.Contains("B", a.Connects);
        Assert.Contains("A", b.Connects);
        Assert.DoesNotContain("A", a.Connects);
        Assert.DoesNotContain("B", b.Connects);
        Assert.True(NavMetric.Chebyshev(a.Location, b.Location) <= NavLimits.SoftLegDistance);
        Assert.Equal(0, ValidationNotes(nodes));
    }

    [Fact]
    public void Bridge_FortyTilesWalkable_InsertsHopsWithinSoftLeg()
    {
        var a = Node("A", 0, 0, 0);
        var b = Node("B", BridgePairDistance, 0, 0);
        var nodes = new List<NavNode> { a, b };

        GraphConnect.Join(nodes, TestWalkers.AnyFloor);
        GraphConnect.Bridge(nodes, TestWalkers.AnyFloor);

        Assert.True(nodes.Count > 2);
        Assert.DoesNotContain("B", a.Connects);
        Assert.DoesNotContain("A", b.Connects);

        var hopName = $"{BridgePrefix(a.Name, b.Name)}-0";
        var hop = Find(nodes, hopName);
        Assert.NotNull(hop);
        Assert.Equal(NavLimits.SoftLegDistance, hop.X);
        Assert.Equal(0, hop.Y);
        Assert.Equal(0, hop.Z);
        Assert.Equal(NavLimits.DefaultArrivalRange, hop.ArrivalRange);
        Assert.Contains(hop.Name, a.Connects);
        Assert.Contains("A", hop.Connects);
        Assert.Contains("B", hop.Connects);
        Assert.Contains(hop.Name, b.Connects);

        AssertAllHopsWithinSoftLeg(nodes);

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        Assert.True(graph.SameComponent("A", "B"));
        Assert.Equal(0, graph.CountValidationNotes());
    }

    [Fact]
    public void JoinAndBridge_BlockedLine_AddsNoEdge()
    {
        var nearA = Node("A", 0, 0, 0);
        var nearB = Node("B", JoinPairDistance, 0, 0);
        var joinNodes = new List<NavNode> { nearA, nearB };

        GraphConnect.Join(joinNodes, BlockedWalker);
        GraphConnect.Bridge(joinNodes, BlockedWalker);

        Assert.Equal(2, joinNodes.Count);
        Assert.Empty(nearA.Connects);
        Assert.Empty(nearB.Connects);

        var farA = Node("C", 0, 0, 0);
        var farB = Node("D", BridgePairDistance, 0, 0);
        var bridgeNodes = new List<NavNode> { farA, farB };

        GraphConnect.Join(bridgeNodes, BlockedWalker);
        GraphConnect.Bridge(bridgeNodes, BlockedWalker);

        Assert.Equal(2, bridgeNodes.Count);
        Assert.Empty(farA.Connects);
        Assert.Empty(farB.Connects);
    }

    [Fact]
    public void Join_OneTileWall_AddsNoEdge()
    {
        var a = Node("A", 0, 0, 0);
        var b = Node("B", JoinPairDistance, 0, 0);

        GraphConnect.Join([a, b], TestWalkers.From((x, _, z) => x != OneTileWallX ? z : null));

        Assert.Empty(a.Connects);
    }

    [Fact]
    public void Join_CliffOneWayDown_AddsNoEdge()
    {
        // A walker drops off a ledge but cannot climb back up it. An edge is walked both
        // ways, so a one-way drop is no edge at all.
        var ledge = Node("ledge", 0, 0, CliffZ);
        var foot = Node("foot", JoinPairDistance, 0, 0);

        GraphConnect.Join([ledge, foot], CliffWalker);

        Assert.Empty(ledge.Connects);
    }

    [Fact]
    public void BridgeFar_BeyondLocalBridgeMax_InsertsHopsAndJoins()
    {
        var far = GraphConnect.BridgeMax + NavLimits.SoftLegDistance;
        var a = Node("A", 0, 0, 0);
        var b = Node("B", far, 0, 0);
        var nodes = new List<NavNode> { a, b };

        GraphConnect.Join(nodes, TestWalkers.AnyFloor);
        GraphConnect.Bridge(nodes, TestWalkers.AnyFloor);

        var afterLocal = new NavGraph(FacetNames.Felucca, nodes);
        Assert.False(afterLocal.SameComponent("A", "B"));

        GraphConnect.BridgeFar(nodes, TestWalkers.AnyFloor);

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        Assert.True(graph.SameComponent("A", "B"));
        Assert.True(nodes.Count > 2);
        AssertAllHopsWithinSoftLeg(nodes);
    }

    [Fact]
    public void AttachDoors_ShopBehindAWall_JoinsThroughTheDoorway()
    {
        // The straight line to the street crosses the shop wall. A real tile route
        // turns through the open doorway and reaches it, so the shop gets its edge.
        var inside = Node("shop", 0, 0, 0);
        inside.Indoor = true;
        var street = Node("street", 20, 0, 0);
        var nodes = new List<NavNode> { inside, street };

        GraphConnect.AttachDoors(nodes, TestWalkers.Ground(ShopWithDoor, FlatZero), ShopInterior);

        Assert.Contains("street", inside.Connects);
        Assert.Contains("shop", street.Connects);
    }

    [Fact]
    public void AttachDoors_UpstairsRoom_StaysUnlinked()
    {
        // A route found on the ground cannot climb stairs. Joining an upstairs room to
        // the street would fake a link the pathfinder cannot walk.
        var upstairs = Node("room", 0, 0, 20);
        upstairs.Indoor = true;
        var street = Node("street", 20, 0, 0);
        var nodes = new List<NavNode> { upstairs, street };

        GraphConnect.AttachDoors(nodes, TestWalkers.Ground(GroundOnlyWithDoor, FlatZero), ShopInterior);

        Assert.Empty(upstairs.Connects);
        Assert.Empty(street.Connects);
    }

    [Fact]
    public void Join_DoesNotAddSelfEdges()
    {
        var a = Node("A", 0, 0, 0);
        var b = Node("B", JoinPairDistance, 0, 0);
        var clone = Node("A", 4, 0, 0);
        var nodes = new List<NavNode> { a, b, clone };

        GraphConnect.Join(nodes, TestWalkers.AnyFloor);
        GraphConnect.Bridge(nodes, TestWalkers.AnyFloor);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            Assert.DoesNotContain(node.Name, node.Connects, StringComparer.OrdinalIgnoreCase);
        }

        Assert.Equal(0, ValidationNotes(nodes));
    }

    [Fact]
    public void Bridge_HopOverRaisedGround_StandsOnTheFloorFound()
    {
        // A hop takes the floor at its own tile, not the height of the node it starts
        // from. A hop left at the start height is a goal on the wrong pathfinder layer.
        var a = Node("A", 0, 0, 0);
        var b = Node("B", BridgePairDistance, 0, 0);
        var nodes = new List<NavNode> { a, b };

        GraphConnect.Bridge(nodes, TestWalkers.From(RaisedFloorPastWall));

        var hop = Find(nodes, $"{BridgePrefix(a.Name, b.Name)}-0");
        Assert.NotNull(hop);
        Assert.Equal(RaisedFloorZ, hop.Z);
    }

    [Fact]
    public void LinkStranded_LoneNodeWithClearLine_JoinsTheNearestLinkedNode()
    {
        var west = Node("west", 0, 0, 0);
        var east = Node("east", JoinPairDistance, 0, 0);
        var lone = Node("lone", JoinPairDistance / 2, StrandedOffsetY, 0);
        LinkBoth(west, east);
        var nodes = new List<NavNode> { west, east, lone };

        GraphConnect.LinkStranded(nodes, TestWalkers.AnyFloor);

        Assert.Single(lone.Connects);
        Assert.True(GraphConnect.HasWalkLink(lone));
    }

    [Fact]
    public void LinkStranded_LoneNodeBehindAWall_StaysAloneAndIsDropped()
    {
        var west = Node("west", 0, 0, 0);
        var east = Node("east", 0, JoinPairDistance, 0);
        var lone = Node("lone", WallX + DoorwayY, 0, 0);
        LinkBoth(west, east);
        var nodes = new List<NavNode> { west, east, lone };

        GraphConnect.LinkStranded(nodes, TestWalkers.Ground(WallAtX, null));
        GraphConnect.DropUnlinked(nodes);

        Assert.Empty(lone.Connects);
        Assert.DoesNotContain(lone, nodes);
        Assert.Contains(west, nodes);
        Assert.Contains(east, nodes);
    }

    [Fact]
    public void DropUnlinked_GateOnlyNode_IsKept()
    {
        var pad = Node("pad", 0, 0, 0);
        var far = Node("far", GraphConnect.BridgeMax, 0, 0);
        NavGates.Add(pad, far, NavGateKind.Teleporter);
        var nodes = new List<NavNode> { pad, far };

        GraphConnect.DropUnlinked(nodes);

        Assert.Equal(2, nodes.Count);
        Assert.False(GraphConnect.HasWalkLink(pad));
    }

    private static void LinkBoth(NavNode left, NavNode right)
    {
        left.Connects.Add(right.Name);
        right.Connects.Add(left.Name);
    }

    private static void AssertAllHopsWithinSoftLeg(List<NavNode> nodes)
    {
        var byName = new Dictionary<string, NavNode>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < nodes.Count; i++)
        {
            byName[nodes[i].Name] = nodes[i];
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var links = node.Connects;

            for (var j = 0; j < links.Count; j++)
            {
                Assert.True(byName.TryGetValue(links[j], out var other));
                Assert.True(NavMetric.Chebyshev(node.Location, other.Location) <= NavLimits.SoftLegDistance);
            }
        }
    }

    private static int ValidationNotes(List<NavNode> nodes) =>
        new NavGraph(FacetNames.Felucca, nodes).CountValidationNotes();

    private static NavNode Find(List<NavNode> nodes, string name)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return nodes[i];
            }
        }

        return null;
    }

    private static string BridgePrefix(string from, string to) => $"br-{from}-{to}";

    private static readonly TileWalker BlockedWalker = TestWalkers.From((x, _, z) => x != BlockedSampleX ? z : null);

    /// <summary>A ledge at the cliff height west of the cliff column, ground east of it.</summary>
    private static readonly TileWalker CliffWalker = new(
        (x, _, _) => x < CliffX ? CliffZ : 0,
        (int _, int _, int fromZ, int toX, int _, out int toZ) =>
        {
            toZ = toX < CliffX ? CliffZ : 0;
            return toZ - fromZ <= CliffClimb;
        }
    );

    /// <summary>Flat ground that rises onto a raised floor past the wall column.</summary>
    private static int? RaisedFloorPastWall(int x, int y, int z) => x > WallX ? RaisedFloorZ : z;

    /// <summary>A wall across every row at the wall column.</summary>
    private static bool WallAtX(int x, int y, int z) => x != WallX;

    /// <summary>A wall two tiles wide with a doorway at y 8 and beyond.</summary>
    private static bool ShopWithDoor(int x, int y, int z) => !(x is WallX or WallX + 1) || y >= DoorwayY;

    private static bool GroundOnlyWithDoor(int x, int y, int z) => z == 0 && ShopWithDoor(x, y, z);

    private static int FlatZero(int x, int y) => 0;

    private static bool ShopInterior(int x, int y, int z) => x < 5;

    private const int WallX = 10;
    private const int DoorwayY = 8;
    private const int RaisedFloorZ = 7;
    private const int StrandedOffsetY = 6;
    private const int CliffX = 10;
    private const int CliffZ = 20;
    private const int CliffClimb = 2;

    private static NavNode Node(string name, int x, int y, int z) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            ArrivalRange = NavLimits.DefaultArrivalRange,
            Connects = []
        };
}
