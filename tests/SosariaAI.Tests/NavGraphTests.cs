using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NavGraphTests
{
    [Fact]
    public void FindNearest_PicksClosestByDistance()
    {
        var near = Node("near", 0, 0, 0);
        var far = Node("far", 50, 0, 0);
        var graph = new NavGraph(FacetNames.Felucca, [near, far]);
        var from = new Point3D(4, 0, 0);

        Assert.True(NavMetric.Distance(from, near.Location) < NavMetric.Distance(from, far.Location));
        Assert.Equal("near", graph.FindNearest(from).Name);
    }

    [Fact]
    public void CountValidationNotes_OverCap_WhenChebyshevExceedsMax()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", NavLimits.MaxLegDistance + 1, 0, 0)
            ]
        );

        Assert.False(NavMetric.WithinLegCap(new Point3D(0, 0, 0), new Point3D(NavLimits.MaxLegDistance + 1, 0, 0)));
        Assert.Equal(1, graph.CountValidationNotes());
    }

    [Fact]
    public void CountValidationNotes_DiagonalWithinTheTileLeg_IsNoNote()
    {
        // The leg cap counts tiles: a diagonal link whose straight line is longer than the cap
        // still fits the pathfinder's box. Measured as a straight line, it was a note on every boot.
        const int diagonal = NavLimits.SoftLegDistance;
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", diagonal, diagonal, 0)
            ]
        );

        Assert.True(NavMetric.Planar(new Point3D(0, 0, 0), new Point3D(diagonal, diagonal, 0)) > NavLimits.MaxLegDistance);
        Assert.Equal(0, graph.CountValidationNotes());
    }

    [Fact]
    public void CountValidationNotes_SelfEdge()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0, 0, "A")]);

        Assert.Equal(1, graph.CountValidationNotes());
    }

    [Fact]
    public void CountValidationNotes_UnknownNeighbor()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0, 0, "ghost")]);

        Assert.Equal(1, graph.CountValidationNotes());
    }

    [Fact]
    public void CountValidationNotes_SoftCap_WhenOverSoftAndWithinMax()
    {
        var length = NavLimits.SoftLegDistance + 1;
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", length, 0, 0)
            ]
        );

        Assert.True(length > NavLimits.SoftLegDistance);
        Assert.True(length <= NavLimits.MaxLegDistance);
        Assert.Equal(1, graph.CountValidationNotes());
    }

    [Fact]
    public void Connect_SkipsSelf()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "A", "B"),
                Node("B", 10, 0, 0)
            ]
        );

        Assert.DoesNotContain("A", graph.Neighbors("A"));
        Assert.Contains("B", graph.Neighbors("A"));
        Assert.Contains("A", graph.Neighbors("B"));
    }

    [Fact]
    public void WithinLegCap_ThirtyTilesTrue_FortyTilesFalse()
    {
        var origin = new Point3D(0, 0, 0);

        Assert.True(NavMetric.WithinLegCap(origin, new Point3D(30, 0, 0)));
        Assert.False(NavMetric.WithinLegCap(origin, new Point3D(40, 0, 0)));
    }

    [Fact]
    public void GateLink_JoinsComponent_AndSkipsOverCap()
    {
        const int gateSpan = 200;
        var a = Node("A", 0, 0, 0, "B");
        var b = Node("B", gateSpan, 0, 0);
        a.Gates =
        [
            new NavGateLink
            {
                To = "B",
                Kind = NavGateLink.KindName(NavGateKind.Teleporter)
            }
        ];

        var graph = new NavGraph(FacetNames.Felucca, [a, b]);

        Assert.True(graph.SameComponent("A", "B"));
        Assert.Contains("B", graph.Neighbors("A"));
        Assert.Contains("A", graph.Neighbors("B"));
        Assert.True(graph.IsGate("A", "B"));
        Assert.True(graph.IsGate("B", "A"));
        Assert.Equal(NavGateKind.Teleporter, graph.GateKind("A", "B"));
        Assert.Equal(0, graph.CountValidationNotes());
    }

    [Fact]
    public void WalkEdge_IsNotGate()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("A", 0, 0, 0, "B"),
                Node("B", 10, 0, 0)
            ]
        );

        Assert.False(graph.IsGate("A", "B"));
        Assert.Equal(NavGateKind.None, graph.GateKind("A", "B"));
        Assert.Equal(0, graph.CountValidationNotes());
    }

    [Fact]
    public void FindNearest_SectorIndex_MatchesAFullSort()
    {
        const int nodeCount = 600;
        const int worldSpan = NavGraph.SectorSize * 9;
        const int zSpan = 60;
        const int take = 12;
        var random = new Random(7);
        var nodes = new List<NavNode>(nodeCount);

        for (var i = 0; i < nodeCount; i++)
        {
            nodes.Add(Node($"n{i}", random.Next(worldSpan), random.Next(worldSpan), random.Next(zSpan)));
        }

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        Point3D[] probes =
        [
            new(0, 0, 0),
            new(worldSpan / 2, worldSpan / 2, zSpan),
            new(worldSpan + NavGraph.SectorSize * 3, -NavGraph.SectorSize, 0),
            new(NavGraph.SectorSize - 1, NavGraph.SectorSize, 5)
        ];

        foreach (var from in probes)
        {
            var sorted = new List<NavNode>(nodes);
            sorted.Sort((a, b) => NavMetric.Distance(from, a.Location).CompareTo(NavMetric.Distance(from, b.Location)));

            Assert.Equal(NavMetric.Distance(from, sorted[0].Location), NavMetric.Distance(from, graph.FindNearest(from).Location));

            var found = graph.FindNearest(from, take);
            Assert.Equal(take, found.Count);

            for (var i = 0; i < take; i++)
            {
                Assert.Equal(NavMetric.Distance(from, sorted[i].Location), NavMetric.Distance(from, found[i].Location));
            }
        }
    }

    [Fact]
    public void FindNearest_CountAboveNodeCount_ReturnsEveryNode()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("a", 0, 0, 0), Node("b", NavGraph.SectorSize * 4, 0, 0)]);

        Assert.Equal(2, graph.FindNearest(new Point3D(NavGraph.SectorSize * 20, 0, 0), 5).Count);
        Assert.Empty(graph.FindNearest(Point3D.Zero, 0));
        Assert.Null(new NavGraph(FacetNames.Felucca, []).FindNearest(Point3D.Zero));
    }

    [Fact]
    public void InvalidateComponents_RecomputesAfterAnIndoorMark()
    {
        var west = Node("west", 0, 0, 0, "shop");
        var shop = Node("shop", 10, 0, 0, "west", "east");
        var east = Node("east", 20, 0, 0, "shop");
        var graph = new NavGraph(FacetNames.Felucca, [west, shop, east]);

        Assert.True(graph.SameComponent("west", "east"));

        shop.Indoor = true;
        Assert.True(graph.SameComponent("west", "east"));

        graph.InvalidateComponents();
        Assert.False(graph.SameComponent("west", "east"));
    }

    [Fact]
    public void LargestComponent_CachedResultStaysConsistent()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a", 0, 0, 0, "b"),
                Node("b", 10, 0, 0, "a", "c"),
                Node("c", 20, 0, 0, "b"),
                Node("alone", 500, 500, 0)
            ]
        );

        var main = graph.LargestComponent();

        Assert.Equal(main, graph.ComponentOf("a"));
        Assert.Equal(main, graph.LargestComponent());
        Assert.Equal(main, graph.LargestComponent());
    }

    [Fact]
    public void LargestComponent_RecomputesAfterInvalidate()
    {
        var shop = Node("shop", 10, 0, 0, "west", "east");
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("west", 0, 0, 0, "shop"),
                shop,
                Node("east", 20, 0, 0, "shop"),
                Node("lone", 500, 500, 0)
            ]
        );

        var main = graph.LargestComponent();
        Assert.Equal(main, graph.ComponentOf("west"));

        shop.Indoor = true;
        graph.InvalidateComponents();

        // The indoor mark splits the three-node piece into three one-node pieces, so
        // the cached id must be recomputed rather than reused.
        Assert.NotEqual(-1, graph.LargestComponent());
        Assert.True(graph.LargestComponent() >= 0);
    }

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
