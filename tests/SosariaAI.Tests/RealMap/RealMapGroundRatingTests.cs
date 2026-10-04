using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;
using Xunit.Abstractions;

namespace SosariaAI.Tests.RealMap;

/// <summary>
/// The dungeon floors and hunt grounds of the real Felucca, rated from the server's own
/// Second Age spawners, the live nav graph and the engine's own creatures: the floors carry the
/// levels players give them, and each floor and camp asks the power its spawn deserves.
/// </summary>
[Collection(RealMapCollection.Name)]
public class RealMapGroundRatingTests
{
    private const string Deceit = "Deceit";
    private const string Despise = "Despise";
    private const string Destard = "Destard";
    private const string Hythloth = "Hythloth";
    private const string Shame = "Shame";
    private const string OrcCave = "Orc Cave";
    private const string Covetous = "Covetous";
    private const string TrinsicPassage = "Trinsic Passage";

    /// <summary>
    /// A fresh fighter, about a journeyman of the live shard (novices 110, journeymen 127 at the
    /// median): the newbie ground is its. The graveyard's spectres cast, and rate about 130.
    /// </summary>
    private const int NewFighterPower = 130;

    /// <summary>
    /// A seasoned fighter, between a master and a grandmaster of the live shard (165 and 187 at
    /// the median): the first floors of the easy dungeons are its, the deep ones not. The Orc
    /// Cave's first floor is a quarter orcish lords, and rates by them.
    /// </summary>
    private const int SeasonedFighterPower = 190;

    /// <summary>No lone fighter of this power, the shard's top tenth, takes a floor of Destard.</summary>
    private const int StrongLonePower = 225;

    /// <summary>A median fighter of the live shard keeps off the Cove swamp and the Trinsic Passage.</summary>
    private const int MedianPower = 141;

    private static readonly Point3D DeceitLevel4 = new(5306, 652, 2);
    private static readonly Point3D HythlothLevel3 = new(6083, 145, -20);
    private static readonly Point3D CovetousLevel2 = new(5614, 1997, 0);
    private static readonly Point3D DespiseEntryway = new(5587, 631, 30);
    private static readonly Point3D ShameLevel4 = new(5875, 20, -5);

    /// <summary>
    /// The Jhelom lizardman and ratman camp, the Cove scorpions (with the earth elementals beside
    /// them), the Yew orc fort, the Britain graveyard and the Cove swamp of bog things.
    /// </summary>
    private static readonly (string Name, Point3D At)[] Camps =
    [
        ("Jhelom lizardmen and ratmen", new Point3D(1131, 3470, 0)),
        ("Cove scorpions", new Point3D(1930, 890, 0)),
        ("Yew orc fort", new Point3D(633, 1484, 0)),
        ("Britain graveyard", new Point3D(1369, 1475, 0)),
        ("Cove bog-thing swamp", new Point3D(2035, 1005, 0))
    ];

    private static readonly string[] NewbieCamps = ["Jhelom lizardmen and ratmen", "Britain graveyard"];
    private const string Swamp = "Cove bog-thing swamp";

    /// <summary>The level players give the Destard floor the Despise and Fire pads drop a walker onto.</summary>
    private const int DestardThirdLevel = 3;

    private static readonly Point3D DestardLevel3 = new(5164, 1009, 0);
    private static readonly Point3D DespiseLevel3 = new(5600, 800, 60);
    private static readonly Point3D DestardDoorHome = new(1170, 2643, 2);
    private static readonly (int X, int Y) DespisePadOntoDestard = (5506, 814);
    private static readonly (int X, int Y) FirePadOntoDestard = (5682, 1437);

    /// <summary>A Deceit first-level spot where weak walkers stood with no road home.</summary>
    private static readonly Point3D DeceitLevel1 = new(5205, 557, 0);

    private static readonly Point3D MoonglowGate = new(4467, 1283, 5);

    /// <summary>Where the pad in from the Deceit door sets a person down, and a spot on Deceit's second level.</summary>
    private static readonly Point3D DeceitDoor = new(5187, 639, 0);

    private static readonly Point3D DeceitLevel2 = new(5327, 542, 0);

    private static readonly Lazy<(DungeonMap Floors, List<SpawnPoint> Spawn, NavGraph Graph)> World = new(Load);

    private readonly ITestOutputHelper _output;

    public RealMapGroundRatingTests(ITestOutputHelper output)
    {
        _output = output;

        if (RealMapWorld.Available)
        {
            KitWorld.EnsureBeasts();
        }
    }

    [RealMapFact]
    public void Levels_AreThoseTheWorldsLocationListGives()
    {
        var floors = World.Value.Floors;

        Assert.Equal(4, floors.FloorAt(DeceitLevel4)?.Level);
        Assert.Equal(3, floors.FloorAt(HythlothLevel3)?.Level);
        Assert.Equal(2, floors.FloorAt(CovetousLevel2)?.Level);
        Assert.Equal(DungeonMap.FirstLevel, floors.FloorAt(DespiseEntryway)?.Level);
        Assert.Equal(4, floors.FloorAt(ShameLevel4)?.Level);
    }

    [RealMapFact]
    public void Stairs_DownLeadDeeper_UpLeadShallower()
    {
        var floors = World.Value.Floors;

        foreach (var floor in floors.Floors)
        {
            Assert.All(floors.StairsDown(floor.Id), stair => Assert.True(floors.Floor(stair.ToFloor)?.Level >= floor.Level));
            Assert.All(floors.StairsUp(floor.Id), stair => Assert.True(floors.Floor(stair.ToFloor)?.Level <= floor.Level));
        }

        // The Despise landing leads down to its first level, though both are level 1.
        var landing = floors.FloorAt(DespiseEntryway)!.Value;
        Assert.Contains(floors.StairsDown(landing.Id), stair => floors.Floor(stair.ToFloor)?.Level == DungeonMap.FirstLevel);

        // Hythloth's balron floor has an exit pad; its stair to the second level is a stair up.
        var third = floors.FloorAt(HythlothLevel3)!.Value;
        Assert.DoesNotContain(floors.StairsDown(third.Id), stair => floors.Floor(stair.ToFloor)?.Level == 2);
    }

    [RealMapFact]
    public void TopFloors_OfTheEasyDungeons_FitASeasonedFighter_TheirDeepFloorsDoNot()
    {
        var floors = World.Value.Floors;
        Report(floors);

        foreach (var dungeon in new[] { Deceit, Despise, Shame, OrcCave })
        {
            var top = Hardest(floors, dungeon, DungeonMap.FirstLevel);
            var deepest = floors.Floors.Where(floor => floor.Dungeon == dungeon).Max(floor => floor.Difficulty);

            Assert.True(HuntGround.InReach(top, SeasonedFighterPower), $"{dungeon} level 1: {top}");
            Assert.False(HuntGround.InReach(deepest, SeasonedFighterPower), $"{dungeon} deepest: {deepest}");
        }

        // Deceit's skeletons and Despise's lizardmen are a fresh fighter's first dungeon.
        Assert.True(HuntGround.InReach(Hardest(floors, Deceit, DungeonMap.FirstLevel), NewFighterPower));
        Assert.True(HuntGround.InReach(Hardest(floors, Despise, DungeonMap.FirstLevel), NewFighterPower));
    }

    [RealMapFact]
    public void Destard_IsBeyondEveryLoneFighterOnEveryFloor()
    {
        var floors = World.Value.Floors;

        Assert.All(
            floors.Floors.Where(floor => floor.Dungeon == Destard && floor.Difficulty > 0),
            floor => Assert.False(HuntGround.InReach(floor.Difficulty, StrongLonePower), $"Destard level {floor.Level}: {floor.Difficulty}")
        );
    }

    [RealMapFact]
    public void TrinsicPassage_CountsTheDreadSpidersSpellsAndPoison()
    {
        var floors = World.Value.Floors;

        // Five fighters of power 128 to 225 died there to dread spiders their blows rated at 146.
        Assert.False(HuntGround.InReach(Hardest(floors, TrinsicPassage, DungeonMap.FirstLevel), MedianPower));
    }

    [RealMapFact]
    public void NewbieCamps_DrawTheWeak_TheSwampDoesNot()
    {
        var (floors, spawn, _) = World.Value;
        var rated = Camps.ToDictionary(camp => camp.Name, camp => Rated(spawn, camp.At));
        Report(floors);

        foreach (var (name, difficulty) in rated)
        {
            _output.WriteLine($"{name}: difficulty {difficulty}, power bar {HuntGround.PowerBar(difficulty)}");
        }

        Assert.All(NewbieCamps, camp => Assert.True(HuntGround.InReach(rated[camp], NewFighterPower), camp));
        Assert.False(HuntGround.InReach(rated[Swamp], MedianPower));
    }

    /// <summary>
    /// Despise's third level and Fire drop a walker by one-way pads onto Destard's third level,
    /// with no pad back. The road home from Despise to a house by the Destard door once took
    /// them, three drops deep; a seasoned fighter, whom none of Destard fits, takes none.
    /// </summary>
    [RealMapFact]
    public void GoHome_FromDespise_TakesNoDropOntoAFloorAboveTheWalkersReach()
    {
        var (floors, _, graph) = World.Value;
        var destard = floors.FloorAt(DestardLevel3)!.Value;
        var ontoDestard = floors.Drops.Where(drop => drop.ToFloor == destard.Id).ToList();
        var barred = FloorDrops.AboveReach(floors, SeasonedFighterPower)!;
        var home = graph.FindNearest(DestardDoorHome);
        var ontoDestardPads = ontoDestard.Select(drop => EdgeHealthRules.Key(drop.Pad.Index, drop.Landing.Index)).ToHashSet();

        Assert.Equal(DestardThirdLevel, destard.Level);
        Assert.Contains(ontoDestard, drop => (drop.Pad.X, drop.Pad.Y) == DespisePadOntoDestard);
        Assert.Contains(ontoDestard, drop => (drop.Pad.X, drop.Pad.Y) == FirePadOntoDestard);
        Assert.All(ontoDestardPads, pad => Assert.Contains(pad, barred));

        foreach (var from in new[] { DespiseEntryway, DespiseLevel3 })
        {
            var start = graph.FindNearest(from);
            var seasoned = RealMapRedReachTests.Road(graph, home, start, FloorDrops.BarsAboveReach(floors, SeasonedFighterPower));

            Assert.NotEmpty(seasoned);
            Assert.False(TakesAny(seasoned, graph, barred), $"{from}: the road home takes a drop onto a floor above the walker's reach");
        }
    }

    /// <summary>
    /// Deceit stands on an island with no moongate, and its one road off it runs down its floors,
    /// onto Fire and up through Despise's third level. A new fighter keeps off the pads onto that
    /// floor and has no road home at all; with the pads open (<see cref="PathSearchBars.OpeningPads"/>),
    /// the second search travel runs then, it gets out.
    /// </summary>
    [RealMapFact]
    public void GoHome_FromDeceit_AWeakWalkerGetsOutOverThePads()
    {
        var (floors, _, graph) = World.Value;
        var start = graph.FindNearest(DeceitLevel1);
        var goal = graph.FindNearest(MoonglowGate);
        var bars = FloorDrops.BarsAboveReach(floors, NewFighterPower)!;

        Assert.Equal(DungeonMap.FirstLevel, floors.FloorAt(start.Location)?.Level);
        Assert.Empty(RealMapRedReachTests.Road(graph, goal, start, bars));
        Assert.True(bars.MayOpenPads);
        Assert.NotEmpty(RealMapRedReachTests.Road(graph, goal, start, bars.OpeningPads()));
    }

    /// <summary>
    /// A crawler bound for Deceit's second level that reaches the door weaker than it set out
    /// finds the pads onto that floor barred, and its plan from the door ends with the walk to
    /// the first node blocked, not with no path. With the pads open the plan goes in.
    /// </summary>
    [RealMapFact]
    public void PlanFromTheDeceitDoor_BarredBelowItsFloor_IsBlocked_AndGoesInWithThePadsOpen()
    {
        var (floors, _, graph) = World.Value;
        var walker = RealMapWorld.Walker();
        var goal = graph.FindNearest(DeceitLevel2);
        var bars = FloorDrops.BarsAboveReach(floors, NewFighterPower)!;

        Assert.Equal(2, floors.FloorAt(goal.Location)?.Level);
        Assert.Empty(PlanFrom(graph, walker, DeceitDoor, goal, bars, out var why));
        Assert.True(Traveler.IsNoRoad(why), why);
        Assert.NotEmpty(PlanFrom(graph, walker, DeceitDoor, goal, bars.OpeningPads(), out why));
    }

    /// <summary>The travel plan from <paramref name="from"/> to <paramref name="goal"/>: a search from the goal under the bars, then the walker's first hop.</summary>
    private static IReadOnlyList<string> PlanFrom(NavGraph graph, TileWalker walker, Point3D from, NavNode goal, PathSearchBars bars, out string why)
    {
        var cost = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var prev = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        NavSearch.Explore(graph, goal.Name, null, NavSearch.DefaultGateCost, null, null, cost, prev, bars);
        return Traveler.FinishPlan(graph, from, goal.Name, prev, walker, RealMapWorld.IsIndoor, null, out why);
    }

    /// <summary>True when the road steps between two nodes whose leg is one of <paramref name="pads"/>.</summary>
    private static bool TakesAny(IReadOnlyList<string> road, NavGraph graph, IReadOnlySet<long> pads)
    {
        for (var i = 1; i < road.Count; i++)
        {
            if (pads.Contains(EdgeHealthRules.Key(graph.SearchIndex(road[i - 1]), graph.SearchIndex(road[i]))))
            {
                return true;
            }
        }

        return false;
    }

    private static int Rated(IReadOnlyList<SpawnPoint> spawn, Point3D at) =>
        AreaDifficulty.SpawnScore(GroundRating.AroundSpot(spawn, at, null), CreatureStatsLookup.TryLive);

    private static int Hardest(DungeonMap floors, string dungeon, int level) =>
        floors.Floors.Where(floor => floor.Dungeon == dungeon && floor.Level == level).Max(floor => floor.Difficulty);

    private void Report(DungeonMap floors)
    {
        foreach (var dungeon in new[] { Deceit, Shame, Despise, OrcCave, Destard, Covetous, Hythloth, TrinsicPassage })
        {
            foreach (var floor in floors.Floors.Where(floor => floor.Dungeon == dungeon && floor.Difficulty > 0).OrderBy(floor => floor.Level))
            {
                _output.WriteLine(
                    $"{dungeon} level {floor.Level} (floor {floor.Id}): difficulty {floor.Difficulty}, power bar {HuntGround.PowerBar(floor.Difficulty)}"
                );
            }
        }
    }

    private static (DungeonMap, List<SpawnPoint>, NavGraph) Load()
    {
        var felucca = RealMapWorld.Felucca;
        var book = RealMapWorld.PlaceBook();
        var placeAt = DungeonGround.RegionNames(felucca, () => book);
        var spawn = GroundRating.Points(
            WorldGenerator.SpawnersFor(RealMapWorld.ServerDataRoot, RealMapWorld.FacetName, WorldSetupRules.T2ASpawnSets),
            CreatureStatsLookup.IsLandEnemy,
            placeAt
        );
        var graph = RealMapWorld.LiveGraph();
        var floors = DungeonMap.Build(
            graph,
            node => placeAt(node.Location),
            DungeonMap.MarksIn(WorldGenerator.LocationsFor(RealMapWorld.ServerDataRoot, RealMapWorld.FacetName)),
            spawn,
            CreatureStatsLookup.TryLive
        );
        return (floors, spawn, graph);
    }
}
