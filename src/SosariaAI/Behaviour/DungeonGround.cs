using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server;
using Server.Regions;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>A dungeon of the running era's world: the name people give it and the door a player goes in by.</summary>
public readonly record struct DungeonDoor(string Dungeon, Point3D Door);

/// <summary>
/// A dungeon a fighter can get to on this trip: the tiles of the trip to its door (walked,
/// the public moongates counted), whether a rune the fighter can use lands at the door, and
/// how many people are inside or on their way there now.
/// </summary>
public readonly record struct DungeonReach(string Dungeon, int TripTiles, bool ByRune, int Visitors);

/// <summary>
/// Where a fighter delves: a
/// dungeon is no special job behind the home leash, and every door of the era's world that the
/// fighter can get to, on foot through gates and teleporters or by a rune, is a destination in
/// the roll. Each weighs its best hall's fit to the fighter's power and tier
/// (<see cref="HuntGround.Fit"/>), less for the crowd already there, and less for a long walk
/// where no rune carries the fighter. The roll comes again after every run, so a fighter goes
/// to different dungeons in turn. The halls lie in the far dungeon block of the map, so the
/// door stands for the dungeon; the navigation graph links each door to its halls by the real
/// teleporter, so the walk in is a walk a player could make. Doors come from the world's own
/// dungeon regions (<see cref="WorldPlaces"/>), one per dungeon at the entrance the world's
/// data records, so Khaldun, the Britain Sewer and the Trinsic Passage have theirs too. A hall
/// is as hard as its floor (<see cref="GroundRating"/>), and the power it is judged against is
/// the fighter's own with its pets and party (<see cref="FightingPower"/>). A floor the fighter
/// lost on lately is no pick for it (<see cref="TooHard"/>).
/// </summary>
public static class DungeonGround
{
    /// <summary>A door's own graph node lies this close to it; further off, reach is not judged by the graph.</summary>
    public const int DoorNodeTiles = 24;

    /// <summary>
    /// A rune for a door lands at least this far from the pad:
    /// a landing on or beside the pad skips the walk onto it.
    /// </summary>
    public const int PadClearTiles = 2;

    /// <summary>Graph nodes looked at round a door for a rune's landing.</summary>
    public const int StandLooks = 8;

    /// <summary>
    /// A walk this long to the door halves a dungeon's weight; a rune carries a traveller at
    /// no cost. At 800 tiles a Skara Brae fencer on foot picked a dungeon across the map as
    /// readily as the one by the Yew gate, and was still walking twelve minutes on.
    /// </summary>
    public const int TravelHalfTiles = 300;

    public const int DungeonSalt = 0xD0;
    public const int HallSalt = 0xA1;

    private const uint RollPrime = 0x9E3779B1;

    private static readonly ConditionalWeakTable<PlaceBook, List<DungeonDoor>> DoorsByBook = new();

    /// <summary>
    /// The hall a fighter goes to, or null when no dungeon it can get to has a hall in its
    /// reach. The dungeon is rolled over <paramref name="reach"/> by weight; the hall inside
    /// is rolled among the best fitting few by fit, so a veteran goes to the deep halls and a
    /// novice to the first. <paramref name="prey"/>, the ambition's creature, fits best
    /// wherever it lives. Pure.
    /// </summary>
    public static Destination Pick(
        DestinationCatalog catalog,
        IReadOnlyList<DungeonReach> reach,
        int power,
        SkillTier tier,
        string prey,
        int seed,
        Func<string, bool> isEnemy,
        Func<Point3D, string> dungeonAt,
        Func<Point3D, bool> barred = null
    )
    {
        if (catalog == null || reach == null || reach.Count == 0 || isEnemy == null || dungeonAt == null)
        {
            return null;
        }

        var byName = new Dictionary<string, DungeonReach>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reach.Count; i++)
        {
            byName.TryAdd(reach[i].Dungeon, reach[i]);
        }

        var hallsByDungeon = FittingHalls(catalog, power, tier, prey, isEnemy, dungeonAt, byName.ContainsKey, barred);

        if (hallsByDungeon.Count == 0)
        {
            return null;
        }

        var dungeons = new List<List<(Destination Hall, double Fit)>>(hallsByDungeon.Count);
        var weights = new List<double>(hallsByDungeon.Count);

        foreach (var (dungeon, halls) in hallsByDungeon)
        {
            halls.Sort(static (left, right) => left.Fit != right.Fit
                ? right.Fit.CompareTo(left.Fit)
                : right.Hall.Difficulty.GetValueOrDefault().CompareTo(left.Hall.Difficulty.GetValueOrDefault()));
            dungeons.Add(halls);
            weights.Add(DungeonWeight(halls[0].Fit, byName[dungeon]));
        }

        var chosen = dungeons[Math.Max(0, WeightedChoice.Index(weights, ChoiceSeed.Unit(seed, DungeonSalt)))];
        var fits = new double[Math.Min(HuntGround.NearChoices, chosen.Count)];

        for (var i = 0; i < fits.Length; i++)
        {
            fits[i] = chosen[i].Fit;
        }

        return chosen[Math.Max(0, WeightedChoice.Index(fits, ChoiceSeed.Unit(seed, HallSalt)))].Hall;
    }

    /// <summary>
    /// The halls of each dungeon that fit a fighter of <paramref name="power"/>, with their
    /// fit, for the dungeons <paramref name="counts"/> lets in, leaving out the halls
    /// <paramref name="barred"/> names (null for none). Vermin inside a dungeon are its spawn:
    /// the Britain Sewer holds nothing else. Pure.
    /// </summary>
    public static SortedDictionary<string, List<(Destination Hall, double Fit)>> FittingHalls(
        DestinationCatalog catalog,
        int power,
        SkillTier tier,
        string prey,
        Func<string, bool> isEnemy,
        Func<Point3D, string> dungeonAt,
        Func<string, bool> counts,
        Func<Point3D, bool> barred = null
    )
    {
        var hallsByDungeon = new SortedDictionary<string, List<(Destination Hall, double Fit)>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < (catalog?.All.Count ?? 0) && isEnemy != null && dungeonAt != null; i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind != DestinationKind.Hunt ||
                place.Difficulty is not { } difficulty ||
                !isEnemy(place.Role) ||
                dungeonAt(place.Arrival) is not { } dungeon ||
                counts?.Invoke(dungeon) == false ||
                barred?.Invoke(place.Arrival) == true)
            {
                continue;
            }

            var fit = HallFit(difficulty, power, tier, string.Equals(place.Role, prey, StringComparison.OrdinalIgnoreCase));

            if (fit <= 0)
            {
                continue;
            }

            if (!hallsByDungeon.TryGetValue(dungeon, out var halls))
            {
                halls = [];
                hallsByDungeon[dungeon] = halls;
            }

            halls.Add((place, fit));
        }

        return hallsByDungeon;
    }

    /// <summary>
    /// The dungeons a character's runs go to: those with a hall that fits its power now.
    /// Main thread: it reads regions.
    /// </summary>
    public static IReadOnlySet<string> UsualFor(SosariaCharacter character)
    {
        var map = Map.Parse(character.HomeFacet);
        var halls = FittingHalls(
            NavWorld.DestinationsFor(character.HomeFacet),
            FightingPower(character),
            character.PersonProfile.Tier,
            character.CurrentAmbition().Prey,
            Navigation.Generation.CreatureStatsLookup.IsLandEnemy,
            RegionNames(map),
            counts: null,
            FloorBarred(character)
        );
        return new HashSet<string>(halls.Keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>What a hall weighs for a fighter: its fit, or the best fit when it holds the ambition's prey in reach.</summary>
    public static double HallFit(int difficulty, int power, SkillTier tier, bool isPrey) =>
        isPrey && HuntGround.InReach(difficulty, power) ? HuntGround.PreyFit : HuntGround.Fit(difficulty, power, tier);

    /// <summary>
    /// A dungeon's weight in the roll: its best hall's fit, less for the crowd there
    /// (<see cref="DungeonShareRules.SpreadWeight"/>), and less for a long walk to the door
    /// when no rune lands there.
    /// </summary>
    public static double DungeonWeight(double bestFit, DungeonReach reach) =>
        bestFit * DungeonShareRules.SpreadWeight(reach.Visitors) * TravelWeight(reach.TripTiles, reach.ByRune);

    /// <summary>One for a rune or a door at hand, a half at <see cref="TravelHalfTiles"/>, a third at twice that.</summary>
    public static double TravelWeight(int tripTiles, bool byRune) =>
        byRune ? 1.0 : 1.0 / (1.0 + (double)Math.Max(0, tripTiles) / TravelHalfTiles);

    /// <summary>
    /// The dungeons a fighter living at <paramref name="home"/> can get to: each door it walks
    /// to or a rune of its lands at, with the trip and the crowd there. Pure.
    /// </summary>
    public static List<DungeonReach> ReachOf(
        IReadOnlyList<DungeonDoor> doors,
        Point3D home,
        Func<Point3D, bool> walks,
        Func<Point3D, bool> byRune,
        IReadOnlyList<Point3D> moongates,
        Func<string, int> visitors
    )
    {
        var reach = new List<DungeonReach>();

        for (var i = 0; i < (doors?.Count ?? 0); i++)
        {
            var door = doors[i];
            var rune = byRune?.Invoke(door.Door) == true;

            if (rune || walks?.Invoke(door.Door) != false)
            {
                reach.Add(new DungeonReach(
                    door.Dungeon,
                    NavMetric.ByMoongate(home, door.Door, moongates),
                    rune,
                    visitors?.Invoke(door.Dungeon) ?? 0
                ));
            }
        }

        return reach;
    }

    /// <summary>
    /// The seed of one dungeon roll: the person's own seed, turned on by every run it made,
    /// so each run rolls again and two people never share a sequence.
    /// </summary>
    public static int TripSeed(int personSeed, int runs) =>
        unchecked((int)ChoiceSeed.Mix((uint)personSeed ^ (uint)runs * RollPrime));

    /// <summary>
    /// World side of <see cref="Pick"/> for one character at <paramref name="power"/>: the era's
    /// doors of its home facet it can get to, the crowd at each, and the catalog halls. The
    /// hall comes back with its power bar as difficulty (<see cref="WithPowerBar"/>). A pick
    /// made <paramref name="onFoot"/> weighs no rune: the recall already would not take.
    /// Main thread: it reads regions, the character's runes and the graph.
    /// </summary>
    public static Destination PickFor(SosariaCharacter character, int power, int seed, string prey, bool onFoot = false)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.HomeFacet))
        {
            return null;
        }

        var facet = character.HomeFacet;
        var map = Map.Parse(facet);
        var hall = Pick(
            NavWorld.DestinationsFor(facet),
            ReachFor(character, onFoot),
            power,
            character.PersonProfile.Tier,
            prey,
            seed,
            Navigation.Generation.CreatureStatsLookup.IsLandEnemy,
            RegionNames(map),
            FloorBarred(character)
        );
        return WithPowerBar(hall);
    }

    /// <summary>
    /// The doors of the era's dungeons on the character's home facet that it can get to now
    /// (<see cref="ReachOf"/>), less the dungeons it rests from. Main thread: it reads regions,
    /// the character's runes and the graph.
    /// </summary>
    public static List<DungeonReach> ReachFor(SosariaCharacter character, bool onFoot = false)
    {
        if (character == null || string.IsNullOrWhiteSpace(character.HomeFacet))
        {
            return [];
        }

        var facet = character.HomeFacet;
        var map = Map.Parse(facet);
        var home = character.HomeSpot;
        var magic = !onFoot && RuneKit.TravelsByMagic(character.Skills.Magery.Value, RuneKit.RecallStock(character));
        // A murderer keeps off the guards: a door under them is no door for it, and its walk
        // goes by a murderer's roads. Reds on the Den picked a Serpent's Hold door and refused
        // the walk into the guards at its first step.
        bool Barred(Point3D door) => RuneShelf.BarredFor(character, door, map);
        var walks = WalksFrom(character, map, NavWorld.GraphFor(facet), home);
        // A door its walk just found no way to is no walk for a while (UnreachableSpots).
        var unreachable = character.Memory.Unreachable.Active(Core.Now);
        var reach = ReachOf(
            DoorsOf(map),
            home,
            door => !Barred(door) && !NavSearch.IsNearAny(door, unreachable) && walks(door),
            door => magic && !Barred(door) && RuneShelf.MarkedNear(character, door, map, RecallRules.MaxLandingWalkTiles) != null,
            Navigation.Generation.MoongateSeeds.LocationsFor(facet),
            DungeonShare.Visitors
        );

        // A dungeon whose floor it failed to reach the same way three runs in a row rests.
        reach.RemoveAll(dungeon => JobTargetRest.Rests(character, SkillKinds.Dungeon, dungeon.Dungeon, Core.Now));
        return reach;
    }

    /// <summary>
    /// A hall for a lone traveller whose door recall would not take, rolled on foot: its
    /// door nearer by the walk than the door of <paramref name="hall"/>, or null when the roll
    /// finds none nearer. Main thread.
    /// </summary>
    public static Destination NearerOnFoot(SosariaCharacter character, Point3D hall, int seed)
    {
        var map = character?.Map;
        var moongates = Navigation.Generation.MoongateSeeds.LocationsFor(character?.HomeFacet);
        var from = character?.Location ?? Point3D.Zero;
        var picked = PickFor(character, FightingPower(character), seed, character?.CurrentAmbition().Prey, onFoot: true);

        return picked != null && IsNearer(
            from,
            DoorOf(map, RegionNames(map)(picked.Arrival)),
            DoorOf(map, RegionNames(map)(hall)),
            moongates
        )
            ? picked
            : null;
    }

    /// <summary>True when <paramref name="door"/> is known and a shorter walk from <paramref name="from"/> than <paramref name="than"/>, the moongates counted. Pure.</summary>
    public static bool IsNearer(Point3D from, Point3D door, Point3D than, IReadOnlyList<Point3D> moongates) =>
        door != Point3D.Zero &&
        (than == Point3D.Zero || NavMetric.ByMoongate(from, door, moongates) < NavMetric.ByMoongate(from, than, moongates));

    /// <summary>
    /// The places a fighter's dungeon walk keeps clear of: for a blue, the public moongates
    /// in the reds' own town. The road to Covetous ran through the Den gate and the Den
    /// teleporter, and ten blues on their way died on the Den's streets in half an hour. Pure.
    /// </summary>
    public static List<Point3D> KeepClear(IReadOnlyList<Point3D> moongates, bool murderer)
    {
        var clear = new List<Point3D>();

        for (var i = 0; i < (moongates?.Count ?? 0) && !murderer; i++)
        {
            if (PkRules.InBuccaneersDen(moongates[i].X, moongates[i].Y))
            {
                clear.Add(moongates[i]);
            }
        }

        return clear;
    }

    /// <summary>World side of <see cref="KeepClear(IReadOnlyList{Point3D}, bool)"/> for a character on its home facet.</summary>
    public static List<Point3D> KeepClear(SosariaCharacter character) =>
        KeepClear(Navigation.Generation.MoongateSeeds.LocationsFor(character?.HomeFacet), PkRules.IsRed(character?.Kills ?? 0));

    /// <summary>
    /// The power a fighter takes a place on with: its own once its wounds are mended
    /// (<see cref="CharacterPower.Healthy"/>), and its pets and the living members of its party
    /// on its map at the share a fight counts allies at (<see cref="DecideChoice.EffectivePower"/>).
    /// A tamer's dragon is half its strength; a party goes where none of it would go alone.
    /// Main thread.
    /// </summary>
    public static int FightingPower(SosariaCharacter character)
    {
        if (character == null)
        {
            return 0;
        }

        var allies = PetKeeper.PetsPower(character);
        var party = GameParty.Of(character);

        for (var i = 0; i < (party?.Members.Count ?? 0); i++)
        {
            var member = party.Members[i].Mobile;

            if (member != null && member != character && !member.Deleted && member.Alive &&
                member is not SosariaCharacter { IsGhost: true } && member.Map == character.Map)
            {
                allies += CharacterPower.Healthy(member);
            }
        }

        return DecideChoice.EffectivePower(CharacterPower.Healthy(character), allies, partyContent: true);
    }

    /// <summary>True while this person remembers the floor as one it lost on (<see cref="DungeonCrawlRules.TooHardRest"/>).</summary>
    public static bool TooHard(SosariaCharacter character, DungeonFloor floor, DateTime now) =>
        character != null &&
        DungeonCrawlRules.StillTooHard(character.ClockAt(RuleClock.FloorTooHard(floor.Dungeon, floor.Level)), now);

    /// <summary>This person lost on the floor: it stays too hard for it a while, across a restart.</summary>
    public static void MarkTooHard(SosariaCharacter character, DungeonFloor floor, DateTime now) =>
        character?.StartClock(RuleClock.FloorTooHard(floor.Dungeon, floor.Level), now);

    /// <summary>A floor this person takes: it fits the power and the person did not lose on it lately.</summary>
    public static bool Takes(SosariaCharacter character, DungeonFloor floor, int power, DateTime now) =>
        DungeonCrawlRules.FloorFits(floor.Difficulty, power) && !TooHard(character, floor, now);

    /// <summary>The halls on floors this person lost on lately, for the pick to pass over. Main thread.</summary>
    private static Func<Point3D, bool> FloorBarred(SosariaCharacter character)
    {
        var floors = DungeonAtlas.For(character.HomeFacet);
        var now = Core.Now;
        return hall => floors.FloorAt(hall) is { } floor && TooHard(character, floor, now);
    }

    /// <summary>
    /// The fit note of a log line for a fighter at a place (<see cref="HuntGround.FitNote"/>):
    /// its power against the floor under the place, or against the hunt ground there. Main thread.
    /// </summary>
    public static string FitNoteAt(SosariaCharacter character, Point3D at)
    {
        var facet = character?.Map?.Name;
        var word = DungeonAtlas.For(facet).FloorAt(at) is { Difficulty: > 0 } ? DungeonCrawlRules.FloorWord : HuntGround.GroundWord;
        return HuntGround.FitNote(FightingPower(character), DifficultyAt(facet, at), word);
    }

    /// <summary>
    /// How hard the place round a point is: the floor under it when that holds spawn, else the
    /// hunt spot nearest it (<see cref="HuntGround.DifficultyAt"/>); zero when neither is known.
    /// Main thread.
    /// </summary>
    public static int DifficultyAt(string facet, Point3D at) =>
        DungeonAtlas.For(facet).FloorAt(at) is { Difficulty: > 0 } floor
            ? floor.Difficulty
            : HuntGround.DifficultyAt(NavWorld.DestinationsFor(facet), at);

    /// <summary>
    /// The hall as a delve names it: the same place, its difficulty turned into the power it
    /// asks of the fighter (<see cref="HuntGround.PowerBar"/>), which the scorer holds a delve to.
    /// </summary>
    public static Destination WithPowerBar(Destination hall) =>
        hall == null
            ? null
            : new Destination
            {
                Name = hall.Name,
                Kind = hall.Kind,
                Role = hall.Role,
                X = hall.X,
                Y = hall.Y,
                Z = hall.Z,
                Node = hall.Node,
                Aliases = hall.Aliases,
                Difficulty = HuntGround.PowerBar(hall.Difficulty.GetValueOrDefault())
            };

    /// <summary>
    /// The door of the dungeon that holds <paramref name="hall"/> is a walk from where the
    /// character stands. A party walks to its door unless its leader gates it there.
    /// </summary>
    public static bool WalksToDoorOf(SosariaCharacter character, Point3D hall)
    {
        var map = character?.Map;

        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var door = DoorOf(map, RegionNames(map)(hall));
        return door != Point3D.Zero && WalksFrom(character, map, NavWorld.GraphFor(character.HomeFacet), character.Location)(door);
    }

    /// <summary>
    /// A door counts as walked to when its own graph node lies near it and the road search
    /// from home reaches that node, on the walker's own roads (see <see cref="RedGangReach.Walks(SosariaCharacter, Map, Point3D, Point3D)"/>).
    /// One piece of the graph was not enough: the doors of Deceit, Hythloth and Serpent's Hold
    /// share a piece with the mainland by one-way links, and 51 delves in half an hour found no
    /// road to them. Without a graph every door counts.
    /// </summary>
    private static Func<Point3D, bool> WalksFrom(SosariaCharacter walker, Map map, NavGraph graph, Point3D home)
    {
        if (graph == null)
        {
            return static _ => true;
        }

        return door => graph.FindNearest(door) is { } node &&
                       NavMetric.Chebyshev(node.Location, door) <= DoorNodeTiles &&
                       RedGangReach.Walks(walker, map, home, door);
    }

    /// <summary>A dungeon step goes to the character's catalog hall when it has one, with its crew.</summary>
    public static SkillStepDefinition ForHome(SkillStepDefinition step, HuntHome hunt)
    {
        if (hunt?.Dungeon == null || !IsDelve(step))
        {
            return step;
        }

        return HuntGround.AtPlace(step, hunt.Dungeon, keepsCrew: true);
    }

    public static bool IsDelve(SkillStepDefinition step) =>
        SkillKinds.Dungeon.Equals(step?.Skill, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The name of the dungeon around a point on the map, as this era's world has it
    /// (<see cref="WorldPlaces"/>): the region's name, or the dungeon's own name inside a
    /// grouping region such as "Misc Dungeons". Null outside a dungeon, and in a dungeon this
    /// world does not have, so nobody delves, crawls or calls a party there.
    /// </summary>
    public static Func<Point3D, string> RegionNames(Map map) => RegionNames(map, () => WorldPlaces.For(map));

    /// <summary>
    /// <see cref="RegionNames(Map)"/> with the world's places read from <paramref name="book"/>,
    /// asked for only when a point stands in a dungeon region.
    /// </summary>
    public static Func<Point3D, string> RegionNames(Map map, Func<PlaceBook> book) =>
        point =>
        {
            if (map == null || map == Map.Internal)
            {
                return null;
            }

            var region = Region.Find(point, map);

            while (region != null && region is not DungeonRegion)
            {
                region = region.Parent;
            }

            return region == null ? null : book().Spoken(region.Name, point);
        };

    /// <summary>
    /// The place a person at a spot leaves by a dungeon's ways out (<see cref="DungeonEscapeRules"/>):
    /// the dungeon this world has there, else a region dropped from this world
    /// (<see cref="PlaceBook.DroppedAt"/>). No plan goes to a dropped place, but a save can
    /// hold people there: Lorne the Poor stood in Blighted Grove on a Second Age shard, where
    /// no walk home started. <see cref="RegionNames"/> names no dungeon there, so without
    /// this the ways out would pass him by. Pure.
    /// </summary>
    public static Func<Point3D, string> PlacesToLeave(Func<Point3D, string> dungeonAt, PlaceBook book) =>
        point => dungeonAt?.Invoke(point) ?? book?.DroppedAt(point)?.Name;

    /// <summary>World side of <see cref="PlacesToLeave(Func{Point3D, string}, PlaceBook)"/>. Main thread: it reads regions.</summary>
    public static Func<Point3D, string> PlacesToLeave(Map map) => PlacesToLeave(RegionNames(map), WorldPlaces.For(map));

    /// <summary>
    /// Where a person at <paramref name="at"/> stands once out of the place round it: the door
    /// of the dungeon this world has there, else the recorded entrance outside the dropped
    /// region round it (<see cref="PlaceRegion.OutsideEntrance"/>), else zero. Pure.
    /// </summary>
    public static Point3D DoorOutOf(Func<Point3D, string> dungeonAt, IReadOnlyList<DungeonDoor> doors, PlaceBook book, Point3D at)
    {
        var door = DoorNamed(doors, dungeonAt?.Invoke(at));
        return door != Point3D.Zero ? door : book?.DroppedAt(at)?.OutsideEntrance ?? Point3D.Zero;
    }

    /// <summary>World side of <see cref="DoorOutOf(Func{Point3D, string}, IReadOnlyList{DungeonDoor}, PlaceBook, Point3D)"/>. Main thread.</summary>
    public static Point3D DoorOutOf(Map map, Point3D at) => DoorOutOf(RegionNames(map), DoorsOf(map), WorldPlaces.For(map), at);

    /// <summary>
    /// The doors of the era's dungeons on a map, one per dungeon: the pad a player goes in by
    /// at the entrance the world's data records, or the entrance itself when it lies inside the
    /// dungeon. Empty for no map. Main thread: it reads regions once per map.
    /// </summary>
    public static IReadOnlyList<DungeonDoor> DoorsOf(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return [];
        }

        return DoorsByBook.GetValue(WorldPlaces.For(map), DoorsIn);
    }

    /// <summary>The door of the dungeon named <paramref name="dungeon"/> on a map, or zero.</summary>
    public static Point3D DoorOf(Map map, string dungeon) => DoorNamed(DoorsOf(map), dungeon);

    /// <summary>The door of the named dungeon among <paramref name="doors"/>, or zero. Pure.</summary>
    public static Point3D DoorNamed(IReadOnlyList<DungeonDoor> doors, string dungeon)
    {
        for (var i = 0; i < (doors?.Count ?? 0) && !string.IsNullOrWhiteSpace(dungeon); i++)
        {
            if (string.Equals(doors[i].Dungeon, dungeon, StringComparison.OrdinalIgnoreCase))
            {
                return doors[i].Door;
            }
        }

        return Point3D.Zero;
    }

    /// <summary>True when the map's world has at least one dungeon door, so its verdict on reach holds.</summary>
    public static bool KnowsDoors(Map map) => DoorsOf(map).Count > 0;

    /// <summary>The doors, nearest first by the walk with the public moongates counted. Pure.</summary>
    public static List<DungeonDoor> NearestFirst(IReadOnlyList<DungeonDoor> doors, Point3D home, IReadOnlyList<Point3D> moongates)
    {
        var sorted = new List<(DungeonDoor Door, int Trip)>(doors?.Count ?? 0);

        for (var i = 0; i < (doors?.Count ?? 0); i++)
        {
            sorted.Add((doors[i], NavMetric.ByMoongate(home, doors[i].Door, moongates)));
        }

        sorted.Sort(static (a, b) => a.Trip != b.Trip ? a.Trip.CompareTo(b.Trip) : string.CompareOrdinal(a.Door.Dungeon, b.Door.Dungeon));
        var nearestFirst = new List<DungeonDoor>(sorted.Count);

        for (var i = 0; i < sorted.Count; i++)
        {
            nearestFirst.Add(sorted[i].Door);
        }

        return nearestFirst;
    }

    /// <summary>
    /// Where a rune for a door lands: the nearest of <paramref name="nodes"/> at least
    /// <see cref="PadClearTiles"/> from the pad and within <see cref="DoorNodeTiles"/> of it,
    /// ground the graph already walks; the door itself when none is. Pure.
    /// </summary>
    public static Point3D StandBy(Point3D door, IReadOnlyList<Point3D> nodes)
    {
        var best = door;
        var bestTiles = int.MaxValue;

        for (var i = 0; i < (nodes?.Count ?? 0); i++)
        {
            var tiles = NavMetric.Chebyshev(door, nodes[i]);

            if (tiles >= PadClearTiles && tiles <= DoorNodeTiles && tiles < bestTiles)
            {
                best = nodes[i];
                bestTiles = tiles;
            }
        }

        return best;
    }

    /// <summary>World side of <see cref="StandBy"/>: the graph nodes round the door on its facet.</summary>
    public static Point3D StandBy(NavGraph graph, Point3D door)
    {
        var nodes = graph?.FindNearest(door, StandLooks) ?? [];
        var points = new List<Point3D>(nodes.Count);

        for (var i = 0; i < nodes.Count; i++)
        {
            points.Add(nodes[i].Location);
        }

        return StandBy(door, points);
    }

    private static List<DungeonDoor> DoorsIn(PlaceBook book)
    {
        var doors = new List<DungeonDoor>();

        foreach (var place in book.Dungeons)
        {
            doors.Add(new DungeonDoor(place.Name, place.Door));
        }

        return doors;
    }
}
