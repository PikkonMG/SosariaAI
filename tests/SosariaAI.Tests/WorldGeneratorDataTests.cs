using System;
using System.Collections.Generic;
using System.IO;
using Server;
using SosariaAI.Admin;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests;

public class WorldGeneratorDataTests
{
    private const string DataFolder = "Data";
    private const string LocationsFolder = "Locations";
    private const string Parent = "..";

    private readonly ITestOutputHelper _output;

    public WorldGeneratorDataTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TryGenerate_FeluccaFromDistributionData_CoversBritainAndMinoc()
    {
        var dataRoot = FindDistributionData();
        Assert.False(string.IsNullOrEmpty(dataRoot), "ModernUO Distribution/Data was not found.");

        Assert.True(
            WorldGenerator.TryGenerate(FacetNames.Felucca, dataRoot, WorldSetupRules.T2ASpawnSets, TestWalkers.Flat, out var graph, out var catalog)
        );
        Assert.NotNull(graph);
        Assert.NotNull(catalog);
        _output.WriteLine(
            "Felucca nodes={0} destinations={1} components={2} largest={3:P0} edges={4}",
            graph.NodeCount,
            catalog.All.Count,
            CountComponents(graph),
            LargestShare(graph),
            CountEdges(graph)
        );
        Assert.All(graph.Nodes, node => Assert.True(node.Connects.Count > 0, $"{node.Name} stands alone"));

        var bank = graph.FindNearest(CharactersFile.DefaultBankSpot);
        Assert.NotNull(bank);

        var moonglow = graph.FindNearest(new Point3D(4467, 1283, 5));
        var magincia = graph.FindNearest(new Point3D(3563, 2139, 0));
        Assert.NotNull(moonglow);
        Assert.NotNull(magincia);
        Assert.True(graph.SameComponent(bank.Name, moonglow.Name));
        Assert.True(graph.SameComponent(bank.Name, magincia.Name));
        Assert.True(graph.SameComponent(moonglow.Name, magincia.Name));

        if (graph.IsGate(moonglow.Name, magincia.Name))
        {
            Assert.Equal(NavGateKind.Moongate, graph.GateKind(moonglow.Name, magincia.Name));
        }

        var minoc = catalog.GetByName("Minoc");
        Assert.NotNull(minoc);
        var minocNode = graph.FindNearest(minoc.Location);
        Assert.NotNull(minocNode);
        Assert.True(graph.SameComponent(bank.Name, minocNode.Name));
        Assert.NotEmpty(NavSearch.FindPath(graph, bank.Name, minocNode.Name));

        var despise = catalog.Resolve("despise", CharactersFile.DefaultBankSpot);
        Assert.NotNull(despise);
        Assert.Equal(DestinationKind.Dungeon, despise.ParsedKind);
        Assert.Contains(
            catalog.All,
            dest => dest.ParsedKind is DestinationKind.Dungeon or DestinationKind.Hunt &&
                    dest.Difficulty.GetValueOrDefault() > 0
        );

        var entryway = catalog.GetByName("Despise Entryway");
        Assert.NotNull(entryway);
        Assert.True(graph.SameComponent(despise.Node, entryway.Node));

        Assert.NotNull(catalog.Resolve("bank", CharactersFile.DefaultBankSpot));
        Assert.NotNull(catalog.Resolve("graveyard", CharactersFile.DefaultBankSpot));

        // Bowyer and mapmaker spawners are shops, not hunting grounds. Filed as Hunt
        // they resolved nothing for vendor: tokens, and fletchers and mapmakers outside
        // Britain walked for a fallback they could not reach.
        var bowyer = catalog.Resolve("vendor:Bowyer", CharactersFile.DefaultBankSpot);
        Assert.NotNull(bowyer);
        Assert.Equal(DestinationKind.Vendor, bowyer.ParsedKind);
        Assert.Equal("Bowyer", bowyer.Role);

        var mapmaker = catalog.Resolve("vendor:Mapmaker", CharactersFile.DefaultBankSpot);
        Assert.NotNull(mapmaker);
        Assert.Equal(DestinationKind.Vendor, mapmaker.ParsedKind);
        Assert.Equal("Mapmaker", mapmaker.Role);
    }

    [Fact]
    public void TryGenerate_TrammelFromDistributionData_IsOwnGraph()
    {
        var dataRoot = FindDistributionData();
        Assert.False(string.IsNullOrEmpty(dataRoot), "ModernUO Distribution/Data was not found.");

        Assert.True(
            WorldGenerator.TryGenerate(FacetNames.Trammel, dataRoot, WorldSetupRules.T2ASpawnSets, TestWalkers.Flat, out var graph, out var catalog)
        );
        Assert.NotNull(graph);
        Assert.True(graph.NodeCount > 0);
        Assert.Equal(FacetNames.Trammel, graph.Facet);
        Assert.NotNull(catalog);
        _output.WriteLine(
            "Trammel nodes={0} destinations={1} components={2} largest={3:P0} edges={4}",
            graph.NodeCount,
            catalog.All.Count,
            CountComponents(graph),
            LargestShare(graph),
            CountEdges(graph)
        );
        Assert.NotEqual(FacetNames.Felucca, graph.Facet);
    }

    private static int CountComponents(NavGraph graph)
    {
        var ids = new HashSet<int>();

        foreach (var node in graph.Nodes)
        {
            ids.Add(graph.ComponentOf(node.Name));
        }

        return ids.Count;
    }

    private static int CountEdges(NavGraph graph)
    {
        var connects = 0;

        foreach (var node in graph.Nodes)
        {
            connects += node.Connects?.Count ?? 0;
        }

        return connects / 2;
    }

    private static double LargestShare(NavGraph graph)
    {
        if (graph.NodeCount == 0)
        {
            return 0;
        }

        var sizes = new Dictionary<int, int>();

        foreach (var node in graph.Nodes)
        {
            var id = graph.ComponentOf(node.Name);
            sizes[id] = sizes.GetValueOrDefault(id) + 1;
        }

        var largest = 0;

        foreach (var size in sizes.Values)
        {
            if (size > largest)
            {
                largest = size;
            }
        }

        return (double)largest / graph.NodeCount;
    }

    private static string FindDistributionData()
    {
        var dir = AppContext.BaseDirectory;

        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.GetFullPath(Path.Combine(dir, "ModernUO", "Distribution", DataFolder));

            if (Directory.Exists(Path.Combine(candidate, LocationsFolder)))
            {
                return candidate;
            }

            var parent = Path.GetFullPath(Path.Combine(dir, Parent));

            if (parent == dir)
            {
                break;
            }

            dir = parent;
        }

        return null;
    }
}
