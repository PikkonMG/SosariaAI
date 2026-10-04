using System;
using System.Collections.Generic;
using System.Diagnostics;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// Walkers the night's log found off the road graph, on the real Felucca tiles and the saved
/// graph. Just after 174 of the 186 freezes of five to eight seconds, one of them wrote "no
/// node within one leg of the walker" or "the walk to the first node is blocked": the world
/// thread had run a graph search and an 80,000-cell tile route for every node near the goal.
/// The world thread's half of each plan from these starts, the first-hop proof and the walk
/// back onto the road, now stays within <see cref="WorldThreadBudgetMs"/>. The graph search
/// itself runs on a path worker and is not timed. A tile trip from them and the marooned
/// rescue stay within the same budget.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapOffGraphTests(ITestOutputHelper output)
{
    /// <summary>
    /// The most one plan's world-thread half may take here. The old walk back took 7.6 s from
    /// the Minoc hills; the bounded one takes a few hundredths of a second.
    /// </summary>
    private const double WorldThreadBudgetMs = 250;

    /// <summary>
    /// Real starts and goals of the log: the Minoc hills, north of Britain, the Yew woods, two
    /// blocked first hops, and the Britain bank floor, where a plan searches the graph from each
    /// indoor start near the walker.
    /// </summary>
    private static readonly (Point3D From, Point3D To)[] Trips =
    [
        (new Point3D(2161, 460, 0), new Point3D(2528, 575, 0)),
        (new Point3D(2161, 460, 0), new Point3D(2701, 692, 5)),
        (new Point3D(2208, 494, 0), new Point3D(2527, 574, 0)),
        (new Point3D(1312, 992, 0), new Point3D(771, 752, 5)),
        (new Point3D(1309, 990, 0), new Point3D(4441, 1160, 0)),
        (new Point3D(1131, 539, 0), new Point3D(1298, 1074, 0)),
        (new Point3D(2439, 822, 0), new Point3D(2701, 692, 5)),
        (new Point3D(2430, 828, 0), new Point3D(2742, 2169, -2)),
        (new Point3D(1425, 1690, 0), new Point3D(2503, 555, 0))
    ];

    [RealMapFact]
    public void FromEachOffRoadStart_TheWorldThreadHalfOfThePlan_StaysWithinTheBudget() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var graph = NavWorld.GraphFor(RealMapWorld.FacetName);
            var walker = RealMapWorld.Walker();
            var worst = 0.0;

            foreach (var (from, to) in Trips)
            {
                var destination = Traveler.PreferredGoal(graph, from, to)?.Name;
                Assert.NotNull(destination);
                var prev = WorkerSearch(graph, from, destination);

                var timer = Stopwatch.StartNew();
                var names = Traveler.FinishPlan(graph, from, destination, prev, walker, RealMapWorld.IsIndoor, avoid: null, out var why);
                IReadOnlyList<Point3D> back = names.Count > 0 ? [] : Traveler.RoadBack(graph, from, destination, prev, walker, RealMapWorld.IsIndoor);
                timer.Stop();

                worst = Math.Max(worst, timer.Elapsed.TotalMilliseconds);
                output.WriteLine($"{from} to {to}: {names.Count} nodes ({why}), road back {back.Count} tiles, {timer.Elapsed.TotalMilliseconds:F1} ms");
            }

            Assert.True(worst < WorldThreadBudgetMs, $"worst {worst:F1} ms");
        });

    /// <summary>
    /// Tile trips of the tile router's reach from the same starts, most of them to ground the
    /// start's area does not reach: each paid all 80,000 cells, 0.65 s, before the cap.
    /// </summary>
    private static readonly (Point3D From, Point3D To)[] TileTrips =
    [
        (new Point3D(2161, 460, 0), new Point3D(2078, 431, 0)),
        (new Point3D(2161, 460, 0), new Point3D(2401, 560, 0)),
        (new Point3D(2208, 494, 0), new Point3D(2311, 600, 2)),
        (new Point3D(1312, 992, 0), new Point3D(1246, 927, 0)),
        (new Point3D(1131, 539, 0), new Point3D(1171, 560, 1)),
        (new Point3D(2439, 822, 0), new Point3D(2690, 700, 0))
    ];

    /// <summary>A home far off, as the marooned rescue proves its escape toward.</summary>
    private static readonly Point3D MinocHome = new(2528, 575, 0);

    [RealMapFact]
    public void TileTripsWithinTheRoutersReach_StayWithinTheBudget() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var walker = RealMapWorld.Walker();
            var worst = 0.0;

            foreach (var (from, to) in TileTrips)
            {
                Assert.True(NavMetric.Chebyshev(from, to) <= TileRoute.MaxTripTiles, $"{from} to {to}");

                var timer = Stopwatch.StartNew();
                var tiles = TileRoute.Find(from, to, walker, RealMapWorld.IsIndoor, 0, out var why);
                timer.Stop();

                worst = Math.Max(worst, timer.Elapsed.TotalMilliseconds);
                output.WriteLine($"{from} to {to}: {tiles.Count} waypoints ({why}), {timer.Elapsed.TotalMilliseconds:F1} ms");
            }

            Assert.True(worst < WorldThreadBudgetMs, $"worst {worst:F1} ms");
        });

    [RealMapFact]
    public void TheMaroonedRescueFromEachOffRoadStart_StaysWithinTheBudget() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var graph = NavWorld.GraphFor(RealMapWorld.FacetName);
            var walker = RealMapWorld.Walker();
            var worst = 0.0;

            foreach (var (from, _) in Trips)
            {
                var timer = Stopwatch.StartNew();
                var sealedIn = !Traveler.HasClearStart(graph, from, walker, RealMapWorld.IsIndoor);
                var node = Traveler.NearestRoutableStart(
                    graph,
                    from,
                    MinocHome,
                    NodeHeight.Stands(walker.FloorNear),
                    walker,
                    RealMapWorld.IsIndoor
                );
                timer.Stop();

                worst = Math.Max(worst, timer.Elapsed.TotalMilliseconds);
                output.WriteLine($"{from}: sealed {sealedIn}, road at {node?.Location}, {timer.Elapsed.TotalMilliseconds:F1} ms");
            }

            Assert.True(worst < WorldThreadBudgetMs, $"worst {worst:F1} ms");
        });

    /// <summary>The path worker's search from the goal node, as the trip queues it.</summary>
    private static Dictionary<string, string> WorkerSearch(NavGraph graph, Point3D from, string destination)
    {
        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var keep = Traveler.IndoorKeepFor(graph, from, destination, RealMapWorld.IsIndoor);
        NavSearch.Explore(graph, destination, stopAt: null, NavSearch.DefaultGateCost, avoid: null, keep, cost, prev);
        return prev;
    }
}
