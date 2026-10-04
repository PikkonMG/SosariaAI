using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class MoongateSeedsTests
{
    private const int NearTiles = 2;
    private const int MoonglowX = 4467;
    private const int MoonglowY = 1283;
    private const int MaginciaX = 3563;
    private const int MaginciaY = 2139;

    [Fact]
    public void LocationsFor_Felucca_IncludesMoonglowAndMagincia()
    {
        var locations = MoongateSeeds.LocationsFor(FacetNames.Felucca);

        Assert.Contains(locations, p => Near(p, MoonglowX, MoonglowY));
        Assert.Contains(locations, p => Near(p, MaginciaX, MaginciaY));
    }

    [Fact]
    public void Link_EmptyNodes_CreatesMoonCompleteGraph()
    {
        var nodes = new List<NavNode>();

        MoongateSeeds.Link(nodes, FacetNames.Felucca);

        Assert.NotEmpty(nodes);
        Assert.All(
            nodes,
            node => Assert.StartsWith(MoongateSeeds.NamePrefix, node.Name, StringComparison.Ordinal)
        );

        var graph = new NavGraph(FacetNames.Felucca, nodes);

        for (var i = 0; i < nodes.Count; i++)
        {
            for (var j = i + 1; j < nodes.Count; j++)
            {
                Assert.True(graph.SameComponent(nodes[i].Name, nodes[j].Name));
            }
        }

        Assert.True(nodes.Count >= 2);
        Assert.Equal(NavGateKind.Moongate, NavGates.KindBetween(nodes[0], nodes[1].Name));
        Assert.Equal(NavGateKind.Moongate, graph.GateKind(nodes[0].Name, nodes[1].Name));
    }

    [Fact]
    public void Link_PadsSitOnTheirExactTileWithDoorRange()
    {
        var locations = MoongateSeeds.LocationsFor(FacetNames.Felucca);
        var beside = new NavNode { Name = "beside", X = locations[0].X + 1, Y = locations[0].Y, Z = locations[0].Z };
        var nodes = new List<NavNode> { beside };

        MoongateSeeds.Link(nodes, FacetNames.Felucca);

        Assert.Empty(beside.Gates ?? []);

        foreach (var pad in locations)
        {
            var node = Assert.Single(nodes, n => n.X == pad.X && n.Y == pad.Y);
            Assert.Equal(NavLimits.DoorArrivalRange, node.ArrivalRange);
            Assert.NotEmpty(node.Gates);
        }
    }

    [Fact]
    public void LocationsFor_BlankFacet_ReturnsEmpty()
    {
        Assert.Empty(MoongateSeeds.LocationsFor(null));
        Assert.Empty(MoongateSeeds.LocationsFor(""));
        Assert.Empty(MoongateSeeds.LocationsFor("   "));
    }

    private static bool Near(Point3D point, int x, int y) =>
        Math.Abs(point.X - x) <= NearTiles && Math.Abs(point.Y - y) <= NearTiles;
}
