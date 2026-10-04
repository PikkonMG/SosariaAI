using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// A ghost's reach (<see cref="GhostReach"/>) on the real Felucca tiles and the saved nav graph.
/// Each verdict is the one the ghost's own trip would reach, a real plan or a tile route, so a
/// ghost never sets out for a site its walk then finds no road to (572 walks went to the
/// Spirituality ankh platform, which only its exit pads touch), and it picks none, and falls
/// back, when no site has a way (three blues at the Deceit door waited out the half hour). Of the
/// sites with a way it picks the shortest trip, not the nearest site: red ghosts at Magincia took
/// the Honesty ankh through three dungeons while the Justice shrine lay a short walk from the Yew
/// gate. The rule is tested, not one graph's roads: a nav rebuild joins more pieces. The Valor island is
/// sealed with no pads on every graph, so a ghost there reaches its own shrine and nothing on
/// the mainland.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapGhostReachTests(ITestOutputHelper output)
{
    private static readonly Point3D DeceitDoor = new(4110, 430, 5);
    private static readonly Point3D BritainStreet = new(1415, 1690, 0);
    private static readonly Point3D TrinsicStreet = new(1911, 2810, 0);
    private static readonly Point3D ValorIsland = new(2512, 3918, 0);
    private static readonly Point3D MaginciaGate = new(3564, 2140, 31);

    /// <summary>
    /// How far the pick's planned trip may run past the shortest: the reach and the plan each
    /// prove the walk to a start with their own few tile tries, so the two may set out from
    /// starts up to a leg apart.
    /// </summary>
    private const double TripSlackTiles = NavLimits.MaxLegDistance;

    private static readonly ResSource SpiritualityAnkh = Shrine(new Point3D(1594, 2489, 20));
    private static readonly ResSource ChaosAnkh = Shrine(new Point3D(1458, 844, 0));
    private static readonly ResSource JusticeAnkh = Shrine(new Point3D(1301, 639, 16));
    private static readonly ResSource HonestyAnkh = Shrine(new Point3D(4217, 564, 36));
    private static readonly ResSource BritainHealer = Healer(new Point3D(1471, 1611, 20));
    private static readonly ResSource TrinsicHealer = Healer(new Point3D(1911, 2805, 0));
    private static readonly ResSource MoonglowHealer = Healer(new Point3D(4392, 1089, 0));
    private static readonly ResSource ValorShrine = Shrine(new Point3D(2492, 3932, 2));

    private static readonly ResSource[] MainlandSites =
        [SpiritualityAnkh, ChaosAnkh, JusticeAnkh, HonestyAnkh, BritainHealer, TrinsicHealer, MoonglowHealer];

    private static readonly ResSource[] Sites = [.. MainlandSites, ValorShrine];
    private static readonly Point3D[] Starts = [DeceitDoor, BritainStreet, TrinsicStreet, ValorIsland, MaginciaGate];

    [RealMapFact]
    public void EveryStart_PicksTheSiteWithTheShortestPlannedTrip_OrNoneWhenNoSiteHasAWay() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var graph = NavWorld.GraphFor(RealMapWorld.FacetName);
            var walker = RealMapWorld.Walker();

            foreach (var from in Starts)
            {
                double? shortest = null;

                foreach (var site in Sites)
                {
                    if (PlannedTrip(graph, walker, from, site) is { } tiles && (shortest is not { } best || tiles < best))
                    {
                        shortest = tiles;
                    }
                }

                var pick = GhostSeek.ShortestTrip(from, Sites, Reach(from).TripTiles);
                var pickTrip = pick is { } picked ? PlannedTrip(graph, walker, from, picked) : null;
                output.WriteLine($"{from}: picks {pick?.Location} ({pickTrip:F0} tiles), shortest planned trip {shortest:F0} tiles");

                Assert.Equal(shortest == null, pick == null);

                if (shortest is { } floor)
                {
                    Assert.NotNull(pickTrip);
                    Assert.True(pickTrip.Value <= floor + TripSlackTiles, $"{from}: the pick's trip is {pickTrip:F0} tiles, the shortest {floor:F0}");
                }
            }
        });

    [RealMapFact]
    public void OnTheSealedValorIsland_TheGhostReachesItsShrine_AndNothingOnTheMainland() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            Assert.NotNull(Reach(ValorIsland).TripTiles(ValorShrine));

            // With no way to any site the look picks none, and the ghost falls back.
            Assert.Null(GhostSeek.ShortestTrip(ValorIsland, MainlandSites, Reach(ValorIsland).TripTiles));
        });

    [RealMapFact]
    public void InTown_TheTownHealerIsReachable() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            Assert.NotNull(Reach(BritainStreet).TripTiles(BritainHealer));
            Assert.NotNull(Reach(TrinsicStreet).TripTiles(TrinsicHealer));
        });

    [RealMapFact]
    public void EveryVerdict_IsTheOneTheGhostsTripWouldReach() =>
        RealMapWorld.WithLiveGraph(() =>
        {
            var graph = NavWorld.GraphFor(RealMapWorld.FacetName);
            var walker = RealMapWorld.Walker();

            foreach (var from in Starts)
            {
                foreach (var site in Sites)
                {
                    var hasAWay = PlannedTrip(graph, walker, from, site) != null;
                    var reaches = Reach(from).TripTiles(site) != null;
                    output.WriteLine($"{from} to {site.Location}: reach {reaches}, a way {hasAWay}");

                    Assert.Equal(hasAWay, reaches);
                }
            }
        });

    /// <summary>
    /// The tiles of what the ghost's trip itself finds, or null for no way: a real plan over the
    /// graph (the walk to its first node, each road step or gate, and on to the site), or a tile
    /// route.
    /// </summary>
    private static double? PlannedTrip(NavGraph graph, TileWalker walker, Point3D from, ResSource site)
    {
        if (Traveler.PreferredGoal(graph, from, site.Location) is { } goal &&
            Traveler.PlanNames(graph, from, goal.Name, walker, RealMapWorld.IsIndoor, avoid: null, out _) is { Count: > 0 } plan)
        {
            return RoadTiles(graph, from, plan) + NavMetric.Distance(goal.Location, site.Location);
        }

        var route = TileRoute.Find(from, site.Location, walker, RealMapWorld.IsIndoor, site.Range);
        return route.Count > 0 ? GhostSeek.RouteTiles(from, route) : null;
    }

    private static double RoadTiles(NavGraph graph, Point3D from, IReadOnlyList<string> plan)
    {
        graph.TryGetNode(plan[0], out var at);
        var tiles = NavMetric.Distance(from, at.Location);
        var gateCost = SosariaSettings.Characters?.Nav?.GateCost ?? NavSettings.DefaultGateCost;

        for (var i = 1; i < plan.Count; i++)
        {
            graph.TryGetNode(plan[i], out var next);
            tiles += graph.IsGate(at.Name, next.Name) ? gateCost : NavMetric.Distance(at.Location, next.Location);
            at = next;
        }

        return tiles;
    }

    private static GhostReach Reach(Point3D from) =>
        GhostReach.For(RealMapWorld.Felucca, RealMapWorld.FacetName, from, murderer: false);

    private static ResSource Shrine(Point3D spot) => new(GhostSeek.KindShrine, spot, GhostRules.ShrineStandTiles);

    private static ResSource Healer(Point3D spot) => new(GhostSeek.KindHealer, spot, GhostRules.HealerRange);
}
