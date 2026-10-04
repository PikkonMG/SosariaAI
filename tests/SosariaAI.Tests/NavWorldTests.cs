using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NavWorldTests
{
    [Fact]
    public void LeftoverPieces_EmptyGraph_ReturnsEmpty()
    {
        var empty = new NavGraph(FacetNames.Felucca, []);

        Assert.Empty(NavWorld.LeftoverPieces(empty, NavWorld.LeftoverPiecesToLog));
        Assert.Empty(NavWorld.LeftoverPieces(null, NavWorld.LeftoverPiecesToLog));
    }

    [Fact]
    public void MarkIndoor_MarksTheRoofAndDropsTheComponentCache()
    {
        var west = new NavNode { Name = "west", X = 0, Y = 0, Connects = ["shop"] };
        var shop = new NavNode { Name = "shop", X = 10, Y = 0, Connects = ["west", "east"] };
        var east = new NavNode { Name = "east", X = 20, Y = 0, Connects = ["shop"] };
        var graph = new NavGraph(FacetNames.Felucca, [west, shop, east]);

        Assert.True(graph.SameComponent("west", "east"));

        NavWorld.MarkIndoor(graph, (x, _, _) => x == shop.X);

        Assert.True(shop.Indoor);
        Assert.False(west.Indoor);
        Assert.False(graph.SameComponent("west", "east"));
        NavWorld.MarkIndoor(null, (_, _, _) => true);
    }

    [Fact]
    public void GraphFor_FacetWithoutAGraph_ReturnsNull()
    {
        // Another facet's graph is on another map. Walking to its coordinates is wrong.
        Assert.Null(NavWorld.GraphFor("NoSuchFacet"));
        Assert.Null(NavWorld.DestinationsFor("NoSuchFacet"));
    }

    [Fact]
    public void LeftoverPieces_SkipsLargestAndTakesNextByCount()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a1", 0, 0, 0, "a2", "a3"),
                Node("a2", 1, 0, 0),
                Node("a3", 2, 0, 0),
                Node("b1", 100, 0, 0, "b2"),
                Node("b2", 101, 0, 0),
                Node("c1", 200, 0, 0)
            ]
        );

        var leftovers = NavWorld.LeftoverPieces(graph, NavWorld.LeftoverPiecesToLog);

        Assert.Equal(2, leftovers.Count);
        Assert.Equal(2, leftovers[0].Count);
        Assert.Equal(new Point3D(100, 0, 0), leftovers[0].Sample);
        Assert.Equal(1, leftovers[1].Count);
        Assert.Equal(new Point3D(200, 0, 0), leftovers[1].Sample);
    }

    [Fact]
    public void LeftoverPieces_TakeLimitsResult()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a1", 0, 0, 0, "a2"),
                Node("a2", 1, 0, 0),
                Node("b1", 100, 0, 0),
                Node("c1", 200, 0, 0)
            ]
        );

        var leftovers = NavWorld.LeftoverPieces(graph, 1);

        Assert.Single(leftovers);
        Assert.Equal(1, leftovers[0].Count);
    }

    [Fact]
    public void LeftoverPieces_SingleComponent_ReturnsEmpty()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("a1", 0, 0, 0, "a2"),
                Node("a2", 1, 0, 0)
            ]
        );

        Assert.Empty(NavWorld.LeftoverPieces(graph, NavWorld.LeftoverPiecesToLog));
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
