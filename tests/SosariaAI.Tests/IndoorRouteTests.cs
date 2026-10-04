using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class IndoorRouteTests
{
    private const int ShopX = 1469;
    private const int ShopY = 1668;
    private const int ShopZ = 0;
    private const int ShopWest = 1464;
    private const int ShopEast = 1476;
    private const int ShopNorth = 1660;
    private const int ShopSouth = 1674;

    /// <summary>Street points from the Britain mine down through the north of town to the bank.</summary>
    private static readonly Point3D[] MountainPath =
    [
        new(1420, 1535, 27),
        new(1421, 1560, 30),
        new(1433, 1585, 20),
        new(1422, 1609, 20),
        new(1419, 1634, 20),
        new(1410, 1659, 10),
        new(1414, 1684, 0)
    ];

    [Fact]
    public void Join_DoesNotUseAShopAsAShortcut()
    {
        var north = Node("north", 10, 0, 0);
        var shop = Node("shop", 10, 10, 0, indoor: true);
        var south = Node("south", 10, 20, 0);
        var nodes = new List<NavNode> { north, shop, south };

        GraphConnect.Connect(nodes, TestWalkers.AnyFloor, IsShopTile);

        Assert.Contains("south", north.Connects);
        Assert.Single(shop.Connects);
        Assert.True(shop.Connects.Contains("north") || shop.Connects.Contains("south"));
    }

    [Fact]
    public void FindPath_OutdoorToOutdoor_SkipsTheShop()
    {
        var north = Node("north", 10, 0, 0, "shop", "south");
        var shop = Node("shop", 10, 10, 0, indoor: true, "north", "south");
        var south = Node("south", 10, 20, 0, "shop", "north");
        var graph = new NavGraph(FacetNames.Felucca, [north, shop, south]);

        var path = NavSearch.FindPath(graph, "north", "south");

        Assert.Equal(["north", "south"], path);
        Assert.DoesNotContain("shop", path);
    }

    [Fact]
    public void FindPath_ToTheShop_GoesThroughTheDoor()
    {
        var street = Node("street", 10, 0, 0, "shop");
        var shop = Node("shop", 10, 10, 0, indoor: true, "street");
        var graph = new NavGraph(FacetNames.Felucca, [street, shop]);

        var path = NavSearch.FindPath(graph, "street", "shop");

        Assert.Equal(["street", "shop"], path);
    }

    [Fact]
    public void Plan_MineToBritainBank_HasNoIndoorPassThrough()
    {
        var seeds = new List<GraphSeed>
        {
            new("mine", CharactersFile.MiraMineApproach.X, CharactersFile.MiraMineApproach.Y,
                CharactersFile.MiraMineApproach.Z, string.Empty),
            new("bank", CharactersFile.DefaultBankSpot.X, CharactersFile.DefaultBankSpot.Y,
                CharactersFile.DefaultBankSpot.Z, string.Empty),
            new("shop", ShopX, ShopY, ShopZ, nameof(DestinationKind.Vendor)),
            new("street-s", 1467, 1686, 0, string.Empty),
            new("street-w", 1448, 1698, 0, string.Empty),
            new("north", 1475, 1645, 20, string.Empty)
        };

        for (var i = 0; i < MountainPath.Length; i++)
        {
            var point = MountainPath[i];
            seeds.Add(new GraphSeed($"via-{i}", point.X, point.Y, point.Z, string.Empty));
        }

        var graph = WorldGenerator.BuildGraph(
            FacetNames.Felucca,
            seeds,
            [],
            TestWalkers.Flat,
            isIndoor: IsShopTile
        );

        var mine = graph.FindNearest(CharactersFile.MiraMineApproach);
        var bank = graph.FindNearest(CharactersFile.DefaultBankSpot);
        Assert.NotNull(mine);
        Assert.NotNull(bank);

        var path = NavSearch.FindPath(graph, mine.Name, bank.Name);
        Assert.NotEmpty(path);

        for (var i = 0; i < path.Count; i++)
        {
            Assert.True(graph.TryGetNode(path[i], out var node));
            var isEnd = i == 0 || i == path.Count - 1;
            Assert.False(node.Indoor && !isEnd, $"{node.Name} at {node.Location} is an indoor pass-through");
        }

        Assert.True(graph.TryGetNode(path[^1], out var last));
        Assert.False(last.Indoor);
    }

    [Fact]
    public void Repair_OldShopHub_KeepsTheTownJoinedButNotThroughTheShop()
    {
        var street = Node("street", 1467, 1686, 0, "shop");
        var north = Node("north", 1475, HillTopY, HillTopZ, "shop");
        var shop = Node("shop", ShopX, ShopY, ShopZ, "street", "north");
        var nodes = new List<NavNode> { street, north, shop };

        IndoorRoute.Repair(nodes, IsShopTile, TestWalkers.Ground(static (_, _, _) => true, Hill));

        Assert.True(shop.Indoor);
        Assert.Contains("street", shop.Connects);
        Assert.Contains("north", shop.Connects);

        var path = NavSearch.FindPath(new NavGraph(FacetNames.Felucca, nodes), "street", "north");
        Assert.NotEmpty(path);
        Assert.DoesNotContain("shop", path);
    }

    [Fact]
    public void Components_ShopBetweenTwoStreets_DoesNotJoinThem()
    {
        var west = Node("west", 0, 0, 0, "shop");
        var east = Node("east", 20, 0, 0, "shop");
        var shop = Node("shop", 10, 0, 0, indoor: true, "west", "east");
        var graph = new NavGraph(FacetNames.Felucca, [west, east, shop]);

        Assert.False(graph.SameComponent("west", "east"));
        Assert.True(graph.SameComponent("shop", "west") || graph.SameComponent("shop", "east"));
    }

    [Fact]
    public void AttachDoors_LinkedShop_KeepsItsLinks()
    {
        var street = Node("street", 10, 0, 0, "shop");
        var yard = Node("yard", 10, 20, 0, "shop");
        var shop = Node("shop", 10, 10, 0, indoor: true, "street", "yard");

        GraphConnect.AttachDoors([street, yard, shop], TestWalkers.AnyFloor);

        Assert.Equal(2, shop.Connects.Count);
    }

    private const int RoofX = 8;
    private const int FarPadX = 900;

    /// <summary>A roof column further from both ends than a tile route may run indoors.</summary>
    private const int LongRoofX = 20;

    private const int LongRoofEastX = 2 * LongRoofX;

    private static bool RoofMidway(int x, int y, int z) => x == RoofX;

    [Fact]
    public void DropIndoorShortcuts_WalkUnderARoof_IsCut()
    {
        var west = Node("west", 0, 0, 0, "east");
        var east = Node("east", 2 * RoofX, 0, 0, "west");

        GraphConnect.DropIndoorShortcuts([west, east], TestWalkers.Flat, RoofMidway);

        Assert.DoesNotContain("east", west.Connects);
        Assert.DoesNotContain("west", east.Connects);
    }

    [Fact]
    public void DropIndoorShortcuts_OpenWalkAndGateHop_AreKept()
    {
        // The open walk stays outside. The gate hop crosses the roof, but a gate is
        // travel, not a walk.
        var west = Node("west", 0, 0, 0, "north");
        var north = Node("north", 0, RoofX, 0, "west");
        var pad = Node("pad", 0, 0, 0);
        var farPad = Node("far-pad", FarPadX, 0, 0);
        NavGates.Add(pad, farPad, NavGateKind.Moongate);

        GraphConnect.DropIndoorShortcuts([west, north, pad, farPad], TestWalkers.Flat, RoofMidway);

        Assert.Contains("north", west.Connects);
        Assert.Contains("far-pad", pad.Connects);
    }

    [Fact]
    public void Repair_NodeTheRoofCutLeavesAlone_IsRemoved()
    {
        // The roof covers the whole column between the two, so the cut edge has no way
        // round. A node left with no walking edge is a goal no plan reaches: the finished
        // graph must not keep it. Finishing a new graph after its catalog was built left
        // 321 such nodes on Felucca.
        var west = Node("west", 0, 0, 0, "east", "north");
        var north = Node("north", 0, RoofX, 0, "west");
        var east = Node("east", LongRoofEastX, 0, 0, "west");
        var nodes = new List<NavNode> { west, north, east };

        IndoorRoute.Repair(nodes, static (x, _, _) => x == LongRoofX, TestWalkers.Flat);

        Assert.DoesNotContain(east, nodes);
        Assert.All(nodes, node => Assert.True(GraphConnect.HasWalkLink(node), $"{node.Name} has no walking edge"));
    }

    [Fact]
    public void BuildGraph_IsFinished_PadUnderARoofIsIndoor()
    {
        // The generator returns the final graph: the catalog is built on it, so the roof
        // marks and cuts cannot wait for a later pass.
        var seeds = new List<GraphSeed>
        {
            new("west", 0, 0, 0, string.Empty),
            new("north", 0, RoofX, 0, string.Empty)
        };
        TeleporterLink[] teleports = [new(FacetNames.Felucca, RoofX, 0, 0, FacetNames.Felucca, FarPadX, 0, 0, Back: true)];

        var graph = WorldGenerator.BuildGraph(FacetNames.Felucca, seeds, teleports, TestWalkers.Flat, isIndoor: RoofMidway);

        Assert.True(graph.TryGetNode(TeleporterLinking.AttachedNamePrefix + RoofX + "-0", out var pad));
        Assert.True(pad.Indoor);
    }

    [Fact]
    public void Repair_OnlyTheRoofTestMarksIndoor()
    {
        // An open forge and a wilderness healer are vendors with no roof. Marking their
        // nodes indoor cut the street they stand on.
        var forge = Node("forge", 1500, 1700, 0, "street");
        var shop = Node("shop", ShopX, ShopY, ShopZ, "street");
        var street = Node("street", 1467, 1686, 0, "forge", "shop");
        var nodes = new List<NavNode> { forge, shop, street };

        IndoorRoute.Repair(nodes, IsShopTile, TestWalkers.AnyFloor);

        Assert.Contains(forge, nodes);
        Assert.Contains(shop, nodes);
        Assert.False(forge.Indoor);
        Assert.True(shop.Indoor);
    }

    // North of the shop the street climbs a hill to the north node, a step at a time.
    private const int HillTopY = 1645;
    private const int HillTopZ = 20;

    private static int Hill(int x, int y) =>
        y >= ShopNorth ? 0 : Math.Min(HillTopZ, (ShopNorth - y) * HillTopZ / (ShopNorth - HillTopY));

    private static bool IsShopTile(int x, int y, int z) =>
        x >= ShopWest && x <= ShopEast && y >= ShopNorth && y <= ShopSouth && z == ShopZ;

    private static NavNode Node(string name, int x, int y, int z, params string[] connects) =>
        Node(name, x, y, z, indoor: false, connects);

    private static NavNode Node(string name, int x, int y, int z, bool indoor, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = z,
            Indoor = indoor,
            Connects = [.. connects]
        };
}
