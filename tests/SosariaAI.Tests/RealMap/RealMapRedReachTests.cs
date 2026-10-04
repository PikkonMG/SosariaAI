using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// A red on Buccaneer's Den, on the real Felucca tiles, the live nav graph and the server's
/// own town guards (the shared moongate region without guards, as the boot leaves it): the
/// Den's gate walks it to the open moongates and the doors past them, and a Serpent's Hold
/// door lies under the guards. From the Den, reds were sent to camps past guarded towns and
/// failed on the way out. Which camps a road clear of the guards reaches is the graph's to
/// say: since the build joins the pieces whose walks meet, the Destard door lies past the
/// Despise pads, whose lowest pad sends to Destard's lowest floor, and the Deceit door past
/// Destard's pad into Deceit; a walker those floors do not fit takes neither
/// (<see cref="PathSearchBars.ForWalker"/>). So the tests hold the rule on any graph: a red
/// walks to a camp only by the road travel plans for it, which steps on no guarded node, leg
/// tile or moongate, and a camp it cannot walk to has no road there but through the guards or
/// a drop onto a floor it does not fit. An island door is reached by a gate or a pad, never by
/// a walk across the sea.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapRedReachTests
{
    private const uint RedSerial = 0x7E51;
    private const uint BlueSerial = 0x7E52;

    private static readonly Point3D DenStreet = new(2706, 2163, 0);
    private static readonly Point3D YewGateCamp = new(787, 736, 0);
    private static readonly Point3D CovetousDoorCamp = new(2500, 914, 0);
    private static readonly Point3D ShameDoorCamp = new(517, 1559, 0);
    private static readonly Point3D DestardDoorCamp = new(1170, 2643, 2);
    private static readonly Point3D GraveyardCamp = new(1390, 1497, 10);
    private static readonly Point3D SerpentsHoldDoor = new(2923, 3405, 6);
    private static readonly Point3D BritainBank = new(1434, 1699, 0);
    private static readonly Point3D DeceitDoor = new(4110, 430, 5);
    private static readonly Point3D HythlothDoor = new(4721, 3813, 0);
    private static readonly Point3D SacrificeShrine = new(3354, 289, 4);
    private static readonly Point3D MinocGate = new(2701, 692, 5);

    [RealMapFact]
    public void DenRed_WalksThroughTheOpenGates_NeverPastTheGuards()
    {
        var map = RealMapWorld.Felucca;
        var regions = RealMapWorld.RegisterGuards(map);

        try
        {
            RealMapWorld.WithLiveGraph(() =>
            {
                TestMap.EnsureInternal();
                TestSkills.EnsureTable();
                var graph = NavWorld.GraphFor(RealMapWorld.FacetName);
                var red = new SosariaCharacter((Serial)RedSerial) { Kills = PkRules.MurdersToRed };
                red.DefaultMobileInit();

                Assert.True(RedGangReach.Walks(red, map, DenStreet, YewGateCamp), "Yew gate camp");
                Assert.True(RedGangReach.Walks(red, map, DenStreet, CovetousDoorCamp), "Covetous door camp");
                AssertRedVerdict(graph, map, red, DenStreet, YewGateCamp, "Yew gate camp");
                AssertRedVerdict(graph, map, red, DenStreet, CovetousDoorCamp, "Covetous door camp");
                AssertRedVerdict(graph, map, red, DenStreet, ShameDoorCamp, "Shame door camp");
                AssertRedVerdict(graph, map, red, DenStreet, DestardDoorCamp, "Destard door camp");
                AssertRedVerdict(graph, map, red, DenStreet, GraveyardCamp, "Britain graveyard camp");
                Assert.True(RuneShelf.BarredFor(red, SerpentsHoldDoor, map), "Serpent's Hold door");

                var blue = new SosariaCharacter((Serial)BlueSerial);
                blue.DefaultMobileInit();
                Assert.True(RedGangReach.Walks(blue, map, BritainBank, CovetousDoorCamp), "blue to Covetous");
                AssertIslandVerdict(graph, map, blue, BritainBank, DeceitDoor, "blue to Deceit");
                AssertIslandVerdict(graph, map, blue, BritainBank, HythlothDoor, "blue to Hythloth");
            });
        }
        finally
        {
            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    /// <summary>
    /// A red raised at the Sacrifice ankh, one in the Britain graveyard and one at the Shame
    /// door, on the live graph and the server's own guards: every road to the Minoc gate and to
    /// the Den runs through a guarded town, so the barred search finds none. Crossing the guards
    /// finds one, and it crosses no more guarded ground than the plain road does.
    /// </summary>
    [RealMapFact]
    public void StrandedRed_HasNoRoadClearOfTheGuards_AndCrossesThemByTheLeastGuardedWay()
    {
        var map = RealMapWorld.Felucca;
        var regions = RealMapWorld.RegisterGuards(map);

        try
        {
            RealMapWorld.WithLiveGraph(() =>
            {
                var graph = NavWorld.GraphFor(RealMapWorld.FacetName);

                AssertCrossing(graph, map, SacrificeShrine, MinocGate, "Sacrifice to the Minoc gate");
                AssertCrossing(graph, map, SacrificeShrine, DenStreet, "Sacrifice to the Den");
                AssertCrossing(graph, map, GraveyardCamp, DenStreet, "Britain graveyard to the Den");
                AssertCrossing(graph, map, ShameDoorCamp, DenStreet, "Shame door to the Den");
            });
        }
        finally
        {
            foreach (var region in regions)
            {
                region.Unregister();
            }
        }
    }

    private static void AssertCrossing(NavGraph graph, Map map, Point3D from, Point3D to, string trip)
    {
        var start = graph.FindNearest(from);
        var goal = graph.FindNearest(to);
        var bars = PathSearchBars.For(graph, map, from, murderer: true);

        Assert.NotNull(bars);
        Assert.True(Road(graph, goal, start, bars).Count == 0, $"{trip}: a road clear of the guards");

        var plain = Road(graph, goal, start, null);
        var crossing = Road(graph, goal, start, bars.CrossingGuards());

        Assert.True(crossing.Count > 0, $"{trip}: no road across the guards");
        Assert.True(GuardedTiles(graph, map, crossing) <= GuardedTiles(graph, map, plain), $"{trip}: the crossing is not the least guarded road");
        Assert.False(TakesGuardedMoongate(graph, map, crossing), $"{trip}: the crossing takes a guarded moongate");
    }

    /// <summary>
    /// The red's verdict on a camp is the road travel plans for it on a murderer's roads. When
    /// the red walks there, that road crosses no guarded ground; when it does not, every road
    /// there crosses the guards, or there is none at all.
    /// </summary>
    private static void AssertRedVerdict(NavGraph graph, Map map, SosariaCharacter red, Point3D from, Point3D to, string trip)
    {
        var start = graph.FindNearest(from);
        var goal = graph.FindNearest(to);
        var bars = PathSearchBars.ForWalker(red, graph, map, from);
        var road = Road(graph, goal, start, bars);

        Assert.True(RedGangReach.Walks(red, map, from, to) == road.Count > 0, $"{trip}: the reach verdict is not the road travel plans");

        if (road.Count > 0)
        {
            Assert.False(CrossesGuards(graph, map, road), $"{trip}: the red's road crosses the guards");
            return;
        }

        var plain = Road(graph, goal, start, bars.Pads == null ? null : new PathSearchBars(null, default, noMoongates: false, openAroundStart: false, pads: bars.Pads));
        Assert.True(plain.Count == 0 || CrossesGuards(graph, map, plain), $"{trip}: a road clear of the guards that the red does not take");
    }

    /// <summary>
    /// A blue's verdict on an island door is the road travel plans for it, and that road gets
    /// onto the island by a gate or a pad: no walk crosses the sea.
    /// </summary>
    private static void AssertIslandVerdict(NavGraph graph, Map map, SosariaCharacter blue, Point3D from, Point3D door, string trip)
    {
        var road = Road(graph, graph.FindNearest(door), graph.FindNearest(from), PathSearchBars.ForWalker(blue, graph, map, from));

        Assert.True(RedGangReach.Walks(blue, map, from, door) == road.Count > 0, $"{trip}: the reach verdict is not the road travel plans");
        Assert.True(road.Count == 0 || TakesAGate(graph, road), $"{trip}: the road walks across the sea");
    }

    /// <summary>True when the road steps on a guarded node, walks a leg over a guarded tile or takes a guarded moongate.</summary>
    private static bool CrossesGuards(NavGraph graph, Map map, IReadOnlyList<string> road)
    {
        for (var i = 0; i < road.Count; i++)
        {
            if (GuardCall.IsGuardedPlace(Location(graph, road[i]), map) ||
                i > 0 && !IsHop(graph, road[i - 1], road[i]) && LegCrossesGuards(Location(graph, road[i - 1]), Location(graph, road[i]), map))
            {
                return true;
            }
        }

        return TakesGuardedMoongate(graph, map, road);
    }

    /// <summary>True when a tile of the straight leg between two nodes lies under the guards.</summary>
    private static bool LegCrossesGuards(Point3D from, Point3D to, Map map) =>
        GuardedLegs.Crosses(from, to, (x, y, z) => GuardCall.IsGuardedPlace(new Point3D(x, y, z), map));

    private static bool TakesGuardedMoongate(NavGraph graph, Map map, IReadOnlyList<string> road)
    {
        for (var i = 1; i < road.Count; i++)
        {
            if (graph.GateKind(road[i - 1], road[i]) == NavGateKind.Moongate &&
                (GuardCall.IsGuardedPlace(Location(graph, road[i - 1]), map) || GuardCall.IsGuardedPlace(Location(graph, road[i]), map)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TakesAGate(NavGraph graph, IReadOnlyList<string> road)
    {
        for (var i = 1; i < road.Count; i++)
        {
            if (IsHop(graph, road[i - 1], road[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a gate of any kind leads between the two nodes, whichever way the road lists them.</summary>
    private static bool IsHop(NavGraph graph, string a, string b) => graph.IsGate(a, b) || graph.IsGate(b, a);

    /// <summary>The road a person walks from <paramref name="start"/> to <paramref name="goal"/>, searched from the goal as travel does.</summary>
    internal static IReadOnlyList<string> Road(NavGraph graph, NavNode goal, NavNode start, PathSearchBars bars)
    {
        var cost = new Dictionary<string, double>();
        var prev = new Dictionary<string, string>();
        NavSearch.Explore(graph, goal.Name, null, NavSearch.DefaultGateCost, null, null, cost, prev, bars);
        return NavSearch.Reconstruct(prev, goal.Name, start.Name);
    }

    /// <summary>
    /// Tiles of the road's legs that leave guarded ground or walk over it, a gate hop counted at
    /// its search cost: the measure the crossing search weighs.
    /// </summary>
    private static double GuardedTiles(NavGraph graph, Map map, IReadOnlyList<string> road)
    {
        var tiles = 0.0;

        for (var i = 1; i < road.Count; i++)
        {
            var from = Location(graph, road[i - 1]);
            var to = Location(graph, road[i]);
            var hop = IsHop(graph, road[i - 1], road[i]);

            if (GuardCall.IsGuardedPlace(from, map) || !hop && LegCrossesGuards(from, to, map))
            {
                tiles += hop ? NavSearch.DefaultGateCost : NavMetric.Distance(from, to);
            }
        }

        return tiles;
    }

    private static Point3D Location(NavGraph graph, string name) => graph.TryGetNode(name, out var node) ? node.Location : Point3D.Zero;
}
