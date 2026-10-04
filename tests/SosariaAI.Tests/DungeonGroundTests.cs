using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// A fighter rolls its dungeon over every door of the era it can get to, weighed by how its
/// best hall fits its power and tier, by the crowd there and by the walk, and rolls again
/// after every run. Only the door has to be in reach; the halls lie far off on the map.
/// </summary>
public class DungeonGroundTests
{
    private const int Leash = 400;
    private const int FighterPower = 90;
    private const int VeteranPower = 250;
    private const int NovicePower = 60;
    private const int Seed = 3;
    private const int SeedsToTry = 400;
    private const int ShortHunt = 25;
    private const int NearTrip = 100;
    private const int FarTrip = 2400;
    private const int Crowd = 30;
    private const string AuthoredDungeon = "despise";
    private const string HuntKind = "Hunt";
    private const string Shame = "Shame";
    private const string Covetous = "Covetous";
    private const string Despise = "Despise";
    private const string Sewer = "Britain Sewer";
    private const string EarthElemental = "EarthElemental";
    private const string Harpy = "Harpy";
    private const string SewerRat = "SewerRat";
    private const string Guildmaster = "RangerGuildmaster";
    private const int EasyDifficulty = 30;
    private const int FairDifficulty = 80;
    private const int HardDifficulty = 280;
    private const int SewerDifficulty = 20;
    private static readonly Point3D YewHome = new(630, 820, 0);
    private static readonly Point3D ShameDoor = new(514, 1561, 0);
    private static readonly Point3D CovetousDoor = new(2499, 919, 0);
    private static readonly Point3D ShameHall = new(5395, 126, 0);
    private static readonly Point3D ShameDeepHall = new(5515, 11, 5);
    private static readonly Point3D CovetousHall = new(5456, 1863, 0);
    private static readonly Point3D SewerHall = new(6048, 1487, 5);
    private static readonly Point3D Outdoors = new(700, 900, 0);
    private static readonly Point3D MaginciaHome = new(3730, 2150, 20);
    private static readonly Point3D MaginciaGate = new(3563, 2139, 0);
    private static readonly Point3D YewGate = new(771, 752, 5);
    private static readonly Point3D DespiseDoor = new(1298, 1080, 0);
    private static readonly Point3D DespiseHall = new(5501, 570, 0);
    private static readonly Point3D[] Moongates = [MaginciaGate, YewGate];

    [Fact]
    public void Pick_ChoosesAHallOfADungeonThatCanBeReached()
    {
        var catalog = Catalog(Hall(Harpy, CovetousHall, EasyDifficulty), Hall(EarthElemental, ShameHall, FairDifficulty));

        var spot = Pick(catalog, [Walk(Shame)], FighterPower, Seed);

        Assert.Equal(ShameHall, spot.Arrival);
    }

    [Fact]
    public void Pick_SkipsAHallBeyondTheReach()
    {
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, HardDifficulty), Hall(Harpy, ShameHall, EasyDifficulty));

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            Assert.Equal(ShameHall, Pick(catalog, [Walk(Shame)], FighterPower, seed).Arrival);
        }
    }

    [Fact]
    public void Pick_AHallAboveThePowerButInReach_IsTakenOn()
    {
        // A fighter opens on a foe up to 1.15 times its power, so a floor that hard is its.
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, HuntGround.Reach(FighterPower)));

        Assert.True(HuntGround.Reach(FighterPower) > FighterPower);
        Assert.Equal(ShameDeepHall, Pick(catalog, [Walk(Shame)], FighterPower, Seed).Arrival);
    }

    [Fact]
    public void Pick_PassesOverTheHallsOfAFloorTheFighterLostOn()
    {
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, FairDifficulty), Hall(Harpy, CovetousHall, FairDifficulty));

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var spot = DungeonGround.Pick(
                catalog, [Walk(Shame), Walk(Covetous)], FighterPower, SkillTier.Journeyman, prey: null, seed, IsMonster, DungeonAt,
                barred: hall => hall == ShameDeepHall
            );

            Assert.Equal(CovetousHall, spot.Arrival);
        }
    }

    [Fact]
    public void TooHard_AFloorLostOnStaysTooHardForTheRest_ThenTakesAgain()
    {
        TestMap.EnsureInternal();
        var fighter = new SosariaCharacter((Serial)LostFighter);
        var lost = new DungeonFloor(Shame, LostLevel, 0, FairDifficulty);
        var other = lost with { Level = LostLevel + 1 };

        Assert.True(DungeonGround.Takes(fighter, lost, FighterPower, LostAt));

        DungeonGround.MarkTooHard(fighter, lost, LostAt);

        Assert.True(DungeonGround.TooHard(fighter, lost, LostAt + DungeonCrawlRules.TooHardRest - System.TimeSpan.FromSeconds(1)));
        Assert.False(DungeonGround.Takes(fighter, lost, FighterPower, LostAt));
        Assert.True(DungeonGround.Takes(fighter, other, FighterPower, LostAt));
        Assert.False(DungeonGround.TooHard(fighter, lost, LostAt + DungeonCrawlRules.TooHardRest));
        Assert.False(DungeonGround.Takes(fighter, lost with { Difficulty = HuntGround.Reach(FighterPower) + 1 }, FighterPower, LostAt + DungeonCrawlRules.TooHardRest));
    }

    private const uint LostFighter = 0x7D0301;
    private const int LostLevel = 2;
    private static readonly System.DateTime LostAt = new(2026, 9, 29, 8, 44, 47, System.DateTimeKind.Utc);

    [Fact]
    public void Pick_AVeteranGoesDeep_ANoviceStaysShallow()
    {
        var catalog = Catalog(Hall(SewerRat, ShameHall, SewerDifficulty), Hall(EarthElemental, ShameDeepHall, HardDifficulty));
        var veteranDeep = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            veteranDeep += Pick(catalog, [Walk(Shame)], VeteranPower, seed, SkillTier.Grandmaster).Arrival == ShameDeepHall ? 1 : 0;
            Assert.Equal(ShameHall, Pick(catalog, [Walk(Shame)], NovicePower, seed, SkillTier.Novice).Arrival);
        }

        Assert.True(veteranDeep > SeedsToTry * 9 / 10);
    }

    [Fact]
    public void Pick_AVeteranLeavesTheNoviceDungeonAlone()
    {
        var catalog = Catalog(Hall(SewerRat, SewerHall, SewerDifficulty), Hall(EarthElemental, ShameDeepHall, HardDifficulty));
        var sewer = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            sewer += DungeonAt(Pick(catalog, [Walk(Sewer), Walk(Shame)], VeteranPower, seed, SkillTier.Grandmaster).Arrival) == Sewer ? 1 : 0;
        }

        Assert.True(sewer < SeedsToTry / 20);
    }

    [Fact]
    public void Pick_ANovicePrefersTheSewerToAHallAtTheTopOfItsReach()
    {
        var catalog = Catalog(Hall(SewerRat, SewerHall, SewerDifficulty), Hall(Harpy, CovetousHall, HuntGround.Reach(NovicePower)));
        var sewer = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            sewer += DungeonAt(Pick(catalog, [Walk(Sewer), Walk(Covetous)], NovicePower, seed, SkillTier.Novice).Arrival) == Sewer ? 1 : 0;
        }

        Assert.True(sewer > SeedsToTry / 2);
    }

    [Fact]
    public void Pick_SpreadsOverEveryDungeonInReach()
    {
        // One dungeon per person sent 200 floor entries to Sanctuary and none to nine others.
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, FairDifficulty), Hall(Harpy, DespiseHall, FairDifficulty));
        var seen = new HashSet<string>();

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            seen.Add(DungeonAt(Pick(catalog, [Walk(Shame), Walk(Despise)], FighterPower, seed).Arrival));
        }

        Assert.Equal([Despise, Shame], seen.OrderBy(name => name));
    }

    [Fact]
    public void Pick_ACrowdedDungeonWeighsLess()
    {
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, FairDifficulty), Hall(Harpy, DespiseHall, FairDifficulty));
        var crowded = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            crowded += DungeonAt(Pick(catalog, [Walk(Shame, visitors: Crowd), Walk(Despise)], FighterPower, seed).Arrival) == Shame ? 1 : 0;
        }

        Assert.True(crowded < SeedsToTry / 4);
    }

    [Fact]
    public void Pick_ARuneTakesTheLongWalkOut()
    {
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, FairDifficulty), Hall(Harpy, DespiseHall, FairDifficulty));
        var walked = 0;
        var byRune = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            walked += DungeonAt(Pick(catalog, [Walk(Shame, FarTrip), Walk(Despise)], FighterPower, seed).Arrival) == Shame ? 1 : 0;
            byRune += DungeonAt(Pick(catalog, [Rune(Shame, FarTrip), Walk(Despise)], FighterPower, seed).Arrival) == Shame ? 1 : 0;
        }

        Assert.True(byRune > walked);
    }

    [Fact]
    public void Pick_TheAmbitionPreyFitsBest()
    {
        var catalog = Catalog(Hall(SewerRat, ShameHall, SewerDifficulty), Hall(EarthElemental, ShameDeepHall, HardDifficulty));
        var prey = SewerRat.ToLowerInvariant();
        var atThePrey = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            var spot = DungeonGround.Pick(catalog, [Walk(Shame)], VeteranPower, SkillTier.Grandmaster, prey, seed, IsMonster, DungeonAt);
            atThePrey += spot.Arrival == ShameHall ? 1 : 0;
        }

        Assert.True(atThePrey > SeedsToTry / 2);
        Assert.Equal(HuntGround.PreyFit, DungeonGround.HallFit(SewerDifficulty, VeteranPower, SkillTier.Grandmaster, isPrey: true));
        Assert.True(DungeonGround.HallFit(SewerDifficulty, VeteranPower, SkillTier.Grandmaster, isPrey: false) < HuntGround.PreyFit / 10);
    }

    [Fact]
    public void Pick_SkipsASpawnThatIsNoEnemy() =>
        Assert.Null(Pick(Catalog(Hall(Guildmaster, ShameHall, EasyDifficulty)), [Walk(Shame)], FighterPower, Seed));

    [Fact]
    public void Pick_SkipsAHuntOutsideAnyDungeon() =>
        Assert.Null(Pick(Catalog(Hall(Harpy, Outdoors, EasyDifficulty)), [Walk(Shame)], FighterPower, Seed));

    [Fact]
    public void Pick_NoDungeonInReach_IsNull()
    {
        var catalog = Catalog(Hall(Harpy, CovetousHall, EasyDifficulty));

        Assert.Null(Pick(catalog, [Walk(Shame)], FighterPower, Seed));
        Assert.Null(Pick(catalog, [], FighterPower, Seed));
    }

    [Fact]
    public void ReachOf_WalkedOrByRune_WithTheTripAndTheCrowd()
    {
        DungeonDoor[] doors = [new(Despise, DespiseDoor), new(Covetous, CovetousDoor), new(Shame, ShameDoor)];

        var reach = DungeonGround.ReachOf(
            doors,
            MaginciaHome,
            walks: door => door != CovetousDoor,
            byRune: door => door == ShameDoor,
            Moongates,
            visitors: dungeon => dungeon == Despise ? Crowd : 0
        );

        Assert.Equal([Despise, Shame], reach.Select(r => r.Dungeon));
        Assert.Equal(NavMetric.ByMoongate(MaginciaHome, DespiseDoor, Moongates), reach[0].TripTiles);
        Assert.Equal(Crowd, reach[0].Visitors);
        Assert.False(reach[0].ByRune);
        Assert.True(reach[1].ByRune);
    }

    [Fact]
    public void ReachOf_AnIslandHome_WalksThroughTheMoongates()
    {
        // Every Magincia fighter "gave up enter a dungeon after 3 failures" under the old leash.
        var reach = DungeonGround.ReachOf([new(Despise, DespiseDoor)], MaginciaHome, _ => true, _ => false, Moongates, _ => 0);

        Assert.True(reach[0].TripTiles < NavMetric.Chebyshev(MaginciaHome, DespiseDoor));
    }

    [Fact]
    public void TravelWeight_ARuneCostsNothing_AWalkHalvesAtTheScale()
    {
        Assert.Equal(1.0, DungeonGround.TravelWeight(FarTrip, byRune: true));
        Assert.Equal(0.5, DungeonGround.TravelWeight(DungeonGround.TravelHalfTiles, byRune: false), precision: 6);
        Assert.True(DungeonGround.TravelWeight(NearTrip, byRune: false) > DungeonGround.TravelWeight(FarTrip, byRune: false));
    }

    [Fact]
    public void Pick_OnFoot_AWalkAcrossTheMapIsRarelyChosen()
    {
        // A fighter on foot should mostly go to the door near it, not across the map.
        var catalog = Catalog(Hall(EarthElemental, ShameDeepHall, FairDifficulty), Hall(Harpy, DespiseHall, FairDifficulty));
        var far = 0;

        for (var seed = 0; seed < SeedsToTry; seed++)
        {
            far += DungeonAt(Pick(catalog, [Walk(Shame, FarTrip), Walk(Despise)], FighterPower, seed).Arrival) == Shame ? 1 : 0;
        }

        Assert.True(far < SeedsToTry / 5);
    }

    [Fact]
    public void IsNearer_ByTheWalkWithTheMoongates()
    {
        Assert.True(DungeonGround.IsNearer(MaginciaHome, DespiseDoor, CovetousDoor, Moongates));
        Assert.False(DungeonGround.IsNearer(MaginciaHome, CovetousDoor, DespiseDoor, Moongates));
        Assert.False(DungeonGround.IsNearer(MaginciaHome, Point3D.Zero, DespiseDoor, Moongates));
        Assert.True(DungeonGround.IsNearer(MaginciaHome, DespiseDoor, Point3D.Zero, Moongates));
    }

    [Fact]
    public void KeepClear_ABlueKeepsOffTheDenGate_ARedDoesNot()
    {
        var denGate = new Point3D(2711, 2234, 0);
        Point3D[] gates = [MaginciaGate, denGate, YewGate];

        Assert.Equal([denGate], DungeonGround.KeepClear(gates, murderer: false));
        Assert.Empty(DungeonGround.KeepClear(gates, murderer: true));
        Assert.Empty(DungeonGround.KeepClear(null, murderer: false));
    }

    [Fact]
    public void TripSeed_EveryRunRollsAgain()
    {
        Assert.NotEqual(DungeonGround.TripSeed(Seed, 0), DungeonGround.TripSeed(Seed, 1));
        Assert.NotEqual(DungeonGround.TripSeed(Seed, 1), DungeonGround.TripSeed(Seed + 1, 1));
        Assert.Equal(DungeonGround.TripSeed(Seed, 2), DungeonGround.TripSeed(Seed, 2));
    }

    [Fact]
    public void WithPowerBar_SamePlace_TheDifficultyTurnedIntoThePowerItAsks()
    {
        var hall = Hall(EarthElemental, ShameDeepHall, HardDifficulty);

        var barred = DungeonGround.WithPowerBar(hall);

        Assert.Equal(hall.Arrival, barred.Arrival);
        Assert.Equal(hall.Role, barred.Role);
        Assert.Equal(HuntGround.PowerBar(HardDifficulty), barred.Difficulty);
        Assert.True(HuntGround.InReach(HardDifficulty, barred.Difficulty.GetValueOrDefault()));
        Assert.False(HuntGround.InReach(HardDifficulty, barred.Difficulty.GetValueOrDefault() - 1));
        Assert.Null(DungeonGround.WithPowerBar(null));
    }

    [Fact]
    public void DoorNamed_FindsTheDoorOrZero()
    {
        DungeonDoor[] doors = [new(Shame, ShameDoor), new(Sewer, SewerHall)];

        Assert.Equal(ShameDoor, DungeonGround.DoorNamed(doors, "shame"));
        Assert.Equal(Point3D.Zero, DungeonGround.DoorNamed(doors, Covetous));
        Assert.Equal(Point3D.Zero, DungeonGround.DoorNamed(doors, null));
        Assert.Equal(Point3D.Zero, DungeonGround.DoorNamed(null, Shame));
        Assert.False(DungeonGround.KnowsDoors(null));
    }

    [Fact]
    public void NearestFirst_ByTheWalkWithTheMoongates()
    {
        DungeonDoor[] doors = [new(Covetous, CovetousDoor), new(Despise, DespiseDoor), new(Shame, ShameDoor)];

        var sorted = DungeonGround.NearestFirst(doors, YewHome, Moongates);

        Assert.Equal([Despise, Shame, Covetous], sorted.Select(door => door.Dungeon));
        Assert.Empty(DungeonGround.NearestFirst(null, YewHome, Moongates));
    }

    [Fact]
    public void StandBy_LandsOffThePad_OnWalkedGround()
    {
        var onThePad = ShameDoor;
        var beside = new Point3D(ShameDoor.X + 1, ShameDoor.Y, 0);
        var clear = new Point3D(ShameDoor.X + DungeonGround.PadClearTiles + 1, ShameDoor.Y, 0);
        var tooFar = new Point3D(ShameDoor.X + DungeonGround.DoorNodeTiles + 1, ShameDoor.Y, 0);

        Assert.Equal(clear, DungeonGround.StandBy(ShameDoor, [onThePad, beside, tooFar, clear]));
        Assert.Equal(ShameDoor, DungeonGround.StandBy(ShameDoor, [onThePad, beside, tooFar]));
        Assert.Equal(ShameDoor, DungeonGround.StandBy(ShameDoor, (IReadOnlyList<Point3D>)null));
    }

    [Fact]
    public void ActionCatalog_AFighterWithNoAuthoredDungeon_DelvesTheCatalogHall()
    {
        var hunter = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster
            .First(c => PersonJobs.Of(c.Build) == PersonJobs.Fighter &&
                        c.ResolvedRoutines().Values.All(steps => steps.All(s => s?.Skill != SkillKinds.Dungeon)));
        var spot = DungeonGround.WithPowerBar(Hall(EarthElemental, ShameHall, FairDifficulty));

        var actions = ActionCatalog.From(hunter, catalog: null, new HuntHome(YewHome, Leash, Ground: null, spot));

        var delve = Assert.Single(actions, a => a.SkillKind == SkillKinds.Dungeon);
        Assert.Equal(ActionCatalog.CatalogDelveId, delve.Id.Value);
        Assert.Equal(ShameHall, delve.Step.Target);
        Assert.Equal(HuntGround.PowerBar(FairDifficulty), delve.RequiredPower);
    }

    [Fact]
    public void ActionCatalog_NoHallFitsTheHome_DropsTheAuthoredDungeon()
    {
        // The authored Despise walk refuses a far home at its first step, and did so three
        // times a trip. With the world's word that no hall fits, the job rolls again.
        var delver = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster
            .First(c => c.ResolvedRoutines().Values.Any(steps => steps.Any(s => s?.Skill == SkillKinds.Dungeon)));
        var noHall = new HuntHome(MaginciaHome, Leash, Ground: null, KnowsDoors: true);
        var noCatalog = new HuntHome(MaginciaHome, Leash, Ground: null);

        Assert.DoesNotContain(ActionCatalog.From(delver, catalog: null, noHall), a => a.SkillKind == SkillKinds.Dungeon);
        Assert.Contains(ActionCatalog.From(delver, catalog: null, noCatalog), a => a.SkillKind == SkillKinds.Dungeon);
    }

    [Fact]
    public void AtPlace_KeepsTheCrewAndTimeAndDropsTheAuthoredDungeon()
    {
        var authored = new SkillStepDefinition
        {
            Skill = SkillKinds.Dungeon,
            Dungeon = AuthoredDungeon,
            Minutes = ShortHunt,
            Party = "delvers"
        };

        var moved = HuntGround.AtPlace(authored, Hall(EarthElemental, ShameHall, FairDifficulty), keepsCrew: true);

        Assert.Null(moved.Dungeon);
        Assert.Equal(ShameHall, moved.Target);
        Assert.True(moved.Area.ToRectangle().Contains(ShameHall));
        Assert.Equal(ShortHunt, moved.Minutes);
        Assert.Equal(authored.Party, moved.Party);
        Assert.Equal(AuthoredDungeon, authored.Dungeon);
    }

    [Fact]
    public void ActionCatalog_WithADungeonSpot_DelvesThereAtItsPowerBar()
    {
        var delver = CharactersFile.CreateDefault().Facets[FacetNames.Felucca].Roster
            .First(c => c.ResolvedRoutines().Values.Any(steps => steps.Any(s => s?.Skill == SkillKinds.Dungeon)));
        var spot = DungeonGround.WithPowerBar(Hall(EarthElemental, ShameHall, FairDifficulty));

        var actions = ActionCatalog.From(delver, catalog: null, new HuntHome(YewHome, Leash, Ground: null, spot));

        var dungeons = actions.FindAll(a => a.SkillKind == SkillKinds.Dungeon);
        Assert.NotEmpty(dungeons);
        Assert.All(dungeons, d =>
        {
            Assert.Equal(ShameHall, d.Step.Target);
            Assert.Equal(HuntGround.PowerBar(FairDifficulty), d.RequiredPower);
        });
    }

    private static Destination Pick(
        DestinationCatalog catalog,
        DungeonReach[] reach,
        int power,
        int seed,
        SkillTier tier = SkillTier.Journeyman
    ) =>
        DungeonGround.Pick(catalog, reach, power, tier, prey: null, seed, IsMonster, DungeonAt);

    private static DungeonReach Walk(string dungeon, int trip = NearTrip, int visitors = 0) => new(dungeon, trip, false, visitors);

    private static DungeonReach Rune(string dungeon, int trip) => new(dungeon, trip, true, 0);

    private static bool IsMonster(string role) => role is EarthElemental or Harpy or SewerRat;

    private static string DungeonAt(Point3D point) =>
        point == ShameHall || point == ShameDeepHall ? Shame :
        point == CovetousHall ? Covetous :
        point == DespiseHall ? Despise :
        point == SewerHall ? Sewer :
        null;

    private static DestinationCatalog Catalog(params Destination[] places) => new(places);

    private static Destination Hall(string role, Point3D at, int difficulty) =>
        new()
        {
            Name = $"{role} {at.X}-{at.Y}",
            Kind = HuntKind,
            Role = role,
            X = at.X,
            Y = at.Y,
            Z = at.Z,
            Difficulty = difficulty
        };
}
