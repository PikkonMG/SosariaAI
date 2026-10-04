using System;
using System.Collections.Generic;
using System.IO;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class WorldGeneratorTests
{
    private const int FarX = 200;
    private const int TinyGraphLimit = 4;
    private const string WestName = "west";
    private const string EastName = "east";
    private const string LocationsFolder = "Locations";
    private const string JsonExtension = ".json";

    /// <summary>A server location file with one named spot, so a generation has a seed to walk to.</summary>
    private const string OneLocationJson =
        """
        { "name": "Felucca", "categories": [ { "name": "Towns", "locations": [ { "name": "west", "location": [0, 0, 0] } ] } ] }
        """;
    private const string MarkerName = "marker";
    private const string StreetName = "street";
    private const int GapStart = 20;
    private const int GapEnd = 60;
    private const int StreetY = 20;

    /// <summary>A facet with no moongate pads, so only the test seeds make nodes.</summary>
    private const string NoGateFacet = FacetNames.Malas;

    [Fact]
    public void BuildDestinations_WeaponsmithLocation_IsNotAForge()
    {
        var graph = SingleNodeGraph(590, 2205);
        IReadOnlyList<NamedSeed> seeds =
        [
            new("Honed Edge", "weaponsmith", FacetNames.Felucca, 590, 2205, 0),
            new("Iron Works", "blacksmith", FacetNames.Felucca, 591, 2205, 0)
        ];

        var catalog = WorldGenerator.BuildDestinations(graph, seeds, [], [], TestWalkers.AnyFloor);

        Assert.Equal("Weaponsmith", catalog.GetByName("Honed Edge")?.Role);
        Assert.Equal("Smith", catalog.GetByName("Iron Works")?.Role);
    }

    [Fact]
    public void BuildGraph_EmptySeeds_DoesNotThrow()
    {
        // Malas has no offline moongate fallback, so empty seeds stay empty.
        var graph = WorldGenerator.BuildGraph(FacetNames.Malas, [], [], null);

        Assert.NotNull(graph);
        Assert.True(graph.NodeCount < TinyGraphLimit);
    }

    [Fact]
    public void TryGenerate_MissingDataRoot_ReturnsFalse()
    {
        Assert.False(WorldGenerator.TryGenerate(FacetNames.Felucca, "", [], null, out var graph, out var catalog));
        Assert.Null(graph);
        Assert.Null(catalog);
    }

    [Fact]
    public void BuildGraph_FarWalkableSeeds_BecomeOneComponent()
    {
        Assert.True(FarX > GraphConnect.BridgeMax);

        var seeds = new GraphSeed[]
        {
            new(WestName, 0, 0, 0, string.Empty),
            new(EastName, FarX, 0, 0, string.Empty)
        };

        var graph = WorldGenerator.BuildGraph(FacetNames.Felucca, seeds, [], TestWalkers.Flat);

        var west = graph.FindNearest(new Point3D(0, 0, 0));
        var east = graph.FindNearest(new Point3D(FarX, 0, 0));

        Assert.NotNull(west);
        Assert.NotNull(east);
        Assert.True(graph.SameComponent(west.Name, east.Name));
        Assert.NotEmpty(NavSearch.FindPath(graph, west.Name, east.Name));
    }

    [Fact]
    public void BuildGraph_Teleporter_LinksFarNodesWithoutWalkableLine()
    {
        Assert.True(FarX > GraphConnect.BridgeMax);

        var seeds = new GraphSeed[]
        {
            new(WestName, 0, 0, 0, string.Empty),
            new(EastName, FarX, 0, 0, string.Empty)
        };
        TeleporterLink[] teleports = [new(FacetNames.Felucca, 0, 0, 0, FacetNames.Felucca, FarX, 0, 0, Back: true)];

        var graph = WorldGenerator.BuildGraph(FacetNames.Felucca, seeds, teleports, TestWalkers.From(EndpointsOnly));

        var west = graph.FindNearest(new Point3D(0, 0, 0));
        var east = graph.FindNearest(new Point3D(FarX, 0, 0));

        Assert.NotNull(west);
        Assert.NotNull(east);
        Assert.Equal(0, west.X);
        Assert.Equal(FarX, east.X);
        Assert.NotEqual(west.Name, east.Name);
        Assert.Contains(east.Name, graph.Neighbors(west.Name));
        Assert.Contains(west.Name, graph.Neighbors(east.Name));
        Assert.True(graph.SameComponent(west.Name, east.Name));
    }


    [Fact]
    public void TryGenerate_FailureInsideGeneration_Propagates()
    {
        // The caller logs the reason a facet has no graph; a swallowed failure hides it.
        var dataRoot = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(dataRoot, LocationsFolder));
        File.WriteAllText(Path.Combine(dataRoot, LocationsFolder, FacetNames.Felucca + JsonExtension), OneLocationJson);

        try
        {
            Assert.Throws<InvalidOperationException>(
                () => WorldGenerator.TryGenerate(
                    FacetNames.Felucca,
                    dataRoot,
                    [],
                    TestWalkers.From((_, _, _) => throw new InvalidOperationException("tiles are not loaded")),
                    out _,
                    out _
                )
            );
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildGraph_TeleporterPad_SitsOnItsExactTileBesideASeed()
    {
        const int padX = 3;
        var seeds = new GraphSeed[]
        {
            new(WestName, 0, 0, 0, string.Empty),
            new(EastName, FarX, 0, 0, string.Empty)
        };
        TeleporterLink[] teleports = [new(FacetNames.Felucca, padX, 0, 0, FacetNames.Felucca, FarX, 0, 0, Back: false)];

        var graph = WorldGenerator.BuildGraph(FacetNames.Felucca, seeds, teleports, TestWalkers.Flat);

        Assert.True(graph.TryGetNode($"{TeleporterLinking.AttachedNamePrefix}{padX}-0", out var pad));
        Assert.Equal(padX, pad.X);
        Assert.Equal(NavLimits.DoorArrivalRange, pad.ArrivalRange);
        Assert.True(graph.IsGate(pad.Name, graph.FindNearest(new Point3D(FarX, 0, 0)).Name));
        Assert.True(graph.SameComponent(pad.Name, graph.FindNearest(Point3D.Zero).Name));
        Assert.Contains(graph.Neighbors(pad.Name), name => !graph.IsGate(pad.Name, name));
    }

    [Fact]
    public void BuildGraph_MarkerPastAGap_LeavesNoLoneNode()
    {
        // A marker with no walk to the rest of the graph would be a goal no plan reaches.
        var seeds = new GraphSeed[]
        {
            new(WestName, 0, 0, 0, string.Empty),
            new(EastName, GapStart - 1, 0, 0, string.Empty),
            new(MarkerName, GapEnd + 1, 0, 0, string.Empty)
        };

        var graph = WorldGenerator.BuildGraph(NoGateFacet, seeds, [], TestWalkers.From(GapBetween));

        Assert.All(graph.Nodes, node => Assert.NotEmpty(node.Connects));
        Assert.DoesNotContain(graph.Nodes, node => node.X > GapEnd);
    }

    [Fact]
    public void BuildGraph_ClusterStandsNowhere_AnchorKeepsItsOwnNode()
    {
        // Three seeds chained eight tiles apart merge into one cluster whose averaged
        // point stands nowhere. The bank still gets a node on its own tile.
        var seeds = new GraphSeed[]
        {
            new(WestName, 0, 0, 0, nameof(DestinationKind.Bank)),
            new(EastName, SeedMerge.MergeChebyshev, 0, 0, string.Empty),
            new(MarkerName, SeedMerge.MergeChebyshev * 2, 0, 0, string.Empty),
            new(StreetName, 0, StreetY, 0, string.Empty)
        };

        var graph = WorldGenerator.BuildGraph(NoGateFacet, seeds, [], TestWalkers.From(ColumnsOnly));

        Assert.True(graph.TryGetNode($"{NoGateFacet}-0-0", out var bank));
        Assert.Equal(NavLimits.BankArrivalRange, bank.ArrivalRange);
        Assert.Contains(bank.Name, graph.Neighbors($"{NoGateFacet}-0-{StreetY}"));
    }

    [Fact]
    public void BuildDestinations_ShopLocation_BecomesAResolvableVendor()
    {
        var graph = SingleNodeGraph(590, 2205);
        IReadOnlyList<NamedSeed> seeds = [new("Bloody Bowman", "bowyer", FacetNames.Felucca, 590, 2205, 0)];

        var catalog = WorldGenerator.BuildDestinations(graph, seeds, [], [], TestWalkers.AnyFloor);

        var dest = catalog.Resolve("vendor:Bowyer", Point3D.Zero);
        Assert.NotNull(dest);
        Assert.Equal("Bloody Bowman", dest.Name);
        Assert.Equal("Bowyer", dest.Role);
    }

    [Fact]
    public void BuildDestinations_TavernLocation_AnswersTheTavernToken()
    {
        var graph = SingleNodeGraph(2000, 1000);
        IReadOnlyList<NamedSeed> seeds = [new("Salty Dog", "tavern", FacetNames.Felucca, 2000, 1000, 0)];

        var catalog = WorldGenerator.BuildDestinations(graph, seeds, [], [], TestWalkers.AnyFloor);

        var dest = catalog.Resolve("tavern", Point3D.Zero);
        Assert.NotNull(dest);
        Assert.Equal("Salty Dog", dest.Name);
        Assert.Equal("TavernKeeper", dest.Role);
    }

    [Fact]
    public void BuildDestinations_InnLocation_AnswersTheHealerAndTavernTokens()
    {
        var graph = SingleNodeGraph(2225, 2892);
        IReadOnlyList<NamedSeed> seeds = [new("The Hunted Stag", "inn", FacetNames.Felucca, 2225, 2892, 0)];

        var catalog = WorldGenerator.BuildDestinations(graph, seeds, [], [], TestWalkers.AnyFloor);

        var dest = catalog.Resolve("healer", Point3D.Zero);
        Assert.NotNull(dest);
        Assert.Equal("The Hunted Stag", dest.Name);
        Assert.NotNull(catalog.Resolve("tavern", Point3D.Zero));
    }

    [Fact]
    public void BuildDestinations_PairedShopSpawner_ResolvesEveryNamedRole()
    {
        // Dock shops spawn a shipwright and a mapmaker from one entry. Listing the
        // mapmaker second must not erase the role: cartographers look for it by name.
        var graph = SingleNodeGraph(1416, 1754);
        var spawners = SpawnerSeeds.Parse("""
            [
              {
                "$type": "Spawner",
                "location": [1416, 1754, 10],
                "map": "Felucca",
                "homeRange": 2,
                "entries": [
                  { "name": "Shipwright", "maxCount": 1, "probability": 100 },
                  { "name": "Mapmaker", "maxCount": 1, "probability": 100 }
                ]
              }
            ]
            """);

        var catalog = WorldGenerator.BuildDestinations(graph, [], [], spawners, TestWalkers.AnyFloor);

        Assert.Equal("Mapmaker", catalog.Resolve("vendor:Mapmaker", Point3D.Zero)?.Role);
        Assert.Equal("Shipwright", catalog.Resolve("vendor:Shipwright", Point3D.Zero)?.Role);
    }

    [Fact]
    public void BuildDestinations_UpstairsDraft_IsSkippedWhenItSitsOverTheGroundFloor()
    {
        // No walk from the ground floor node climbs to the keeper on the second floor,
        // so offering it only produces repeated "has no route" failures.
        var graph = SingleNodeGraph(1427, 1716);
        var spawners = SpawnerSeeds.Parse("""
            [
              {
                "$type": "Spawner",
                "location": [1427, 1716, 20],
                "map": "Felucca",
                "homeRange": 2,
                "entries": [ { "name": "TavernKeeper", "maxCount": 1, "probability": 100 } ]
              }
            ]
            """);

        var catalog = WorldGenerator.BuildDestinations(graph, [], [], spawners, TestWalkers.AnyFloor);

        Assert.Null(catalog.Resolve("tavern", Point3D.Zero));
    }

    [Fact]
    public void BuildDestinations_RaisedFloorDraft_SurvivesWhenTheGraphStandsAtThatHeight()
    {
        // The Britain stables sit on a hill: the keeper and the walk surface are both
        // 30 up, while the land under them reads 0. Judging height against the land
        // called them a first floor and dropped every animal trainer in the city.
        var graph = SingleNodeGraph(1388, 1655, 30);
        var spawners = SpawnerSeeds.Parse("""
            [
              {
                "$type": "Spawner",
                "location": [1388, 1655, 30],
                "map": "Felucca",
                "homeRange": 2,
                "entries": [ { "name": "AnimalTrainer", "maxCount": 1, "probability": 100 } ]
              }
            ]
            """);

        var catalog = WorldGenerator.BuildDestinations(graph, [], [], spawners, TestWalkers.AnyFloor);

        Assert.Equal("AnimalTrainer", catalog.Resolve("vendor:AnimalTrainer", Point3D.Zero)?.Role);
    }

    [Fact]
    public void BuildDestinations_HuntSpot_IsRatedAtBootNotHere()
    {
        // A hunt spot's difficulty is its floor's or its ground's, which needs the world's
        // dungeon regions: the boot rates it (GroundRating), so a saved old number never stays.
        var graph = SingleNodeGraph(1200, 900);
        var spawners = SpawnerSeeds.Parse("""
            [
              {
                "$type": "Spawner",
                "location": [1200, 900, 0],
                "map": "Felucca",
                "homeRange": 4,
                "entries": [
                  { "name": "Ettin", "maxCount": 2, "probability": 100 }
                ]
              }
            ]
            """);

        var catalog = WorldGenerator.BuildDestinations(graph, [], [], spawners, TestWalkers.AnyFloor);

        Assert.Null(HuntDifficulty(catalog));
    }

    private static int? HuntDifficulty(DestinationCatalog catalog)
    {
        foreach (var place in catalog.All)
        {
            if (place.ParsedKind == DestinationKind.Hunt)
            {
                return place.Difficulty;
            }
        }

        throw new Xunit.Sdk.XunitException("no hunt spot in the catalog");
    }

    private static NavGraph SingleNodeGraph(int x, int y) => SingleNodeGraph(x, y, 0);

    private static NavGraph SingleNodeGraph(int x, int y, int z) =>
        new(
            FacetNames.Felucca,
            [new NavNode { Name = "node", X = x, Y = y, Z = z, Connects = [] }]
        );

    private static int? GapBetween(int x, int y, int z) => x is >= GapStart and <= GapEnd ? null : z;

    /// <summary>Ground only on the column of the bank and the column of the far seed.</summary>
    private static int? ColumnsOnly(int x, int y, int z) =>
        x == 0 || x == SeedMerge.MergeChebyshev * 2 ? z : null;

    private static int? EndpointsOnly(int x, int y, int z) =>
        y == 0 && z == 0 && (x == 0 || x == FarX) ? z : null;
}
