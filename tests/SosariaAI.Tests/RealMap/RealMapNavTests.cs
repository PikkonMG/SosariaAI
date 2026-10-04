using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Server;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>Nav generation and the staff panel run on the real Felucca tiles.</summary>
[Collection(RealMapCollection.Name)]
public class RealMapNavTests
{
    private const string TeleportersFile = "teleporters.json";
    private const int DoorReachTiles = 3;
    /// <summary>
    /// Nodes in the largest piece. The coarse spread alone left 91.6% of the nodes there and
    /// Cove apart; the tile walk through the pieces it missed brought it to 95.6%, 17,639 of
    /// 18,450. The join where walks meet also lays roads on islands no walk or pad from a town
    /// reaches (Nujel'm, Ocllo, the Valor island), which lowers the share, so the floor counts
    /// the nodes. Built from the server's own data with no marker files, the largest piece
    /// holds 16,989 of 18,313 nodes (92.8%).
    /// </summary>
    private const int LargestNodesFloor = 16_800;

    private static readonly Point3D DespiseDoor = new(1296, 1081, 0);

    /// <summary>A Cove street node inside the town wall, whose gate bends between two walls.</summary>
    private static readonly Point3D CoveStreet = new(2236, 1208, 0);

    /// <summary>Two main-graph street nodes south of the Despise pass, on the Britain side.</summary>
    private static readonly Point3D BritainSideA = new(1240, 1317, 0);

    private static readonly Point3D BritainSideB = new(1241, 1317, 0);

    /// <summary>The sewer grate in Britain, the teleporter down to the Britain Sewer.</summary>
    private static readonly Point3D BritainSewerGrate = new(1491, 1641, 24);

    private const string BritainSewer = "Britain Sewer";

    /// <summary>Places a Second Age world keeps: the classic dungeons and the two parts of "Misc Dungeons".</summary>
    private static readonly string[] KeptPlaces =
    [
        "Covetous", "Deceit", "Despise", "Destard", "Fire", "Hythloth", "Ice", "Orc Cave", "Shame", "Terathan Keep",
        "Wrong", BritainSewer, "Trinsic Passage", "Britain", "Wind"
    ];

    /// <summary>
    /// Places a Second Age world drops: the grouping region and every Mondain's Legacy place,
    /// Sanctuary and Blighted Grove too, whose pads work and whose spawns ModernUO's "shared"
    /// set loads in every era.
    /// </summary>
    private static readonly string[] DroppedPlaces =
    [
        "Misc Dungeons", EraRules.ThePaintedCaves, EraRules.ThePrismOfLight, EraRules.ThePalaceOfParoxysmus,
        EraRules.TheHeartwood, EraRules.Sanctuary, EraRules.BlightedGrove
    ];

    /// <summary>
    /// Dungeon doors a player walks to from a town on Felucca. Deceit is left out: its door
    /// stands on an island no land walk from a town reaches.
    /// </summary>
    private static readonly string[] WalkableDoors =
    [
        "Covetous", "Despise", "Destard", "Fire", "Hythloth", "Ice", "Khaldun", "Misc Dungeons",
        "Orc Cave", "Shame", "Terathan Keep", "Wrong"
    ];

    /// <summary>
    /// Spots that share one walk though no travel from the main graph joined them: the Deceit
    /// door and the Honesty shrine on their island, the two ends of the Valor island, and the
    /// Fire dungeon's upper landing and its exit pads.
    /// </summary>
    private static readonly (string Place, Point3D From, Point3D To)[] SharedWalks =
    [
        ("Deceit island", new Point3D(4111, 430, 5), new Point3D(4217, 564, 36)),
        ("Valor island", new Point3D(2492, 3932, 2), new Point3D(2438, 3914, 0)),
        ("Fire upper floor", new Point3D(5687, 1423, 38), new Point3D(5680, 1438, 0)),
        ("lower Trinsic passage", new Point3D(5961, 1408, 57), new Point3D(6025, 1344, -27))
    ];

    /// <summary>
    /// The Honesty and the Valor shrines, whose islands no walk or pad from a town reaches, at
    /// their spots in the server's own location file.
    /// </summary>
    private static readonly Point3D[] IslandShrines = [new(4217, 564, 36), new(2496, 3932, 0)];

    private readonly ITestOutputHelper _output;

    public RealMapNavTests(ITestOutputHelper output) => _output = output;

    [RealMapFact]
    public void TerrainRoute_DespiseDoor_JoinsTheBritainSideWithRoadsThatWalkBothWays()
    {
        // The flood reached the door; every road back failed on a move that only walked
        // outward. The door must join, and every road edge must walk both ways.
        var walker = RealMapWorld.Walker();
        var keep = WalkLine.Outdoors(RealMapWorld.IsIndoor);
        var south = Node("south", BritainSideA);
        var southEast = Node("south-east", BritainSideB);
        south.Connects.Add(southEast.Name);
        southEast.Connects.Add(south.Name);
        var door = Node("door", DespiseDoor);
        var nodes = new List<NavNode> { south, southEast, door };

        TerrainRoute.ConnectComponents(nodes, walker, keep);

        Assert.True(new NavGraph(RealMapWorld.FacetName, nodes).SameComponent(south.Name, door.Name));
        var byName = nodes.ToDictionary(node => node.Name);

        foreach (var node in nodes)
        {
            foreach (var other in node.Connects.Select(name => byName[name]))
            {
                Assert.True(
                    WalkLine.Reaches(walker, node.Location, other.Location, keep),
                    $"road edge {node.Location} -> {other.Location} does not walk"
                );
            }
        }
    }

    [RealMapFact]
    public void PanelLanding_EveryTownAndDungeon_LandsWhereAPersonCanWalkAway()
    {
        var map = RealMapWorld.Felucca;
        var walker = RealMapWorld.Walker();
        var pads = DataPads();
        var failed = new List<string>();

        foreach (var spot in PanelSpotsFromData())
        {
            var landed = PanelActions.Landed(map, [spot], pads);

            if (landed.Count == 0)
            {
                failed.Add($"{spot.Label} {spot.At}: no landing");
                continue;
            }

            var at = landed[0].At;
            var walksAway = TileGrid.Neighbours.ToArray().Any(offset =>
                walker.Step(at.X, at.Y, at.Z, at.X + offset.X, at.Y + offset.Y, out var z) &&
                !PanelActions.IsStepOff(map, at.X + offset.X, at.Y + offset.Y, z, pads)
            );

            if (walker.FloorNear(at.X, at.Y, at.Z) != at.Z || PanelActions.IsStepOff(map, at.X, at.Y, at.Z, pads) || !walksAway)
            {
                failed.Add($"{spot.Label} {spot.At} -> {at}");
            }

            _output.WriteLine($"{spot.Label}: {spot.At} -> {at}");
        }

        Assert.True(failed.Count == 0, string.Join(Environment.NewLine, failed));
    }

    [RealMapFact]
    public void PlaceBook_SecondAge_KeepsWorkingPlaces_DropsBrokenOnes_AndSplitsMiscDungeons()
    {
        var book = RealMapWorld.PlaceBook();
        var names = book.Places.Select(place => place.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var place in book.Places)
        {
            _output.WriteLine($"{(place.IsTown ? "town" : "dungeon")} {place.Name} ({place.Region}) door {place.Door}");
        }

        Assert.All(KeptPlaces, name => Assert.Contains(name, names));
        Assert.All(DroppedPlaces, name => Assert.DoesNotContain(name, names));
        Assert.True(
            NavMetric.Chebyshev(book.Places.Single(place => place.Name == BritainSewer).Door, BritainSewerGrate) <= DoorReachTiles,
            "the Britain Sewer door is not the grate in Britain"
        );
    }

    [RealMapFact(fullBuild: true)]
    public void Generate_Felucca_MainGraphHoldsTheWalkableDoorsAndCove()
    {
        var walker = RealMapWorld.Walker();
        var timer = Stopwatch.StartNew();

        Assert.True(
            WorldGenerator.TryGenerate(
                RealMapWorld.FacetName,
                RealMapWorld.ServerDataRoot,
                WorldSetupRules.T2ASpawnSets,
                walker,
                out var graph,
                out var catalog,
                groundZ: (x, y) => Standable.TryFindGround(RealMapWorld.Felucca, x, y, out var z) ? z : 0,
                isIndoor: RealMapWorld.IsIndoor
            )
        );
        NodeHeight.Settle(graph.Nodes, walker.FloorNear);
        graph = new NavGraph(graph.Facet, graph.Nodes);
        timer.Stop();

        var main = graph.LargestComponent();
        var sizes = graph.Nodes.GroupBy(node => graph.ComponentOf(node.Name)).ToDictionary(group => group.Key, group => group.Count());
        var share = (double)sizes[main] / graph.NodeCount;
        var edges = graph.Nodes.Sum(node => node.Connects.Count) / 2;
        var dungeons = catalog.All.Count(destination => destination.ParsedKind == DestinationKind.Dungeon);
        _output.WriteLine(
            $"{graph.NodeCount} nodes, {edges} edges, {sizes.Count} components, {share:P1} in the largest, " +
            $"{catalog.All.Count} destinations ({dungeons} Dungeon), built in {timer.Elapsed.TotalSeconds:F0} s"
        );

        var doorInMain = new Dictionary<string, bool>();

        foreach (var region in RealMapWorld.ReadRegions(RegionsPath(), RealMapWorld.DungeonRegionType))
        {
            doorInMain[region.Name] = graph.Nodes.Any(node =>
                NavMetric.Chebyshev(node.Location, region.Entrance) <= DoorReachTiles &&
                graph.ComponentOf(node.Name) == main
            );
            _output.WriteLine($"{region.Name} door {region.Entrance}: {(doorInMain[region.Name] ? "main graph" : "apart")}");
        }

        Assert.True(sizes[main] >= LargestNodesFloor, $"largest component holds {sizes[main]} nodes ({share:P1})");
        Assert.True(
            graph.Nodes.Any(node => NavMetric.Chebyshev(node.Location, CoveStreet) <= DoorReachTiles && graph.ComponentOf(node.Name) == main),
            "Cove is off the main graph"
        );
        Assert.All(WalkableDoors, door => Assert.True(doorInMain[door], $"{door} door is off the main graph"));
        Assert.All(
            IslandShrines,
            shrine => Assert.Contains(
                catalog.All,
                destination => destination.ParsedKind == DestinationKind.Shrine &&
                               NavMetric.Chebyshev(destination.Location, shrine) <= DoorReachTiles
            )
        );
        Assert.All(
            SharedWalks,
            walk => Assert.True(
                RoadChecks.WalkJoined(graph.Nodes, NodeNear(graph, walk.From).Name, NodeNear(graph, walk.To).Name),
                $"{walk.Place}: {walk.From} and {walk.To} are not walk-joined"
            )
        );
    }

    /// <summary>The graph node nearest a spot, which must lie within <see cref="DoorReachTiles"/>.</summary>
    private static NavNode NodeNear(NavGraph graph, Point3D spot)
    {
        var node = graph.FindNearest(spot);
        Assert.True(node != null && NavMetric.Chebyshev(node.Location, spot) <= DoorReachTiles, $"no node near {spot}");
        return node;
    }

    private static string RegionsPath() => Path.Combine(RealMapWorld.ServerDataRoot, RealMapWorld.RegionsFile);

    /// <summary>The panel's towns and dungeons: the doors of the places a Second Age world keeps.</summary>
    private static List<TravelSpot> PanelSpotsFromData() => PanelRules.PlaceSpots(RealMapWorld.PlaceBook().Places);

    /// <summary>
    /// Every gate pad on Felucca from the server data: each teleporter's source, its return
    /// end when it has one, and the public moongates. The live panel reads the same pads off
    /// the nav graph; this process has no world items.
    /// </summary>
    private static HashSet<Point2D> DataPads()
    {
        var pads = new HashSet<Point2D>();
        var json = File.ReadAllText(Path.Combine(RealMapWorld.ServerDataRoot, TeleportersFile));

        foreach (var link in WorldDataSeeds.ParseTeleporters(json))
        {
            if (RealMapWorld.FacetName.Equals(link.SrcMap, StringComparison.OrdinalIgnoreCase))
            {
                pads.Add(new Point2D(link.Sx, link.Sy));
            }

            if (link.Back && RealMapWorld.FacetName.Equals(link.DstMap, StringComparison.OrdinalIgnoreCase))
            {
                pads.Add(new Point2D(link.Dx, link.Dy));
            }
        }

        foreach (var gate in MoongateSeeds.LocationsFor(RealMapWorld.FacetName))
        {
            pads.Add(new Point2D(gate.X, gate.Y));
        }

        return pads;
    }

    private static NavNode Node(string name, Point3D at) =>
        new() { Name = name, X = at.X, Y = at.Y, Z = at.Z, ArrivalRange = NavLimits.DefaultArrivalRange, Connects = [] };
}
