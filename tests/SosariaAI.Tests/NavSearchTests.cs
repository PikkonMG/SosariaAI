using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NavSearchTests
{
    [Fact]
    public void FindPath_ThreeNodeLine_ReturnsNamesInOrder()
    {
        var graph = LineGraph();
        var path = NavSearch.FindPath(graph, "A", "C");

        Assert.Equal(["A", "B", "C"], path);
    }

    [Fact]
    public void FindPath_UnknownNode_ReturnsEmpty()
    {
        var graph = LineGraph();

        Assert.Empty(NavSearch.FindPath(graph, "A", "missing"));
        Assert.Empty(NavSearch.FindPath(graph, "missing", "A"));
    }

    [Fact]
    public void FindPath_SameStartAndEnd_ReturnsSingleName()
    {
        var path = NavSearch.FindPath(LineGraph(), "A", "A");

        Assert.Equal(["A"], path);
    }

    [Fact]
    public void FindPath_DisconnectedComponents_ReturnsEmpty()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0),
                Node("C", 100, 0, 0)
            ]
        );

        Assert.Empty(NavSearch.FindPath(graph, "A", "C"));
    }

    [Fact]
    public void FindPath_EdgeListedOnlyOnA_WalksFromBToA()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0)
            ]
        );

        Assert.Equal(["B", "A"], NavSearch.FindPath(graph, "B", "A"));
    }

    [Fact]
    public void FindPath_ShortWalk_BeatsFarGate()
    {
        var home = Node("Home", 0, 0, 0, "Mid");
        var mid = Node("Mid", 20, 0, 0, "Tree");
        var tree = Node("Tree", 40, 0, 0);
        NavGates.Add(home, tree, NavGateKind.Moongate);
        var graph = new NavGraph(FacetNames.Felucca, [home, mid, tree]);

        var path = NavSearch.FindPath(graph, "Home", "Tree", NavSearch.DefaultGateCost);

        Assert.Equal(["Home", "Mid", "Tree"], path);
    }

    [Fact]
    public void FindPath_FarIsland_UsesGate()
    {
        var home = Node("Home", 0, 0, 0, "Tree");
        var tree = Node("Tree", 40, 0, 0);
        var island = Node("Island", 4000, 0, 0);
        NavGates.Add(home, island, NavGateKind.Moongate);
        var graph = new NavGraph(FacetNames.Felucca, [home, tree, island]);

        var path = NavSearch.FindPath(graph, "Home", "Island", NavSearch.DefaultGateCost);

        Assert.Equal(["Home", "Island"], path);
    }

    [Fact]
    public void OneWayPads_AreTakenOnlyTheirOwnWay()
    {
        // Moonglow's hub: its pad sends a person out, and the landing of the pad that
        // brings one back is bare ground. The way back is the pad beside the far landing.
        var hubPad = Node("HubPad", 0, 0, 0, "HubLanding");
        var hubLanding = Node("HubLanding", 6, 0, 0);
        var farLanding = Node("FarLanding", 200, 0, 0, "FarPad");
        var farPad = Node("FarPad", 208, 0, 0);
        NavGates.AddOneWay(hubPad, farLanding, NavGateKind.Teleporter);
        NavGates.AddOneWay(farPad, hubLanding, NavGateKind.Teleporter);
        var graph = new NavGraph(FacetNames.Felucca, [hubPad, hubLanding, farLanding, farPad]);

        Assert.Equal(["HubPad", "FarLanding"], NavSearch.FindPath(graph, "HubPad", "FarLanding", NavSearch.DefaultGateCost));
        Assert.Equal(
            ["FarLanding", "FarPad", "HubLanding", "HubPad"],
            NavSearch.FindPath(graph, "FarLanding", "HubPad", NavSearch.DefaultGateCost)
        );

        // A plan searched from its goal, as the path workers search: the walker at the far
        // landing heads for the pad beside it, not back through the bare hub landing.
        var cost = new Dictionary<string, double>();
        var prev = new Dictionary<string, string>();
        NavSearch.Explore(graph, "HubPad", null, NavSearch.DefaultGateCost, null, null, cost, prev);

        Assert.Equal("FarPad", prev["FarLanding"]);
    }

    private static NavGraph LineGraph() =>
        new(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0, "C"),
                Node("C", 20, 0, 0)
            ]
        );

    private static NavNode Node(string name, int x, int y, int z, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            Connects = [.. connects]
        };

}
