using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class TeleporterLinkingTests
{
    [Fact]
    public void Link_TwoNodes_AddsUndirectedConnects()
    {
        var gate = Node("despise-gate", 1298, 1080, 0);
        var inside = Node("despise-inside", 5587, 631, 30);
        var nodes = new List<NavNode> { gate, inside };

        TeleporterLinking.Link(nodes, [("despise-gate", "despise-inside", false)]);

        Assert.Contains("despise-inside", gate.Connects);
        Assert.Contains("despise-gate", inside.Connects);
        Assert.Single(gate.Connects);
        Assert.Single(inside.Connects);

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        Assert.Contains("despise-inside", graph.Neighbors("despise-gate"));
        Assert.Contains("despise-gate", graph.Neighbors("despise-inside"));
    }

    [Fact]
    public void Link_OneWayPad_RecordsTheGateOnlyWhereItSends()
    {
        var gate = Node("despise-gate", 1298, 1080, 0);
        var inside = Node("despise-inside", 5587, 631, 30);
        var nodes = new List<NavNode> { gate, inside };

        TeleporterLinking.Link(nodes, [("despise-gate", "despise-inside", false)]);

        Assert.Contains(gate.Gates, g => g.To == "despise-inside" && g.ParsedKind == NavGateKind.Teleporter);
        Assert.Null(inside.Gates);
        Assert.Equal(NavGateKind.Teleporter, NavGates.KindEitherWay(inside, gate));

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        Assert.True(graph.IsGate("despise-inside", "despise-gate"));
        Assert.True(graph.Travels("despise-gate", "despise-inside"));
        Assert.False(graph.Travels("despise-inside", "despise-gate"));
    }

    [Fact]
    public void Link_BackTrue_IsTwoWay()
    {
        var src = Node("shame-gate", 514, 1561, 0);
        var dst = Node("shame-inside", 5395, 126, 0);
        var nodes = new List<NavNode> { src, dst };

        TeleporterLinking.Link(nodes, [("shame-gate", "shame-inside", true)]);

        Assert.Contains("shame-inside", src.Connects);
        Assert.Contains("shame-gate", dst.Connects);
        Assert.Contains(dst.Gates, g => g.To == "shame-gate");

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        Assert.True(graph.Travels("shame-inside", "shame-gate"));
        Assert.True(graph.Travels("shame-gate", "shame-inside"));
    }

    [Fact]
    public void Link_TwoOneWayPadsFacingEachOther_AreTwoWay()
    {
        var src = Node("a", 0, 0, 0);
        var dst = Node("b", 100, 0, 0);
        var nodes = new List<NavNode> { src, dst };

        TeleporterLinking.Link(nodes, [("a", "b", false), ("b", "a", false)]);
        var graph = new NavGraph(FacetNames.Felucca, nodes);

        Assert.True(graph.Travels("a", "b"));
        Assert.True(graph.Travels("b", "a"));
    }

    [Fact]
    public void Link_MissingDestination_Skips()
    {
        var src = Node("fel-gate", 100, 100, 0);
        var nodes = new List<NavNode> { src };

        TeleporterLinking.Link(nodes, [("fel-gate", "tram-gate", false)]);

        Assert.Empty(src.Connects);
    }

    [Fact]
    public void Link_Duplicate_DoesNotRepeatConnects()
    {
        var src = Node("a", 0, 0, 0);
        var dst = Node("b", 10, 0, 0);
        var nodes = new List<NavNode> { src, dst };

        TeleporterLinking.Link(
            nodes,
            [
                ("a", "b", false),
                ("a", "b", true)
            ]
        );

        Assert.Equal(["b"], src.Connects);
        Assert.Equal(["a"], dst.Connects);
    }

    [Fact]
    public void Link_SelfName_Skips()
    {
        var node = Node("same", 0, 0, 0);
        var nodes = new List<NavNode> { node };

        TeleporterLinking.Link(nodes, [("same", "same", true)]);

        Assert.Empty(node.Connects);
    }

    [Fact]
    public void Link_Teleporter_AttachesBothPadsAndConnects()
    {
        var nodes = new List<NavNode>();
        var links = new TeleporterLink[]
        {
            new(FacetNames.Felucca, 311, 786, 0, FacetNames.Felucca, 5750, 350, 5, false)
        };

        TeleporterLinking.Link(nodes, links);

        Assert.Equal(2, nodes.Count);
        Assert.Equal("tp-311-786", nodes[0].Name);
        Assert.Equal("tp-5750-350", nodes[1].Name);
        Assert.Contains(nodes[1].Name, nodes[0].Connects);
        Assert.Contains(nodes[0].Name, nodes[1].Connects);
    }

    [Fact]
    public void FindOrAttach_NodeOnTheExactTile_ReusesItAndStopsOnIt()
    {
        var existing = Node("britain-bank", 1425, 1695, 0);
        var nodes = new List<NavNode> { existing };

        var name = TeleporterLinking.FindOrAttach(nodes, new Point3D(1425, 1695, 0));

        Assert.Equal("britain-bank", name);
        Assert.Single(nodes);
        Assert.Equal(NavLimits.DoorArrivalRange, existing.ArrivalRange);
    }

    [Fact]
    public void FindOrAttach_NodeOneTileAway_AddsAPadOnTheExactTile()
    {
        // A pad snapped to a neighbour stops the character off the pad, and the gate
        // skill then teleports it from bare ground.
        var existing = Node("britain-bank", 1425, 1695, 0);
        var nodes = new List<NavNode> { existing };
        var point = new Point3D(1426, 1695, 5);

        var name = TeleporterLinking.FindOrAttach(nodes, point);

        Assert.Equal("tp-1426-1695", name);
        Assert.Equal(2, nodes.Count);
        Assert.Equal(point.X, nodes[1].X);
        Assert.Equal(point.Y, nodes[1].Y);
        Assert.Equal(point.Z, nodes[1].Z);
        Assert.Equal(NavLimits.DoorArrivalRange, nodes[1].ArrivalRange);
        Assert.Equal(name, TeleporterLinking.FindOrAttach(nodes, point));
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public void FindOrAttach_EmptyList_AddsTpNode()
    {
        var nodes = new List<NavNode>();
        var point = new Point3D(311, 786, 0);

        var name = TeleporterLinking.FindOrAttach(nodes, point);

        Assert.Equal("tp-311-786", name);
        Assert.Single(nodes);
        Assert.Equal("tp-311-786", nodes[0].Name);
    }

    private static NavNode Node(string name, int x, int y, int z) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z
        };
}
