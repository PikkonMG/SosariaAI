using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Islands and dungeon floors that travel from the main graph never led into, on the real
/// Felucca tiles. Each island is walked tile by tile inside a box larger than it: the walk
/// ends inside the box, so the island has no land way off it, and the server data puts no
/// pad and no public moongate on it other than the ones named. The graph builder then joins
/// the pieces that share one of these walks. The home piece stands on the south Jhelom
/// island, which is sealed too, so the build walks only the ground each test names.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapIslandWalkTests(ITestOutputHelper output)
{
    private const string TeleportersFile = "teleporters.json";

    private static readonly Point3D DeceitDoor = new(4111, 430, 5);
    private static readonly Point3D HonestyShrine = new(4212, 563, 42);
    private static readonly Box DeceitIsland = new(3700, 100, 4500, 900);

    /// <summary>The three Deceit door pads, which carry walkers into the dungeon.</summary>
    private static readonly Point2D[] DeceitDoorPads = [new(4110, 430), new(4111, 430), new(4112, 430)];

    private static readonly Point3D ValorShrine = new(2492, 3932, 2);

    /// <summary>The shrine map markers the world generator reads: a place, and no height.</summary>
    private static readonly Point2D HonestyMarker = new(4212, 563);

    private static readonly Point2D ValorMarker = new(2491, 3933);
    private const string HonestyName = "Honesty";
    private const string ValorName = "Valor";

    /// <summary>A node of the other graph piece on the Valor island, north-west of the shrine.</summary>
    private static readonly Point3D ValorWest = new(2436, 3902, 0);

    private static readonly Box ValorIsland = new(2200, 3700, 2800, 4095);

    /// <summary>The landing of the south Jhelom island's hub pad, and the dead pad that was its only way off.</summary>
    private static readonly Point3D SouthJhelomLanding = new(1466, 4015, 5);

    private static readonly Point2D SouthJhelomDeadPad = new(1406, 3996);
    private static readonly Point3D JhelomHubPad = new(1419, 3832, 5);
    private static readonly Box SouthJhelomIsland = new(1200, 3700, 1700, 4095);

    private static readonly Point3D SerpentsHoldFireLanding = new(2923, 3406, 8);
    private static readonly Point2D SerpentsHoldFirePad = new(2923, 3405);
    private static readonly Point3D FireDoor = new(2922, 3402, 0);
    private static readonly Box SerpentsHoldIsland = new(2600, 3150, 3300, 3800);

    /// <summary>Where the Serpent's Hold pad lands on the Fire dungeon's upper floor.</summary>
    private static readonly Point3D FireUpperLanding = new(5687, 1423, 38);

    /// <summary>Beside the Fire dungeon's one-way exit pads to other dungeons, on the same floor.</summary>
    private static readonly Point3D FireExitPads = new(5680, 1438, 0);

    private static readonly Box FireDungeon = new(5635, 1285, 5880, 1520);

    /// <summary>The lower Trinsic passage: its landing from above, and two pads on into the Lost Lands.</summary>
    private static readonly Point3D TrinsicPassageLanding = new(5961, 1408, 57);

    private static readonly Point3D TrinsicPassageWestPad = new(6025, 1344, -27);
    private static readonly Point3D TrinsicPassageSouthPad = new(6005, 1378, 0);
    private static readonly Box TrinsicPassage = new(5880, 1280, 6140, 1440);

    /// <summary>A second home node on the south Jhelom island, the tile south of its landing.</summary>
    private static readonly Point3D SouthJhelomHomeSouth = new(1466, 4016, 5);

    [RealMapFact]
    public void DeceitIsland_IsOneWalk_WithNoLandOrPadOffIt_ButTheDeceitDoor()
    {
        var walk = AssertSealedIsland("Deceit island", DeceitDoor, DeceitIsland, DeceitDoorPads);

        Assert.True(walk.Reaches(HonestyShrine), "the Deceit door walk misses the Honesty shrine");
    }

    [RealMapFact]
    public void ValorIsland_IsOneWalk_WithNoLandOrPadOffIt()
    {
        var walk = AssertSealedIsland("Valor island", ValorShrine, ValorIsland, []);

        Assert.True(walk.Reaches(ValorWest), "the Valor shrine walk misses the island's other piece");
    }

    [RealMapFact]
    public void SouthJhelomIsland_HasNoLandWayOff_AndNoPadButTheDeadOne()
    {
        var walk = AssertSealedIsland("south Jhelom island", SouthJhelomLanding, SouthJhelomIsland, [SouthJhelomDeadPad]);

        Assert.False(walk.Reaches(JhelomHubPad), "the south Jhelom island walks to the Jhelom hub pad");
    }

    [RealMapFact]
    public void SerpentsHoldIsland_HasNoLandWayOff_AndOnlyTheFirePad()
    {
        var walk = AssertSealedIsland("Serpent's Hold island", SerpentsHoldFireLanding, SerpentsHoldIsland, [SerpentsHoldFirePad]);

        Assert.True(walk.Reaches(FireDoor), "the Serpent's Hold walk misses the Fire door");
    }

    [RealMapFact]
    public void FireUpperFloor_ExitPadsStandOnTheLandingsWalk()
    {
        var walker = RealMapWorld.Walker();

        Assert.True(TileWalk.From(walker, FireUpperLanding, FireDungeon).Reaches(FireExitPads));
    }

    [RealMapFact]
    public void TerrainRoute_JoinsTheDeceitDoorAndTheHonestyShrine_ThoughNoTravelLeadsThere()
    {
        var nodes = HomeAnd(Node("door", DeceitDoor), Node("shrine", HonestyShrine));

        AssertJoined(nodes, "door", "shrine");
    }

    [RealMapFact]
    public void TerrainRoute_JoinsTheValorIslandPieces_ThoughNoTravelLeadsThere()
    {
        var nodes = HomeAnd(Node("shrine", ValorShrine), Node("west", ValorWest));

        AssertJoined(nodes, "shrine", "west");
    }

    /// <summary>
    /// The live Fire dungeon's gates: a pad carries walkers onto the upper floor, and the exit
    /// pads send them on one way. Travel reaches both nodes, and that alone left them apart.
    /// </summary>
    [RealMapFact]
    public void TerrainRoute_JoinsTheFireExitPadsToTheUpperFloor_ThoughTravelReachesBoth()
    {
        var landing = Node("landing", FireUpperLanding);
        var exitPads = Node("exit-pads", FireExitPads);
        var nodes = HomeAnd(landing, exitPads);
        NavGates.AddOneWay(nodes[0], landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(exitPads, nodes[1], NavGateKind.Teleporter);

        AssertJoined(nodes, "landing", "exit-pads");
    }

    /// <summary>
    /// The lower Trinsic passage: the pad from above lands on it, and its pads go on into the
    /// Lost Lands. Its three pieces share one walk.
    /// </summary>
    [RealMapFact]
    public void TerrainRoute_JoinsTheLowerTrinsicPassage_ThoughTravelReachesEachPart()
    {
        var walker = RealMapWorld.Walker();
        var walk = TileWalk.From(walker, TrinsicPassageLanding, TrinsicPassage);
        Assert.True(walk.Reaches(TrinsicPassageWestPad) && walk.Reaches(TrinsicPassageSouthPad));

        var landing = Node("landing", TrinsicPassageLanding);
        var west = Node("west-pad", TrinsicPassageWestPad);
        var south = Node("south-pad", TrinsicPassageSouthPad);
        var nodes = HomeAnd(landing, west, south);
        NavGates.AddOneWay(nodes[0], landing, NavGateKind.Teleporter);
        NavGates.AddOneWay(west, nodes[1], NavGateKind.Teleporter);
        NavGates.AddOneWay(south, nodes[1], NavGateKind.Teleporter);

        AssertJoined(nodes, "landing", "west-pad");
        Assert.True(RoadChecks.WalkJoined(nodes, "landing", "south-pad"));
    }

    /// <summary>
    /// The destination catalog on the joined islands: the Honesty and the Valor shrines, at
    /// their map markers, each get an entry on a node that walks to its own island's other
    /// piece, so a ghost that falls there has an ankh to walk to.
    /// </summary>
    [RealMapFact]
    public void Catalog_KeepsTheHonestyAndValorShrines_OnTheirIslands()
    {
        var walker = RealMapWorld.Walker();
        var nodes = HomeAnd(Node("door", DeceitDoor), Node("honesty", HonestyShrine), Node("valor", ValorShrine), Node("valor-west", ValorWest));
        TerrainRoute.ConnectComponents(nodes, walker, WalkLine.Outdoors(RealMapWorld.IsIndoor));
        var graph = new NavGraph(RealMapWorld.FacetName, nodes);
        DestinationDraft[] drafts =
        [
            new() { Name = HonestyName, Kind = nameof(DestinationKind.Shrine), X = HonestyMarker.X, Y = HonestyMarker.Y },
            new() { Name = ValorName, Kind = nameof(DestinationKind.Shrine), X = ValorMarker.X, Y = ValorMarker.Y }
        ];

        var catalog = CatalogBuilder.Build(drafts, graph, walker, RealMapWorld.IsIndoor);

        var honesty = catalog.GetByName(HonestyName);
        var valor = catalog.GetByName(ValorName);
        Assert.NotNull(honesty);
        Assert.NotNull(valor);
        output.WriteLine($"Honesty at {honesty.Arrival} on {honesty.Node}, Valor at {valor.Arrival} on {valor.Node}");
        Assert.True(RoadChecks.WalkJoined(nodes, honesty.Node, "door"), "the Honesty entry's node does not walk to the Deceit door");
        Assert.True(RoadChecks.WalkJoined(nodes, valor.Node, "valor-west"), "the Valor entry's node does not walk across its island");
    }

    /// <summary>
    /// Walks the island from <paramref name="start"/> and checks that the walk ends inside
    /// <paramref name="box"/>, and that of the server's teleporter pads and public moongates
    /// only <paramref name="pads"/> stand on it.
    /// </summary>
    private TileWalk AssertSealedIsland(string island, Point3D start, Box box, Point2D[] pads)
    {
        var walk = TileWalk.From(RealMapWorld.Walker(), start, box);
        var padsOnIt = DataPads().Where(walk.Covers).ToHashSet();
        output.WriteLine($"{island}: {walk.Tiles.Count} tiles, {walk.StepsOut} steps out of {box}, pads {string.Join(" ", padsOnIt)}");

        Assert.Equal(0, walk.StepsOut);
        Assert.Equal(pads.ToHashSet(), padsOnIt);
        return walk;
    }

    /// <summary>
    /// The graph builder on these nodes with the real walker and roof test: the two named
    /// nodes end up joined by walking links alone, not to the home piece, and every walking
    /// link walks both ways.
    /// </summary>
    private void AssertJoined(List<NavNode> nodes, string from, string to)
    {
        var walker = RealMapWorld.Walker();
        var keep = WalkLine.Outdoors(RealMapWorld.IsIndoor);

        var joined = TerrainRoute.ConnectComponents(nodes, walker, keep);
        output.WriteLine($"{joined} pieces joined, {nodes.Count} nodes");

        Assert.True(RoadChecks.WalkJoined(nodes, from, to), $"{from} and {to} are not walk-joined");
        Assert.False(RoadChecks.WalkJoined(nodes, nodes[0].Name, from), $"{from} is walk-joined to home");
        RoadChecks.AssertWalksBothWays(nodes, walker, keep);
    }

    /// <summary>A two-node home piece on the sealed south Jhelom island, then <paramref name="others"/>, each a piece of its own.</summary>
    private static List<NavNode> HomeAnd(params NavNode[] others)
    {
        var home = Node("home", SouthJhelomLanding);
        var homeSouth = Node("home-south", SouthJhelomHomeSouth);
        GraphConnect.LinkForRoad(home, homeSouth);
        return [home, homeSouth, .. others];
    }

    /// <summary>Every Felucca teleporter pad in the server data, and every public moongate.</summary>
    private static HashSet<Point2D> DataPads()
    {
        var json = File.ReadAllText(Path.Combine(RealMapWorld.ServerDataRoot, TeleportersFile));
        var pads = WorldDataSeeds.ParseTeleporters(json)
            .Where(link => RealMapWorld.FacetName.Equals(link.SrcMap, StringComparison.OrdinalIgnoreCase))
            .Select(link => new Point2D(link.Sx, link.Sy))
            .ToHashSet();
        pads.UnionWith(MoongateSeeds.LocationsFor(RealMapWorld.FacetName).Select(gate => new Point2D(gate.X, gate.Y)));
        return pads;
    }

    private static NavNode Node(string name, Point3D at) =>
        new() { Name = name, X = at.X, Y = at.Y, Z = at.Z, ArrivalRange = NavLimits.DefaultArrivalRange, Connects = [] };

    /// <summary>A box of tiles, corners included.</summary>
    private readonly record struct Box(int West, int North, int East, int South)
    {
        public bool Holds(int x, int y) => x >= West && x <= East && y >= North && y <= South;
    }

    /// <summary>
    /// Every tile a walker reaches from a start with the engine's own step rule, inside a box,
    /// and how many steps leave the box.
    /// </summary>
    private sealed class TileWalk
    {
        /// <summary>Tiles a named spot may lie from the walk and still stand on it.</summary>
        private const int ReachTiles = 2;

        public HashSet<(int X, int Y)> Tiles { get; } = [];

        public int StepsOut { get; private set; }

        public static TileWalk From(TileWalker walker, Point3D start, Box box)
        {
            var walk = new TileWalk();
            var floor = walker.FloorNear(start.X, start.Y, start.Z) ?? start.Z;
            var seen = new HashSet<(int X, int Y, int Z)> { (start.X, start.Y, floor) };
            var queue = new Queue<(int X, int Y, int Z)>(seen);

            while (queue.Count > 0)
            {
                var (x, y, z) = queue.Dequeue();
                walk.Tiles.Add((x, y));

                foreach (var (dx, dy) in TileGrid.Neighbours)
                {
                    var (toX, toY) = (x + dx, y + dy);

                    if (!walker.Step(x, y, z, toX, toY, out var toZ))
                    {
                        continue;
                    }

                    if (!box.Holds(toX, toY))
                    {
                        walk.StepsOut++;
                    }
                    else if (seen.Add((toX, toY, toZ)))
                    {
                        queue.Enqueue((toX, toY, toZ));
                    }
                }
            }

            return walk;
        }

        public bool Covers(Point2D tile) => Tiles.Contains((tile.X, tile.Y));

        public bool Reaches(Point3D spot) =>
            Tiles.Any(tile => Math.Max(Math.Abs(tile.X - spot.X), Math.Abs(tile.Y - spot.Y)) <= ReachTiles);
    }
}
