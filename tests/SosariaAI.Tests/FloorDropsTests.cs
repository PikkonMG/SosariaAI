using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// The one-way pads that drop a walker onto a dungeon floor harder than the ground it leaves,
/// and the walkers who keep off them: those the floor does not fit. Walkers home and tamers took
/// the Despise and Fire pads onto Destard's third level.
/// </summary>
public class FloorDropsTests
{
    private const string Dungeon = "Testdun";
    private const int DungeonBlockX = 5120;
    private const string Weak = "Skeleton";
    private const string Strong = "Lich";
    private const int WeakThreat = 84;
    private const int StrongThreat = 300;
    private const int SpawnRange = 10;
    private const int SingleSpawn = 1;

    /// <summary>Below the reach the strong floor asks (<see cref="HuntGround.PowerBar"/>).</summary>
    private const int WeakWalkerPower = 100;

    /// <summary>A walker the strong floor fits.</summary>
    private const int StrongWalkerPower = StrongThreat;

    /// <summary>The weak floor fits it; the strong one does not.</summary>
    private const int MiddleWalkerPower = 200;

    /// <summary>
    /// The drops of a dungeon with every kind of pad: a one-way pad from the overland onto
    /// level 1, a one-way stair down onto the harder level 2, a one-way stair down from there
    /// onto the easier level 3, a one-way stair up from level 3 onto the harder level 2, a
    /// two-way stair from level 1 onto the hard level 4, and an exit pad out of level 2.
    /// </summary>
    [Fact]
    public void Drops_AreTheOneWayPadsOntoAHarderFloor_ThatLeadNoWayUp()
    {
        var map = Layered();

        Assert.Equal(
            [("door", "r1a"), ("r1b", "r2a")],
            map.Drops.Select(drop => (drop.Pad.Name, drop.Landing.Name)).ToList()
        );
        Assert.Equal(map.FloorOfNode("r2a")?.Id, map.Drops[1].ToFloor);
    }

    [Fact]
    public void AboveReach_BarsTheDropsOntoTheFloorsThatDoNotFit()
    {
        var map = Layered();
        var ontoLevelTwo = EdgeHealthRules.Key(map.Drops[1].Pad.Index, map.Drops[1].Landing.Index);
        var ontoLevelOne = EdgeHealthRules.Key(map.Drops[0].Pad.Index, map.Drops[0].Landing.Index);

        Assert.True(FloorDrops.AboveReach(map, power: 0)!.SetEquals([ontoLevelOne, ontoLevelTwo]));
        Assert.True(FloorDrops.AboveReach(map, MiddleWalkerPower)!.SetEquals([ontoLevelTwo]));
        Assert.Null(FloorDrops.AboveReach(map, StrongWalkerPower));
    }

    [Fact]
    public void BarsAboveReach_IsOneSharedObjectPerReach_AndNoneForAWalkerEveryFloorFits()
    {
        var map = Layered();
        var bars = FloorDrops.BarsAboveReach(map, MiddleWalkerPower);

        Assert.NotNull(bars);
        Assert.Same(bars, FloorDrops.BarsAboveReach(map, MiddleWalkerPower + 1));
        Assert.Same(bars.Pads, FloorDrops.AboveReach(map, MiddleWalkerPower));
        Assert.False(bars.KeepsOffGuards);
        Assert.Null(FloorDrops.BarsAboveReach(map, StrongWalkerPower));
    }

    /// <summary>
    /// The pad from the west onto a hard floor, whose exit pad sets a walker down in the east, is
    /// shorter than the road: a walker the floor does not fit takes the road, one it fits the pad.
    /// </summary>
    [Fact]
    public void Explore_AWalkerTheFloorDoesNotFit_TakesTheRoadRoundTheDrop()
    {
        var map = Shortcut(out var graph);
        var weak = Road(graph, FloorDrops.BarsAboveReach(map, WeakWalkerPower));

        Assert.Contains("westPad", Road(graph, bars: null));
        Assert.DoesNotContain("westPad", weak);
        Assert.Contains("road", weak);
        Assert.Contains("westPad", Road(graph, FloorDrops.BarsAboveReach(map, StrongWalkerPower)));
    }

    [Fact]
    public void BarsGate_APadBarredEitherWay_AndTheCrossingKeepsIt()
    {
        const int pad = 4;
        const int landing = 9;
        var pads = new HashSet<long> { EdgeHealthRules.Key(pad, landing) };
        var red = new PathSearchBars(new HashSet<int>(), Point3D.Zero, noMoongates: false, openAroundStart: true, pads: pads);

        Assert.True(red.BarsGate(NavGateKind.Teleporter, pad, landing));
        Assert.True(red.BarsGate(NavGateKind.Teleporter, landing, pad));
        Assert.False(red.BarsGate(NavGateKind.Teleporter, pad, landing + 1));
        Assert.True(red.KeepsOffGuards);
        Assert.Same(pads, red.CrossingGuards().Pads);
    }

    /// <summary>A walk out of nowhere, or with no floors read off its graph, keeps every pad.</summary>
    [Fact]
    public void ForWalker_WithoutFloorsOfTheGraph_BarsNoPad()
    {
        var graph = new NavGraph(FacetNames.Felucca, [Node("A", 0, 0)]);

        Assert.Null(PathSearchBars.ForWalker(null, graph, null, Point3D.Zero));
        Assert.False(DungeonMap.Empty.IsOf(graph));
    }

    private static DungeonMap Layered()
    {
        var town = Node("town", 1000, 900, "door");
        var door = Node("door", 1010, 900, "town");
        var r1a = Node("r1a", 5300, 900, "r1b");
        var r1b = Node("r1b", 5320, 900, "r1a");
        var r2a = Node("r2a", 5400, 1000, "r2b");
        var r2b = Node("r2b", 5420, 1000, "r2a");
        var r3a = Node("r3a", 5500, 1100, "r3b");
        var r3b = Node("r3b", 5520, 1100, "r3a");
        var r4a = Node("r4a", 5600, 1200);
        NavGates.AddOneWay(door, r1a, NavGateKind.Teleporter);
        NavGates.AddOneWay(r1b, r2a, NavGateKind.Teleporter);
        NavGates.AddOneWay(r2b, r3a, NavGateKind.Teleporter);
        NavGates.AddOneWay(r3b, r2b, NavGateKind.Teleporter);
        NavGates.Add(r1b, r4a, NavGateKind.Teleporter);
        NavGates.AddOneWay(r2a, town, NavGateKind.Teleporter);
        var graph = new NavGraph(FacetNames.Felucca, [town, door, r1a, r1b, r2a, r2b, r3a, r3b, r4a]);
        LevelMark[] marks = [new(r1a.Location, 1), new(r2a.Location, 2), new(r3a.Location, 3), new(r4a.Location, 4)];
        SpawnPoint[] spawn =
        [
            Spawn(r1a, Weak),
            Spawn(r2a, Strong),
            Spawn(r3a, Weak),
            Spawn(r4a, Strong)
        ];
        return DungeonMap.Build(graph, InDungeon, marks, spawn, Lookup);
    }

    private static DungeonMap Shortcut(out NavGraph graph)
    {
        var west = Node("west", 1000, 900, "westPad", "road");
        var westPad = Node("westPad", 1010, 900, "west");
        var road = Node("road", 1800, 900, "west", "east");
        var east = Node("east", 2600, 900, "road", "eastLanding");
        var eastLanding = Node("eastLanding", 2590, 900, "east");
        var hallA = Node("hallA", 5300, 900, "hallB");
        var hallB = Node("hallB", 5320, 900, "hallA");
        NavGates.AddOneWay(westPad, hallA, NavGateKind.Teleporter);
        NavGates.AddOneWay(hallB, eastLanding, NavGateKind.Teleporter);
        graph = new NavGraph(FacetNames.Felucca, [west, westPad, road, east, eastLanding, hallA, hallB]);
        return DungeonMap.Build(graph, InDungeon, marks: null, [Spawn(hallA, Strong)], Lookup);
    }

    /// <summary>The walk from the west to the east, searched from its goal as a trip plans it.</summary>
    private static IReadOnlyList<string> Road(NavGraph graph, PathSearchBars bars)
    {
        var cost = new Dictionary<string, double>();
        var prev = new Dictionary<string, string>();
        NavSearch.Explore(graph, "east", null, NavSearch.DefaultGateCost, null, null, cost, prev, bars);
        return NavSearch.Reconstruct(prev, "east", "west");
    }

    private static string InDungeon(NavNode node) => node.X >= DungeonBlockX ? Dungeon : null;

    private static SpawnPoint Spawn(NavNode at, string creature) => new(at.Location, SpawnRange, creature, SingleSpawn, Dungeon);

    private static HostileStats? Lookup(string name) =>
        name == Weak ? new HostileStats(WeakThreat, 0, 0) : name == Strong ? new HostileStats(StrongThreat, 0, 0) : null;

    private static NavNode Node(string name, int x, int y, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = 0,
            Connects = [.. connects]
        };
}
