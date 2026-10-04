using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class PlaceRulesTests
{
    private const int AreaSize = 100;
    private const int GroupingSize = 300;
    private const int Inside = 10;
    private const int OneTile = 1;
    private const int MiscParts = 2;
    private const Expansion SecondAge = Expansion.T2A;
    private const string KeepName = "Keep";
    private const string WalkinName = "Walkin";
    private const string TownName = "Town";
    private const string GroveSpawnFile = "BlightedGrove";

    private static readonly Point3D KeepDoor = new(100, 100, 0);
    private static readonly Point3D KeepPad = new(100, 110, 0);
    private static readonly Point3D KeepHall = new(1050, 1050, 0);
    private static readonly Point3D HoleEntrance = new(300, 300, 0);
    private static readonly Point3D FarEntrance = new(400, 400, 0);
    private static readonly Point3D WalkInEntrance = new(550, 550, 0);
    private static readonly Point3D TownGo = new(700, 700, 0);
    private static readonly Point3D GateEntrance = new(800, 800, 0);
    private static readonly Point3D MiscEntrance = new(900, 900, 0);
    private static readonly Point3D MiscFrontPad = new(900, 901, 0);
    private static readonly Point3D MiscSidePad = new(1200, 1200, 0);
    private static readonly Point3D MiscDeadPad = new(1500, 1500, 0);
    private static readonly Point3D NorthHall = new(5020, 5020, 0);
    private static readonly Point3D EastHall = new(5240, 5240, 0);
    private static readonly Point3D GroveEntrance = new(587, 1641, 0);
    private static readonly Point3D GrovePad = new(588, 1637, 0);
    private static readonly Point3D GroveHall = new(6472, 868, 0);

    private static PlaceRegion Region(string name, bool isTown, int x, int y, int size, Point3D entrance, Point3D go = default) =>
        new(
            name,
            isTown,
            [new Rectangle3D(new Point3D(x, y, Server.Region.MinZ), new Point3D(x + size, y + size, Server.Region.MaxZ))],
            entrance,
            go
        );

    private static Point3D Near(Point3D at, int offset) => new(at.X + offset, at.Y + offset, at.Z);

    private static readonly List<PlaceRegion> Regions =
    [
        Region(KeepName, false, 1000, 1000, AreaSize, KeepDoor),
        Region("The Hole", false, 2000, 2000, AreaSize, HoleEntrance),
        Region("Faraway", false, 3000, 3000, AreaSize, FarEntrance),
        Region(WalkinName, false, 500, 500, AreaSize, WalkInEntrance),
        Region("Empty", false, 6000, 6000, AreaSize, new Point3D(1300, 1300, 0)),
        Region(TownName, true, 650, 650, AreaSize, Point3D.Zero, TownGo),
        Region("Gate Town", true, 4000, 4000, AreaSize, GateEntrance),
        Region("Misc", false, 5000, 5000, GroupingSize, MiscEntrance)
    ];

    private static readonly List<WayIn> WaysIn =
    [
        new(KeepPad, KeepHall),
        new(new Point3D(FarEntrance.X, FarEntrance.Y + PlaceRules.DoorReach + OneTile, 0), new Point3D(3050, 3050, 0)),
        new(new Point3D(1300, 1301, 0), new Point3D(6050, 6050, 0)),
        new(MiscFrontPad, new Point3D(5010, 5010, 0)),
        new(MiscSidePad, new Point3D(5250, 5250, 0)),
        new(MiscDeadPad, new Point3D(5150, 5150, 0)),
        new(new Point3D(5100, 5100, 0), new Point3D(5110, 5110, 0))
    ];

    private static readonly List<SpawnFile> Spawns =
    [
        new("Keep", [Near(KeepHall, Inside)]),
        new("TheHole", [new Point3D(2050, 2050, 0)]),
        new("Faraway", [new Point3D(3050, 3050, 0)]),
        new("Walkin", [Near(WalkInEntrance, Inside)]),
        new("Townsfolk", [Near(TownGo, Inside), new Point3D(4050, 4050, 0)]),
        new("NorthSewer", [NorthHall, Near(NorthHall, Inside), Near(NorthHall, -Inside)]),
        new("EastPassage", [EastHall, Near(EastHall, Inside)]),
        new("Wide", [new Point3D(5200, 5200, 0), new Point3D(7000, 7000, 0), new Point3D(7010, 7010, 0)])
    ];

    /// <summary>A later era's dungeon whose pad works and whose spawns load in every era, as Blighted Grove on a Second Age shard.</summary>
    private static readonly List<PlaceRegion> GroveRegions = [Region(EraRules.BlightedGrove, false, 6440, 820, AreaSize, GroveEntrance)];

    private static readonly List<WayIn> GroveWaysIn = [new(GrovePad, GroveHall)];

    private static readonly List<SpawnFile> GroveSpawns = [new(GroveSpawnFile, [Near(GroveHall, Inside)])];

    private static List<Place> Book() => PlaceRules.Book(Regions, WaysIn, Spawns, SecondAge);

    private static List<Place> GroveBook(Expansion expansion) => PlaceRules.Book(GroveRegions, GroveWaysIn, GroveSpawns, expansion);

    [Fact]
    public void Book_KeepsPlacesWithAWorkingDoorAndLiveSpawns()
    {
        var names = Book().Select(place => place.Name).ToList();

        Assert.Equal(["Keep", "Walkin", "Town", "North Sewer", "East Passage"], names);
    }

    [Fact]
    public void Book_DoorIsThePadAtTheRecordedEntrance_TheMouthInside_OrTheTownGoLocation()
    {
        var byName = Book().ToDictionary(place => place.Name);

        Assert.Equal(KeepPad, byName["Keep"].Door);
        Assert.Equal(WalkInEntrance, byName["Walkin"].Door);
        Assert.Equal(TownGo, byName["Town"].Door);
        Assert.True(byName["Town"].IsTown);
    }

    [Fact]
    public void Book_GroupingRegion_ListsEachSpawnFileAsItsOwnDungeonAtItsOwnPad()
    {
        var parts = Book().Where(place => place.Region == "Misc").ToDictionary(place => place.Name);

        Assert.Equal(MiscParts, parts.Count);
        Assert.Equal(MiscFrontPad, parts["North Sewer"].Door);
        Assert.Equal(MiscSidePad, parts["East Passage"].Door);
    }

    [Fact]
    public void SpokenFileName_SplitsTheWords()
    {
        Assert.Equal("Britain Sewer", PlaceRules.SpokenFileName("BritainSewer"));
        Assert.Equal("Trinsic Passage", PlaceRules.SpokenFileName("TrinsicPassage"));
        Assert.Equal("Ice", PlaceRules.SpokenFileName("Ice"));
    }

    [Fact]
    public void PlaceBook_NamesKeptDroppedGroupedAndUnjudgedRegions()
    {
        var book = new PlaceBook(Regions, Book());

        Assert.Equal("Keep", book.Spoken("Keep", KeepHall));
        Assert.Null(book.Spoken("The Hole", new Point3D(2050, 2050, 0)));
        Assert.Equal("East Passage", book.Spoken("Misc", Near(EastHall, OneTile)));
        Assert.Equal("North Sewer", book.Spoken("Misc", Near(NorthHall, OneTile)));
        Assert.Equal("A Wheatfield", book.Spoken("A Wheatfield", TownGo));
        Assert.Equal(["Town"], book.Towns.Select(place => place.Name));
        Assert.Contains(book.Dropped, region => region.Name == "Gate Town");
    }

    [Fact]
    public void PlaceBook_DropsWhatLiesInNamesOrMarksTheDoorOfADroppedPlace()
    {
        var book = new PlaceBook(Regions, Book());
        var inside = new Destination { Name = "Orc 2050-2050", Kind = "Hunt", Role = "Orc", X = 2050, Y = 2050 };
        var named = new Destination { Name = "Hole Entrance", Kind = "Dungeon", Role = "Hole", X = 1, Y = 1 };
        var doorMark = new Destination { Name = "Pit", Kind = "Dungeon", Role = "DUNGEON", X = HoleEntrance.X + OneTile, Y = HoleEntrance.Y };
        var shopByGate = new Destination { Name = "Smith", Kind = "Vendor", Role = "Smith", X = GateEntrance.X + OneTile, Y = GateEntrance.Y };
        var keepHall = new Destination { Name = "Ogre 1050-1050", Kind = "Hunt", Role = "Ogre", X = KeepHall.X, Y = KeepHall.Y };

        Assert.True(book.IsDropped(inside));
        Assert.True(book.IsDropped(named));
        Assert.True(book.IsDropped(doorMark));
        Assert.False(book.IsDropped(shopByGate));
        Assert.False(book.IsDropped(keepHall));

        var filtered = book.Filter(new DestinationCatalog([inside, named, doorMark, shopByGate, keepHall]));

        Assert.Equal(["Smith", "Ogre 1050-1050"], filtered.All.Select(destination => destination.Name));
    }

    [Fact]
    public void PlaceSpots_AreEachPlaceAtItsDoor()
    {
        var spots = PanelRules.PlaceSpots(Book());

        Assert.Contains(new TravelSpot("Keep", KeepPad), spots);
        Assert.Contains(new TravelSpot("North Sewer", MiscFrontPad), spots);
    }

    [Fact]
    public void Book_DropsALaterErasPlace_ThoughItsPadWorksAndItSpawns()
    {
        Assert.Empty(GroveBook(SecondAge));
        Assert.Equal([EraRules.BlightedGrove], GroveBook(Expansion.ML).Select(place => place.Name));
    }

    [Fact]
    public void PlaceBook_DroppedAt_FindsTheLaterErasRegionAndItsEntranceOutside()
    {
        var book = new PlaceBook(GroveRegions, GroveBook(SecondAge));
        var grove = book.DroppedAt(GroveHall);

        Assert.Equal(EraRules.BlightedGrove, grove?.Name);
        Assert.Equal(GroveEntrance, grove.OutsideEntrance);
        Assert.Null(book.DroppedAt(GroveEntrance));
        Assert.Null(new PlaceBook(GroveRegions, GroveBook(Expansion.ML)).DroppedAt(GroveHall));
    }

    [Fact]
    public void OutsideEntrance_IsZeroForAMouthInsideOrNoEntrance()
    {
        var byName = Regions.ToDictionary(region => region.Name);

        Assert.Equal(KeepDoor, byName[KeepName].OutsideEntrance);
        Assert.Equal(Point3D.Zero, byName[WalkinName].OutsideEntrance);
        Assert.Equal(Point3D.Zero, byName[TownName].OutsideEntrance);
    }

    [Fact]
    public void PlacesToLeave_NamesTheWorldsDungeon_ElseTheDroppedPlace_AndTheDoorOut()
    {
        var book = new PlaceBook(GroveRegions, GroveBook(SecondAge));
        List<DungeonDoor> doors = [new(KeepName, KeepPad)];
        string DungeonAt(Point3D at) => at == KeepHall ? KeepName : null;
        var leave = DungeonGround.PlacesToLeave(DungeonAt, book);

        Assert.Equal(KeepName, leave(KeepHall));
        Assert.Equal(EraRules.BlightedGrove, leave(GroveHall));
        Assert.Null(leave(GroveEntrance));
        Assert.Equal(KeepPad, DungeonGround.DoorOutOf(DungeonAt, doors, book, KeepHall));
        Assert.Equal(GroveEntrance, DungeonGround.DoorOutOf(DungeonAt, doors, book, GroveHall));
        Assert.Equal(Point3D.Zero, DungeonGround.DoorOutOf(DungeonAt, doors, book, GroveEntrance));
    }
}
