using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class CatalogBuilderTests
{
    [Fact]
    public void Build_AssignsNearestGraphNode()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("near", 0, 0, 0),
                Node("far", 50, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("Britain West Bank", "Bank", x: 4, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Single(catalog.All);
        Assert.Equal("near", catalog.All[0].Node);
        Assert.True(NavMetric.Distance(new Point3D(4, 0, 0), new Point3D(0, 0, 0)) <
                    NavMetric.Distance(new Point3D(4, 0, 0), new Point3D(50, 0, 0)));
    }

    [Fact]
    public void Build_Bank_ResolvesByItsKind()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("near", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Britain West Bank", "Bank", x: 0, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);
        var dest = catalog.Resolve("bank", new Point3D(0, 0, 0));

        Assert.NotNull(dest);
        Assert.Equal("Britain West Bank", dest.Name);
        Assert.Empty(dest.Aliases);
    }

    [Fact]
    public void Build_NullOrEmptyGraph_SkipsDrafts()
    {
        var drafts = new[]
        {
            Draft("Britain West Bank", "Bank", x: 0, y: 0)
        };

        Assert.Empty(CatalogBuilder.Build(drafts, null, TestWalkers.AnyFloor).All);
        Assert.Empty(CatalogBuilder.Build(drafts, new NavGraph(FacetNames.Felucca, []), TestWalkers.AnyFloor).All);
        Assert.Empty(CatalogBuilder.Build(null, new NavGraph(FacetNames.Felucca, [Node("near", 0, 0, 0)]), TestWalkers.AnyFloor).All);
        Assert.Empty(CatalogBuilder.Build(drafts, new NavGraph(FacetNames.Felucca, [Node("near", 0, 0, 0)]), walker: null).All);
    }

    [Fact]
    public void Build_AddsRoutineAndShopAliasesOnly()
    {
        // The words a routine names a place by, and the shop words, are aliases. The bank,
        // forest, mine, fish and smith words resolve by kind and role, so they need none.
        var graph = new NavGraph(FacetNames.Felucca, [Node("hub", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Britain West Bank", "Bank", x: 0, y: 0),
            Draft("Britain Graveyard", "Hunt", role: "Graveyard", x: 1, y: 0),
            Draft("Despise Entrance", "Dungeon", role: "Despise", x: 2, y: 0),
            Draft("Britain Forest", "Resource", role: "Lumber", x: 3, y: 0),
            Draft("Britain Mountain Mine", "Resource", role: "Mine", x: 4, y: 0),
            Draft("Britain River", "Resource", role: "Fish", x: 5, y: 0),
            Draft("Britain Blacksmith", "Vendor", role: "Smith", x: 6, y: 0),
            Draft("Britain Carpenter", "Vendor", role: "Carpenter", x: 7, y: 0),
            Draft("Britain Fisherman", "Vendor", role: "Fisherman", x: 8, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Empty(catalog.GetByName("Britain West Bank").Aliases);
        Assert.Equal(["graveyard"], catalog.GetByName("Britain Graveyard").Aliases);
        Assert.Equal(["despise"], catalog.GetByName("Despise Entrance").Aliases);
        Assert.Empty(catalog.GetByName("Britain Forest").Aliases);
        Assert.Empty(catalog.GetByName("Britain Mountain Mine").Aliases);
        Assert.Empty(catalog.GetByName("Britain River").Aliases);
        Assert.Empty(catalog.GetByName("Britain Blacksmith").Aliases);
        Assert.Equal(["carpenter"], catalog.GetByName("Britain Carpenter").Aliases);
        Assert.Equal(["fisherman"], catalog.GetByName("Britain Fisherman").Aliases);
        Assert.Equal("Britain West Bank", catalog.Resolve("bank", new Point3D(0, 0, 0)).Name);
        Assert.Equal("Britain Blacksmith", catalog.Resolve("blacksmith", new Point3D(0, 0, 0)).Name);
        Assert.Equal("Britain Carpenter", catalog.Resolve("carpenter", new Point3D(0, 0, 0)).Name);
        Assert.Equal("Britain Fisherman", catalog.Resolve("fisherman", new Point3D(0, 0, 0)).Name);
    }

    [Fact]
    public void Build_CopiesDifficulty()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("hub", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Despise Entrance", "Dungeon", role: "Despise", x: 0, y: 0, difficulty: 120)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Equal(120, catalog.All[0].Difficulty);
    }

    [Fact]
    public void Build_HealerInn_StillGetsTavernAlias()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("hub", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Britain Warrior's Inn", "Healer", role: "Inn", x: 0, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);
        var dest = catalog.Resolve("tavern", new Point3D(0, 0, 0));

        Assert.NotNull(dest);
        Assert.Equal("Britain Warrior's Inn", dest.Name);
        Assert.Equal(["tavern", "pub"], dest.Aliases);
        Assert.NotNull(catalog.Resolve("pub", new Point3D(0, 0, 0)));
    }

    [Fact]
    public void Build_PublicLibrary_IsNotATavern()
    {
        // "Public" contains "pub". A library named so was aliased as tavern, and an inn
        // try walked into the library.
        var graph = new NavGraph(FacetNames.Felucca, [Node("hub", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Britain Public Library", "Hunt", role: "Britain Public Library", x: 0, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Single(catalog.All);
        Assert.DoesNotContain("tavern", catalog.All[0].Aliases);
        Assert.DoesNotContain("pub", catalog.All[0].Aliases);
        Assert.Null(catalog.Resolve("tavern", new Point3D(0, 0, 0)));
    }

    [Fact]
    public void Build_IslandedSeed_AttachesToNearestRoutableNode()
    {
        // A bank NPC seeded on a node with no edges (a walled interior). The draft
        // must attach to the closest node a walk plan can reach, not the dead node.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("street", 0, 0, 0, connects: ["corner"]),
                Node("corner", -10, 0, 0),
                Node("bank-inside", 10, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("Banker 2881-684", "Bank", x: 10, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Single(catalog.All);
        Assert.Equal("street", catalog.All[0].Node);
        Assert.Equal(new Point3D(10, 0, 0), catalog.All[0].Arrival);
    }

    [Fact]
    public void Build_SeedAboveReachableFloor_IsDropped()
    {
        // An upstairs barkeep: the closest nodes a walk plan can reach are a full
        // floor down, and no walk from them climbs to it. Offering it only fails.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("street", 0, 0, 0, connects: ["corner"]),
                Node("corner", 20, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("TavernKeeper 1427-1716", "Vendor", role: "TavernKeeper", x: 4, y: 0, z: 20)
        };

        Assert.Empty(CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor).All);
    }

    [Fact]
    public void Build_UpstairsSeed_OnRoutableFloor_IsKept()
    {
        // A z = 20 floor that is itself connected (a walkable roof or deck) still
        // gets its destination.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("roof-a", 0, 0, 20, connects: ["roof-b"]),
                Node("roof-b", 10, 0, 20)
            ]
        );
        var drafts = new[]
        {
            Draft("Deck Vendor", "Vendor", x: 4, y: 0, z: 20)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Single(catalog.All);
        Assert.Equal("roof-a", catalog.All[0].Node);
    }

    [Fact]
    public void Build_UpstairsTavern_OnAWalkedFloor_IsKept()
    {
        // Live Britain: InnKeeper 1493-1616 at z = 20 on a second floor. The generator
        // walks every edge, so a floor node joined to the street is one a walk reaches
        // by its stairs, and the keeper is a tavern like any other.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("street", 0, 0, 0, connects: ["floor2", "corner"]),
                Node("floor2", 10, 0, 20, connects: ["street"]),
                Node("corner", -10, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("InnKeeper 1493-1616", "Healer", role: "InnKeeper", x: 10, y: 0, z: 20)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor, isIndoor: (_, _, _) => true);

        Assert.Equal("floor2", Assert.Single(catalog.All).Node);
        Assert.NotNull(catalog.Resolve("tavern", Point3D.Zero));
    }

    [Fact]
    public void Build_UpstairsIndoorNode_IsSkippedForStreet()
    {
        // An upstairs floor node sits a tile from the draft, which stands on the
        // ground. The draft must attach to the street node a walk reaches on its floor.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("street", 0, 0, 0, connects: ["floor2", "corner"]),
                Node("floor2", 6, 0, 20, indoor: true),
                Node("corner", -10, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("Shopkeeper", "Vendor", x: 5, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor);

        Assert.Single(catalog.All);
        Assert.Equal("street", catalog.All[0].Node);
    }

    [Fact]
    public void Build_WallBetweenNearestNodeAndSeed_AttachesToTheNodeAWalkReaches()
    {
        // A wall runs the length of the map at x = 3, between the seed and the nearest
        // node. No walk from that node arrives, so the node on the seed's side wins.
        const int wallX = 3;
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("behind-wall", 0, 0, 0, connects: ["open-side"]),
                Node("open-side", 13, 0, 0, connects: ["behind-wall"])
            ]
        );
        var drafts = new[]
        {
            Draft("Shopkeeper", "Vendor", x: 6, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.Ground(static (x, _, _) => x != wallX, null));

        Assert.Equal("open-side", Assert.Single(catalog.All).Node);
    }

    [Fact]
    public void Build_InnAttachedToDistantNode_IsDropped()
    {
        // Live Sweet Dreams (1493,1619) snapped to Felucca-1512-1618, a mine
        // hill 19 tiles east. Street-to-inn walks then had no route.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("mine-hill", 19, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("Sweet Dreams", "Healer", role: "Inn", x: 0, y: 0)
        };

        Assert.Empty(CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor).All);
    }

    [Fact]
    public void Build_IndoorSeedWithNoStandableTile_IsDropped()
    {
        // The space under a raised floor: indoors, and no tile in reach of the
        // seed can be stood on, so the arrival leg can never finish.
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("street", 0, 0, 0, connects: ["corner"]),
                Node("corner", -10, 0, 0)
            ]
        );
        var drafts = new[]
        {
            Draft("Guild Vendor", "Vendor", x: 4, y: 0)
        };

        var catalog = CatalogBuilder.Build(
            drafts,
            graph,
            TestWalkers.From(static (_, _, _) => null),
            isIndoor: (_, _, _) => true
        );

        Assert.Empty(catalog.All);
    }

    [Fact]
    public void Build_IndoorSeedWithStandableFloor_IsKept()
    {
        // A vendor behind a counter: the counter tile cannot be stood on, but
        // the floor beside it can, so the arrival leg still finishes.
        var graph = new NavGraph(FacetNames.Felucca, [Node("street", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Shopkeeper", "Vendor", x: 4, y: 0)
        };

        var catalog = CatalogBuilder.Build(
            drafts,
            graph,
            TestWalkers.From(static (x, y, z) => x != 4 || y != 0 ? z : null),
            isIndoor: (_, _, _) => true
        );

        Assert.Single(catalog.All);
    }

    [Fact]
    public void Build_SeedRingedByIslands_IsDropped()
    {
        // More islanded nodes than the candidate window sits between the seed and
        // the main component: nothing a plan can reach is near, so the draft drops.
        var nodes = new List<NavNode>
        {
            Node("street", 0, 0, 0, connects: ["corner"]),
            Node("corner", -10, 0, 0)
        };

        for (var i = 0; i < Traveler.StartCandidates; i++)
        {
            nodes.Add(Node($"island-{i}", 10 + i, 0, 0));
        }

        var graph = new NavGraph(FacetNames.Felucca, nodes);
        var drafts = new[]
        {
            Draft("Deep Seed", "Vendor", x: 16, y: 0)
        };

        Assert.Empty(CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor).All);
    }

    [Fact]
    public void Build_ShrineAcrossTheSea_IsDropped()
    {
        // The Shrine of Honesty stands on an island. The nearest node a walk plan
        // reaches is on the mainland, far more than one pathfinder leg away.
        var graph = new NavGraph(FacetNames.Felucca, [Node("shore", 0, 0, 0)]);
        var drafts = new[]
        {
            Draft("Honesty", "Shrine", x: NavLimits.MaxLegDistance + 1, y: 0)
        };

        Assert.Empty(CatalogBuilder.Build(drafts, graph, TestWalkers.AnyFloor).All);
    }

    // A mainland street far from an island whose own piece holds one node, as the Valor
    // island's piece holds its shrine: no land and no pad leads there.
    private const int IslandX = 200;
    private const int IslandSeedOffset = 4;

    private static NavGraph MainlandAndIsland() =>
        new(
            FacetNames.Felucca,
            [
                Node("street", 0, 0, 0, connects: ["corner"]),
                Node("corner", -10, 0, 0),
                Node("isle", IslandX, 0, 0)
            ]
        );

    [Fact]
    public void Build_ShrineOnAnIslandPiece_AttachesToTheIslandNode()
    {
        // A ghost that falls on the island can walk to its shrine, and has no other ankh.
        var drafts = new[]
        {
            Draft("Valor", "Shrine", x: IslandX + IslandSeedOffset, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, MainlandAndIsland(), TestWalkers.AnyFloor);

        Assert.Equal("isle", Assert.Single(catalog.All).Node);
    }

    [Fact]
    public void Build_VendorOnAnIslandPiece_IsDropped()
    {
        // Every other kind is a trip from the mainland, which no plan finishes.
        var drafts = new[]
        {
            Draft("Island Vendor", "Vendor", x: IslandX + IslandSeedOffset, y: 0)
        };

        Assert.Empty(CatalogBuilder.Build(drafts, MainlandAndIsland(), TestWalkers.AnyFloor).All);
    }

    [Fact]
    public void Build_MarkerHeight_TakesTheFloorFound()
    {
        // A marker carries height 0; the walker stands on the street at 7.
        const int streetZ = 7;
        var graph = new NavGraph(FacetNames.Felucca, [Node("street", 0, 0, streetZ)]);
        var drafts = new[]
        {
            Draft("Moonglow Healer", "Healer", x: 2, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, TestWalkers.From(static (_, _, _) => streetZ));

        Assert.Equal(streetZ, Assert.Single(catalog.All).Z);
    }

    [Fact]
    public void Build_MarkerFarBelowTheOnlyFloor_TakesTheNodesFloor()
    {
        // Hythloth's gate chamber stands at 64 and its map marker reads 0: no floor stands
        // near 0, so the arrival ends on the floor level with the node beside it.
        const int chamberZ = 64;
        var graph = new NavGraph(FacetNames.Felucca, [Node("chamber", 0, 0, chamberZ)]);
        var drafts = new[]
        {
            Draft("Hythloth", "Dungeon", x: 2, y: 0)
        };

        var catalog = CatalogBuilder.Build(drafts, graph, ColumnWorld.Walker(static (_, _) => [chamberZ]));

        var hythloth = Assert.Single(catalog.All);
        Assert.Equal(chamberZ, hythloth.Z);
        Assert.Equal("chamber", hythloth.Node);
    }

    private static DestinationDraft Draft(
        string name,
        string kind,
        string role = null,
        int x = 0,
        int y = 0,
        int z = 0,
        int? difficulty = null
    ) =>
        new()
        {
            Name = name,
            Kind = kind,
            Role = role,
            X = x,
            Y = y,
            Z = z,
            Difficulty = difficulty
        };

    private static NavNode Node(
        string name,
        int x,
        int y,
        int z,
        string[] connects = null,
        bool indoor = false
    ) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            Indoor = indoor,
            Connects = connects == null ? [] : [.. connects]
        };
}
