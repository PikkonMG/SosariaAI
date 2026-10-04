using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A hunt spot is rated by what stands on its ground: a dungeon hall by its whole floor, a spot
/// on open ground by the spawn whose walk reaches its hunt square, foes only.
/// </summary>
public class GroundRatingTests
{
    private const string Swamp = "Swamp";
    private const string Dungeon = "Testdun";
    private const string BogThing = "BogThing";
    private const string Lizardman = "Lizardman";
    private const string Rabbit = "Rabbit";
    private const string Banker = "Banker";
    private const string Dragon = "Dragon";
    private const string Skeleton = "Skeleton";
    private const int BogThingThreat = 876;
    private const int LizardmanThreat = 115;
    private const int DragonThreat = 1277;
    private const int SkeletonThreat = 84;
    private const int HomeRange = 10;
    private const int DungeonBlockX = 5120;
    private const string HuntKind = "Hunt";
    private const double LizardmenStanding = 1.5;

    private static readonly Point3D Camp = new(1131, 3470, 0);
    private static readonly Point3D Hall = new(5220, 900, 0);
    private static readonly Point3D DragonCave = new(5230, 905, 0);

    [Fact]
    public void Points_KeepEveryFoeWithItsStandingCount_AndItsPlace()
    {
        var spawner = new SpawnerSeed(
            "Felucca", Camp.X, Camp.Y, Camp.Z, HomeRange, [Lizardman, Rabbit, Banker], 4,
            [new SpawnEntry(Lizardman, 3, 100), new SpawnEntry(Rabbit, 3, 100), new SpawnEntry(Banker, 1, 100)]
        );

        var point = Assert.Single(GroundRating.Points([spawner], name => name != Rabbit, _ => Swamp));

        // Four stand at once: one banker at most, and the rest shared by the lizardmen and rabbits.
        Assert.Equal(new SpawnPoint(Camp, HomeRange, Lizardman, LizardmenStanding, Swamp), point);
    }

    [Fact]
    public void AroundSpot_TheSpawnWhoseWalkReachesTheSquare_InTheSamePlaceOnly()
    {
        var reach = GroundRating.SpotRange + HomeRange;
        SpawnPoint[] points =
        [
            new(new Point3D(Camp.X + reach, Camp.Y, 0), HomeRange, Lizardman, 1, null),
            new(new Point3D(Camp.X + reach + 1, Camp.Y, 0), HomeRange, BogThing, 1, null),
            new(Camp, HomeRange, Skeleton, 1, Dungeon)
        ];

        var spawn = GroundRating.AroundSpot(points, Camp, null);

        Assert.Equal(new[] { Lizardman }, spawn.Select(count => count.Creature));
    }

    [Fact]
    public void Rate_AHallTakesItsFloor_AnOpenSpotItsGround()
    {
        var floors = Floors(out var floorSpawn);
        var camp = Spot(Lizardman, Camp);
        var hall = Spot(Skeleton, Hall);
        var catalog = new DestinationCatalog([camp, hall]);
        List<SpawnPoint> points = [.. floorSpawn, new(Camp, HomeRange, Lizardman, 8, null)];

        GroundRating.Rate(catalog, floors, points, PlaceAt, Lookup);

        Assert.Equal(LizardmanThreat, camp.Difficulty);
        Assert.Equal(floors.FloorAt(Hall)!.Value.Difficulty, hall.Difficulty);
        Assert.Equal((int)(DragonThreat * AreaDifficulty.BossShare), hall.Difficulty);
    }

    [Fact]
    public void Rate_ASpotWithNoFoeRatesZero_SoNobodyIsSentThere()
    {
        var empty = Spot(Lizardman, Camp);

        GroundRating.Rate(new DestinationCatalog([empty]), DungeonMap.Empty, [], PlaceAt, Lookup);

        Assert.Equal(0, empty.Difficulty);
    }

    /// <summary>One dungeon floor of forty skeletons and one dragon, reached from a town door.</summary>
    private static DungeonMap Floors(out List<SpawnPoint> spawn)
    {
        var town = Node("town", 1000, 900, "door");
        var door = Node("door", 1010, 900, "town");
        var a = Node("a", 5200, 900, "b");
        var b = Node("b", 5220, 900, "a");
        NavGates.Add(door, a, NavGateKind.Teleporter);
        var graph = new NavGraph("Felucca", new List<NavNode> { town, door, a, b });
        spawn =
        [
            new(Hall, HomeRange, Skeleton, 40, Dungeon),
            new(DragonCave, HomeRange, Dragon, 1, Dungeon)
        ];
        return DungeonMap.Build(graph, node => node.X >= DungeonBlockX ? Dungeon : null, null, spawn, Lookup);
    }

    private static string PlaceAt(Point3D at) => at.X >= DungeonBlockX ? Dungeon : null;

    private static HostileStats? Lookup(string name) =>
        name switch
        {
            Lizardman => new HostileStats(LizardmanThreat, 0, 0),
            BogThing => new HostileStats(BogThingThreat, 0, 0),
            Dragon => new HostileStats(DragonThreat, 0, 0),
            Skeleton => new HostileStats(SkeletonThreat, 0, 0),
            _ => null
        };

    private static Destination Spot(string role, Point3D at) =>
        new() { Name = $"{role} {at.X}-{at.Y}", Kind = HuntKind, Role = role, X = at.X, Y = at.Y, Z = at.Z };

    private static NavNode Node(string name, int x, int y, params string[] connects) =>
        new() { Name = name, X = x, Y = y, Z = 0, Connects = [.. connects] };
}
