using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using Xunit;

namespace SosariaAI.Tests;

public class DungeonMapTests
{
    private const string Dungeon = "Testdun";
    private const int DungeonBlockX = 5120;

    [Fact]
    public void Build_DoorFloorIsLevelOneAndEachStairAddsOne()
    {
        var map = Build();

        Assert.Equal(DungeonMap.FirstLevel, map.FloorOfNode("l1b")?.Level);
        Assert.Equal(DungeonMap.FirstLevel + 1, map.FloorOfNode("l2b")?.Level);
        Assert.Equal(Dungeon, map.FloorOfNode("l2b")?.Dungeon);
        Assert.Equal(DungeonMap.FirstLevel + 1, map.FloorOfNode("l2a")?.Level);
    }

    [Fact]
    public void Build_OverlandAndUnreachedFloorsAreNoFloor()
    {
        var map = Build();

        Assert.Null(map.FloorOfNode("town"));
        Assert.Null(map.FloorOfNode("lonely"));
        Assert.Null(map.FloorOfNode("nosuchnode"));
    }

    [Fact]
    public void Rooms_LeavePadsOut()
    {
        var map = Build();
        var floor = map.FloorOfNode("l1b").Value;
        var rooms = map.Rooms(floor.Id).Select(n => n.Name).ToList();

        Assert.Equal(["l1b"], rooms);
    }

    [Fact]
    public void StairsDown_LeadToTheNextLevel()
    {
        var map = Build();
        var floor = map.FloorOfNode("l1b").Value;
        var stair = Assert.Single(map.StairsDown(floor.Id));

        Assert.Equal("l1c", stair.Pad.Name);
        Assert.Equal("l2a", stair.Landing.Name);
        Assert.Equal(map.FloorOfNode("l2a")?.Id, stair.ToFloor);
        Assert.Empty(map.StairsDown(stair.ToFloor));
    }

    [Fact]
    public void StairsDown_SkipAOneWayPadThatOnlyComesUp()
    {
        // A pad on level 2 that sends a person up to level 1 is no way down from level 1.
        var map = Build(upExit: true);
        var floor = map.FloorOfNode("l1b").Value;
        var stair = Assert.Single(map.StairsDown(floor.Id));

        Assert.Equal("l1c", stair.Pad.Name);
    }

    [Fact]
    public void FloorAt_ReadsTheNearestNodeWithinOneLeg()
    {
        var map = Build();

        Assert.Equal(DungeonMap.FirstLevel + 1, map.FloorAt(new Point3D(5322, 1002, 0))?.Level);
        Assert.Null(map.FloorAt(new Point3D(2000, 2000, 0)));
    }

    [Fact]
    public void Build_NoGraph_IsEmpty()
    {
        Assert.Empty(DungeonMap.Build(null, _ => Dungeon).Rooms(0));
        Assert.Empty(DungeonMap.Empty.Rooms(0));
        Assert.Null(DungeonMap.Empty.FloorAt(Point3D.Zero));
    }

    private static DungeonMap Build(bool upExit = false)
    {
        var town = Node("town", 1000, 900, "door");
        var door = Node("door", 1010, 900, "town");
        var l1a = Node("l1a", 5200, 900, "l1b");
        var l1b = Node("l1b", 5220, 900, "l1a", "l1c");
        var l1c = Node("l1c", 5240, 900, "l1b");
        var l2a = Node("l2a", 5300, 1000, "l2b");
        var l2b = Node("l2b", 5320, 1000, "l2a");
        var lonely = Node("lonely", 5500, 500);
        NavGates.Add(door, l1a, NavGateKind.Teleporter);
        NavGates.Add(l1c, l2a, NavGateKind.Teleporter);

        if (upExit)
        {
            NavGates.AddOneWay(l2b, l1a, NavGateKind.Teleporter);
        }

        var graph = new NavGraph("Felucca", new List<NavNode> { town, door, l1a, l1b, l1c, l2a, l2b, lonely });
        return DungeonMap.Build(graph, node => node.X >= DungeonBlockX ? Dungeon : null);
    }

    private static NavNode Node(string name, int x, int y, params string[] connects) =>
        new()
        {
            Name = name,
            X = x,
            Y = y,
            Z = 0,
            Connects = [.. connects]
        };

    [Fact]
    public void Build_LevelsFollowTheMarks_AnExitPadDoesNotMakeAFloorLevelOne()
    {
        var map = Marked();

        Assert.Equal(1, map.FloorOfNode("land")?.Level);
        Assert.Equal(1, map.FloorOfNode("r1a")?.Level);
        Assert.Equal(2, map.FloorOfNode("r2a")?.Level);
        Assert.Equal(3, map.FloorOfNode("r3a")?.Level);
        Assert.Equal(4, map.FloorOfNode("r4a")?.Level);
        Assert.Equal(4, map.FloorOfNode("g")?.Level);
    }

    [Fact]
    public void Stairs_ByRealLevel_TheExitFloorsPadBackIsAStairUp_TheLandingLeadsDown()
    {
        var map = Marked();
        var fourth = map.FloorOfNode("r4a").Value;
        var third = map.FloorOfNode("r3a").Value;
        var landing = map.FloorOfNode("land").Value;

        Assert.Empty(map.StairsDown(fourth.Id));
        Assert.Equal(third.Id, Assert.Single(map.StairsUp(fourth.Id)).ToFloor);
        Assert.Contains(map.StairsDown(third.Id), stair => stair.ToFloor == map.FloorOfNode("g")?.Id);
        Assert.Equal(map.FloorOfNode("r1a")?.Id, Assert.Single(map.StairsDown(landing.Id)).ToFloor);
    }

    [Fact]
    public void Build_RatesEachFloorByItsOwnSpawn_OfItsOwnDungeon()
    {
        var map = Marked(
            [
                new SpawnPoint(new Point3D(5320, 900, 0), SpawnRange, Weak, WeakCount, Dungeon),
                new SpawnPoint(new Point3D(5520, 1100, 0), SpawnRange, Strong, 1, Dungeon),
                new SpawnPoint(new Point3D(5320, 900, 0), SpawnRange, Strong, 1, OtherDungeon)
            ]
        );

        Assert.Equal(WeakThreat, map.FloorOfNode("r1a")?.Difficulty);
        Assert.Equal(StrongThreat, map.FloorOfNode("r3a")?.Difficulty);
        Assert.Equal(0, map.FloorOfNode("r2a")?.Difficulty);
    }

    [Fact]
    public void MarksIn_ReadsTheLevelLocationsOnly()
    {
        var marks = DungeonMap.MarksIn(
            [
                new NamedSeed("Level 3", "Felucca/Dungeons/Deceit", "Felucca", 5137, 650, 5),
                new NamedSeed("Entrance", "Felucca/Dungeons/Deceit", "Felucca", 4111, 432, 5),
                new NamedSeed("Level Up", "Felucca/Dungeons/Deceit", "Felucca", 1, 1, 0)
            ]
        );

        Assert.Equal(new[] { new LevelMark(new Point3D(5137, 650, 5), 3) }, marks);
    }

    [Fact]
    public void FloorsLine_NamesThePowerEachFloorAsks()
    {
        DungeonFloor[] floors = [new(Dungeon, 2, 1, StrongThreat), new(Dungeon, 1, 0, WeakThreat), new(Dungeon, 3, 2, 0)];

        Assert.Equal(
            $"Floors of {Dungeon}: level 1 asks power {HuntGround.PowerBar(WeakThreat)}, level 2 asks power {HuntGround.PowerBar(StrongThreat)}",
            DungeonAtlas.FloorsLine(Dungeon, floors)
        );
    }

    private const string OtherDungeon = "Otherdun";
    private const string Weak = "Skeleton";
    private const string Strong = "Lich";
    private const int WeakThreat = 84;
    private const int StrongThreat = 300;
    private const int WeakCount = 20;
    private const int SpawnRange = 10;

    /// <summary>
    /// A dungeon laid out like Deceit and Despise: a door landing with no mark, marked levels 1 to
    /// 3 down a stair each, a fourth level with an exit pad out and a one-way pad up to the third,
    /// and an unmarked floor down a one-way pad from the third.
    /// </summary>
    private static DungeonMap Marked(IReadOnlyList<SpawnPoint> spawn = null)
    {
        var town = Node("town", 1000, 900, "door");
        var door = Node("door", 1010, 900, "town");
        var land = Node("land", 5200, 900);
        var r1a = Node("r1a", 5300, 900, "r1b");
        var r1b = Node("r1b", 5320, 900, "r1a");
        var r2a = Node("r2a", 5400, 1000, "r2b");
        var r2b = Node("r2b", 5420, 1000, "r2a");
        var r3a = Node("r3a", 5500, 1100, "r3b");
        var r3b = Node("r3b", 5520, 1100, "r3a");
        var r4a = Node("r4a", 5600, 1200, "r4b");
        var r4b = Node("r4b", 5620, 1200, "r4a");
        var g = Node("g", 5700, 1300);
        NavGates.Add(door, land, NavGateKind.Teleporter);
        NavGates.Add(land, r1a, NavGateKind.Teleporter);
        NavGates.Add(r1b, r2a, NavGateKind.Teleporter);
        NavGates.Add(r2b, r3a, NavGateKind.Teleporter);
        NavGates.AddOneWay(r4b, town, NavGateKind.Teleporter);
        NavGates.AddOneWay(r4a, r3b, NavGateKind.Teleporter);
        NavGates.AddOneWay(r3b, g, NavGateKind.Teleporter);
        var graph = new NavGraph("Felucca", new List<NavNode> { town, door, land, r1a, r1b, r2a, r2b, r3a, r3b, r4a, r4b, g });
        LevelMark[] marks = [new(r1a.Location, 1), new(r2a.Location, 2), new(r3a.Location, 3), new(r4a.Location, 4)];
        return DungeonMap.Build(graph, node => node.X >= DungeonBlockX ? Dungeon : null, marks, spawn, Lookup);
    }

    private static HostileStats? Lookup(string name) =>
        name == Weak ? new HostileStats(WeakThreat, 0, 0) : name == Strong ? new HostileStats(StrongThreat, 0, 0) : null;
}
