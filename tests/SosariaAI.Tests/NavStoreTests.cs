using System;
using System.IO;
using System.Text.Json;
using Server.Json;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class NavStoreTests
{
    private const int LongConnectTiles = 200;
    private const string NearName = "near";
    private const string FarName = "far";

    [Fact]
    public void FromFile_LongConnect_BecomesTeleporterGate()
    {
        var file = LongConnectFile();

        var graph = NavStore.FromFile(file, FacetNames.Felucca);

        Assert.NotNull(graph);
        Assert.True(graph.SameComponent(NearName, FarName));
        Assert.True(graph.IsGate(NearName, FarName));
        Assert.Equal(NavGateKind.Teleporter, graph.GateKind(NearName, FarName));
        Assert.Contains(file.Nodes[0].Gates, gate => gate.To == FarName);
        Assert.Contains(file.Nodes[1].Gates, gate => gate.To == NearName);
        Assert.Contains(FarName, file.Nodes[0].Connects);
        Assert.Contains(NearName, file.Nodes[1].Connects);
    }

    [Fact]
    public void FromFile_DiagonalWithinTheTileLeg_StaysAWalk()
    {
        // The leg cap counts tiles. A diagonal link whose straight line is longer than the cap
        // still fits the pathfinder's box, so no pad is made for it.
        const int diagonal = NavLimits.SoftLegDistance;
        var file = new NavFile
        {
            Format = NavFile.CurrentFormat,
            Facet = FacetNames.Felucca,
            Nodes =
            [
                Node(NearName, 0, 0, 0, FarName),
                Node(FarName, diagonal, diagonal, 0, NearName)
            ]
        };

        var graph = NavStore.FromFile(file, FacetNames.Felucca);

        Assert.True(NavMetric.Planar(file.Nodes[0].Location, file.Nodes[1].Location) > NavLimits.MaxLegDistance);
        Assert.False(graph.IsGate(NearName, FarName));
        Assert.All(file.Nodes, node => Assert.Null(node.Gates));
    }

    [Fact]
    public void FromFile_MissingFormatAndGates_DoesNotThrow()
    {
        var json = """
            {"facet":"felucca","nodes":[{"name":"near","x":0,"y":0,"z":0,"connects":["far"]},{"name":"far","x":200,"y":0,"z":0,"connects":["near"]}]}
            """;
        var file = JsonSerializer.Deserialize<NavFile>(json, JsonConfig.DefaultOptions);

        var graph = NavStore.FromFile(file, FacetNames.Felucca);

        Assert.NotNull(file);
        Assert.Equal(0, file.Format);
        Assert.NotNull(graph);
        Assert.True(graph.SameComponent(NearName, FarName));
        Assert.True(graph.IsGate(NearName, FarName));
    }

    [Fact]
    public void DestinationFile_OldCityKey_StillLoads()
    {
        // Older saved catalogs wrote a city on every destination. Nothing reads it now,
        // and a file that still carries it must load as before.
        var json = """
            {"facet":"felucca","destinations":[{"name":"Britain West Bank","kind":"Bank","city":"Britain","x":1434,"y":1699,"z":0,"node":"bank"}]}
            """;

        var file = JsonSerializer.Deserialize<DestinationFile>(json, JsonConfig.DefaultOptions);

        var destination = Assert.Single(file.Destinations);
        Assert.Equal("Britain West Bank", destination.Name);
        Assert.Equal("bank", destination.Node);
        Assert.DoesNotContain("city", JsonConfig.Serialize(file));
    }

    [Fact]
    public void FromFile_NullNodes_ReturnsNull()
    {
        var file = new NavFile { Facet = FacetNames.Felucca, Nodes = null };

        Assert.Null(NavStore.FromFile(file, FacetNames.Felucca));
    }

    [Fact]
    public void FromFile_NoFacetInFile_TakesTheFacetAsked()
    {
        var file = LongConnectFile();
        file.Facet = null;

        Assert.Equal(FacetNames.Trammel, NavStore.FromFile(file, FacetNames.Trammel).Facet);
    }

    [Fact]
    public void Serialize_LongConnectFile_RoundTripsThenGates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-nav-{Guid.NewGuid():N}.json");

        try
        {
            JsonConfig.Serialize(path, LongConnectFile());
            var loaded = JsonConfig.Deserialize<NavFile>(path);

            Assert.NotNull(loaded);
            Assert.Equal(NavFile.CurrentFormat, loaded.Format);
            Assert.All(loaded.Nodes, node => Assert.Null(node.Gates));

            var graph = NavStore.FromFile(loaded, FacetNames.Felucca);

            Assert.NotNull(graph);
            Assert.True(graph.SameComponent(NearName, FarName));
            Assert.True(graph.IsGate(NearName, FarName));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void CurrentFormat_IsTwo_AndSavePayloadUsesIt()
    {
        Assert.Equal(2, NavFile.CurrentFormat);

        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node(NearName, 0, 0, 0, FarName),
                Node(FarName, LongConnectTiles, 0, 0, NearName)
            ]
        );
        var file = new NavFile
        {
            Format = NavFile.CurrentFormat,
            Facet = graph.Facet,
            Nodes = [.. graph.Nodes]
        };
        var json = JsonConfig.Serialize(file);
        var roundTrip = JsonSerializer.Deserialize<NavFile>(json, JsonConfig.DefaultOptions);

        Assert.NotNull(roundTrip);
        Assert.Equal(NavFile.CurrentFormat, roundTrip.Format);
    }

    private static NavFile LongConnectFile() =>
        new()
        {
            Format = NavFile.CurrentFormat,
            Facet = FacetNames.Felucca,
            Nodes =
            [
                Node(NearName, 0, 0, 0, FarName),
                Node(FarName, LongConnectTiles, 0, 0, NearName)
            ]
        };

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
