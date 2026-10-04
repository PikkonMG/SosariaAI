using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Logging;
using Server.Regions;
using Server.Spells;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Skills;

/// <summary>
/// The runebook an established character walks in with on its first day: a book a scribe
/// made it in the life before the shard saw it, holding the places it marked then, its home
/// bank, its own corner of town, and for a fighter its far hunt and the door of every
/// dungeon of the era a rune can land at, with the recall scrolls it carried dropped in as
/// charges. A novice
/// has none and walks. A mage who marks keeps one blank rune for a new place. Given once,
/// like the rest of the kit; a blessed book is not lost to a death, and a rune marked on
/// the road goes into it. A character that was already in the world gets the same kit once
/// on its next bind, when it carries no marked rune yet; the saved flag
/// <see cref="SosariaCharacter.RuneKitPacked"/> keeps it to once. Loose marked runes and a
/// stack of scrolls from an older save go into a book on every bind, so the pack holds a
/// book, not a pile, and a fighter's book gets the doors it lacks on every bind, so its
/// dungeon runs start with a recall to the door.
/// A red's book drops the entries that land under the guards on every bind (a character the
/// plan turned red keeps the town runes of its blue days) and gets the PvP hot spots it lacks
/// first, so it recalls off Buccaneer's Den to the roads it hunts and back to the Den's bank; an entry
/// at the Den's bank it always keeps, for the recall home after a death. A blue's book gets the town banks it lacks
/// last, on every bind, while it has room: the bank book every travelling player kept, so a
/// trip to another town is a recall there.
/// An established tamer's book gets the taming grounds it would go to first, before the doors,
/// so its trip to the dragons of Destard starts with a recall (<see cref="TamingGrounds"/>).
/// </summary>
public static class RuneKit
{
    private static readonly ILogger logger = SosariaLog.For(typeof(RuneKit));

    /// <summary>The first tier that has lived long enough to carry marked runes.</summary>
    public const SkillTier EstablishedTier = SkillTier.Journeyman;

    /// <summary>A tamer's book holds this many of its best taming grounds.</summary>
    public const int TamingGroundRunes = 2;

    /// <summary>True when the person can travel by recall at all: from a book, or from scrolls with enough skill to read them.</summary>
    public static bool TravelsByMagic(double magery, int recallScrolls) =>
        magery >= RecallRules.MinMagery || recallScrolls > 0 && magery >= RecallRules.ScrollMinMagery;

    public static bool CarriesMarkedRunes(SkillTier tier, bool travelsByMagic) =>
        tier >= EstablishedTier && travelsByMagic;

    /// <summary>
    /// The places, in order, that get a marked rune: each one kept only when no earlier
    /// place already lies within <see cref="RuneShelf.NearPlaceTiles"/> of it, at most
    /// <see cref="SupplyRules.RuneTarget"/> of them. An unknown place is zero and skipped.
    /// </summary>
    public static List<Point3D> Places(IReadOnlyList<Point3D> wanted)
    {
        var places = new List<Point3D>();

        for (var i = 0; i < (wanted?.Count ?? 0) && places.Count < SupplyRules.RuneTarget; i++)
        {
            var place = wanted[i];

            if (place != Point3D.Zero && !NearAny(place, places))
            {
                places.Add(place);
            }
        }

        return places;
    }

    /// <summary>
    /// The doors, nearest first, that get a new book entry: each one that no mark the person
    /// keeps already lands near, at most <paramref name="room"/> of them. An unknown door is
    /// zero and skipped.
    /// </summary>
    public static List<Point3D> DoorsToMark(IReadOnlyList<Point3D> doors, IReadOnlyList<Point3D> marked, int room)
    {
        var picks = new List<Point3D>();

        for (var i = 0; i < (doors?.Count ?? 0) && picks.Count < room; i++)
        {
            var door = doors[i];

            if (door != Point3D.Zero && !NearAny(door, marked) && !NearAny(door, picks))
            {
                picks.Add(door);
            }
        }

        return picks;
    }

    /// <summary>
    /// A first day packs the kit; a character already in the world only once, and only when
    /// it carries no marked rune yet.
    /// </summary>
    public static bool PacksKit(bool firstDay, bool packedBefore, bool carriesMarked) =>
        firstDay || !packedBefore && !carriesMarked;

    /// <summary>Blank runes a marking mage carries beside its book: one, for a new place.</summary>
    public static int SpareBlanks(bool marks) => marks ? RunebookRules.SpareBlankRunes : 0;

    /// <summary>
    /// Packs the rune kit. World thread; call once the kit and the scrolls are packed: on the
    /// first day, or on the bind of a character already in the world. Either way the saved
    /// flag is set, so the kit is judged once per character and a novice keeps walking.
    /// Loose marked runes and spare scrolls go into the book first; an established fighter's
    /// book gets the dungeon doors it lacks last, on every bind. Last, a red's spare kit goes
    /// into its bank box once (<see cref="SpareKit.Seed"/>): this is the one call every bind makes.
    /// </summary>
    public static void Pack(SosariaCharacter character, bool firstDay)
    {
        var pack = character?.Backpack;

        if (pack == null || string.IsNullOrWhiteSpace(character.HomeFacet) ||
            !Map.TryParse(character.HomeFacet, null, out var map) || map == null || map == Map.Internal)
        {
            return;
        }

        var magery = character.Skills.Magery.Value;
        var established = CarriesMarkedRunes(character.PersonProfile.Tier, TravelsByMagic(magery, RecallStock(character)));
        ShelveLoose(character, established);

        if (PacksKit(firstDay, character.RuneKitPacked, RuneShelf.CarriesMarked(character)))
        {
            character.RuneKitPacked = true;

            if (established)
            {
                PackPlaces(character, map, magery);
            }
        }

        // A red's guarded town runes go first, so the room they held takes its camps; the Den's
        // bank comes next, before any camp, so the recall home after a death always has a rune.
        if (PkRules.IsRed(character.Kills))
        {
            DropGuardedEntries(character);
            MarkDen(character, map);
        }

        // A red's hot spots go in before the doors, so a full book still takes it off the Den.
        if (established && character.IsPk)
        {
            MarkHotSpots(character, map);
        }

        // A tamer's grounds go in before the doors: the trip to its beasts is the one it makes most.
        if (established && PetRules.KeepsPets(character.PersonProfile.Class))
        {
            MarkTamingGrounds(character, map);
        }

        if (established && character.Build?.IsFighter == true)
        {
            MarkDoors(character, map);
        }

        // Doors go in first, so a fighter's full book still starts its dungeon runs by recall.
        if (established && !character.IsPk && !PkRules.IsRed(character.Kills))
        {
            MarkBanks(character, map);
        }

        SpareKit.Seed(character);
    }

    /// <summary>The bank a red uses: the Den's teller nearest its home, or null when the facet has none.</summary>
    public static Destination DenBankOf(SosariaCharacter red) =>
        red == null ? null : NavWorld.DestinationsFor(red.HomeFacet)?.NearestBank(red.HomeSpot, murderer: true);

    /// <summary>
    /// A full book with no entry at the Den gives its last entry up for one: the recall home
    /// after a death outranks the camp or door the book took last. Pure.
    /// </summary>
    public static bool MakesRoomForDen(bool denMarked, int entries) => !denMarked && !RunebookRules.HasRoom(entries);

    /// <summary>
    /// Keeps an entry at the Den's bank in a red's book, on every bind: the recall home after
    /// a death lands there, beside the spare kit in the box (<see cref="SpareKit"/>). A red with
    /// no book has none to keep and walks home.
    /// </summary>
    private static void MarkDen(SosariaCharacter red, Map map)
    {
        if (RuneShelf.Book(red) is not { } book || DenBankOf(red) is not { } den)
        {
            return;
        }

        var landing = BankLanding(map, NavWorld.GraphFor(red.HomeFacet), den);

        if (landing == Point3D.Zero || !SpellHelper.CheckTravel(red, map, landing, TravelCheckType.RecallTo, out _))
        {
            return;
        }

        if (MakesRoomForDen(NearAny(landing, RuneShelf.MarkedTargets(red, map)), book.Entries.Count))
        {
            var keptDefault = book.Default;
            book.Entries.RemoveAt(book.Entries.Count - 1);
            book.Default = keptDefault;
        }

        MarkInBook(red, map, [landing]);
    }

    /// <summary>
    /// A raised red whose book holds no charge gets <see cref="RunebookRules.FreeHomeCharges"/>,
    /// so its recall home to the Den takes though the death took its scrolls. Logged. World thread.
    /// </summary>
    public static void ChargeForHome(SosariaCharacter red)
    {
        var book = RuneShelf.Book(red);

        if (red == null ||
            !RunebookRules.GetsFreeHomeCharge(PkRules.IsRed(red.Kills), book != null, book?.CurCharges ?? 0, red.Skills.Magery.Value))
        {
            return;
        }

        book.CurCharges = RunebookRules.FreeHomeCharges;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} finds {Charges} charge in its empty runebook for the way home to the Den", red.Name, book.CurCharges);
        }
    }

    /// <summary>
    /// The town banks of a catalog, one per town, nearest <paramref name="home"/> first. The
    /// reds' town is left out: a blue that recalled to the Den fought reds there for an hour.
    /// A teller within <see cref="RuneShelf.NearPlaceTiles"/> of a nearer one is the same bank.
    /// </summary>
    public static List<Destination> TownBanks(IReadOnlyList<Destination> places, Point3D home)
    {
        var banks = new List<Destination>();

        if (places == null)
        {
            return banks;
        }

        var nearestFirst = places
            .Where(static place =>
                place != null &&
                string.Equals(place.Kind, TownTripRules.BankKind, StringComparison.OrdinalIgnoreCase) &&
                place.Arrival != Point3D.Zero &&
                !PkRules.InBuccaneersDen(place.Arrival.X, place.Arrival.Y))
            .OrderBy(place => NavMetric.Chebyshev(home, place.Arrival));

        var kept = new List<Point3D>();

        foreach (var bank in nearestFirst)
        {
            if (!NearAny(bank.Arrival, kept))
            {
                kept.Add(bank.Arrival);
                banks.Add(bank);
            }
        }

        return banks;
    }

    /// <summary>
    /// Adds a book entry for every town bank of the facet no mark serves yet, nearest home
    /// first, while the book has room. The mark lands at the bank, or on its street node when
    /// the teller's own tile is taken.
    /// </summary>
    private static void MarkBanks(SosariaCharacter character, Map map)
    {
        var graph = NavWorld.GraphFor(character.HomeFacet);
        var banks = TownBanks(NavWorld.DestinationsFor(character.HomeFacet)?.All, character.HomeSpot);
        var landings = new List<Point3D>(banks.Count);

        for (var i = 0; i < banks.Count; i++)
        {
            var landing = BankLanding(map, graph, banks[i]);

            if (landing != Point3D.Zero &&
                SpellHelper.CheckTravel(character, map, landing, TravelCheckType.RecallTo, out _))
            {
                landings.Add(landing);
            }
        }

        MarkInBook(character, map, landings);
    }

    /// <summary>Where a bank rune lands: the bank itself where a person fits, else its bound street node, else zero.</summary>
    private static Point3D BankLanding(Map map, NavGraph graph, Destination bank)
    {
        var atBank = Landable(map, bank.Arrival);

        if (atBank != Point3D.Zero || graph?.TryGetNode(bank.Node, out var node) != true)
        {
            return atBank;
        }

        return Landable(map, node.Location);
    }

    /// <summary>The kit's first marks: home, corner and hunt, spare blanks, and the charged book.</summary>
    private static void PackPlaces(SosariaCharacter character, Map map, double magery)
    {
        var places = Places(WantedPlaces(character, map));

        if (places.Count > 0 && RuneShelf.Book(character) == null)
        {
            AddBook(character);
        }

        for (var i = 0; i < places.Count; i++)
        {
            var rune = RuneShelf.Blank(character) ?? AddRune(character);
            MarkAt(rune, places[i], map);
            RuneShelf.Shelve(character, rune);
        }

        for (var i = 0; i < SpareBlanks(magery >= MarkRules.MinMagery); i++)
        {
            AddRune(character);
        }

        StockBook(character);
    }

    /// <summary>
    /// The doors in the order a fighter's book takes them: those of its usual dungeons, the
    /// ones with a hall that fits it, first, each part in the order given. The era has more
    /// doors than a book has room for after its home, hunt and grounds, so nearest first a
    /// book could fill with doors of dungeons the fighter never goes to. Pure.
    /// </summary>
    public static List<DungeonDoor> UsualFirst(IReadOnlyList<DungeonDoor> doors, IReadOnlySet<string> usual)
    {
        var ordered = new List<DungeonDoor>(doors?.Count ?? 0);

        for (var i = 0; i < (doors?.Count ?? 0); i++)
        {
            if (usual?.Contains(doors[i].Dungeon) == true)
            {
                ordered.Add(doors[i]);
            }
        }

        for (var i = 0; i < (doors?.Count ?? 0); i++)
        {
            if (usual?.Contains(doors[i].Dungeon) != true)
            {
                ordered.Add(doors[i]);
            }
        }

        return ordered;
    }

    /// <summary>
    /// Adds a book entry for every dungeon door of the era's world that no mark serves yet and
    /// a recall may land at, the usual dungeons' doors first and nearest first within them
    /// (<see cref="UsualFirst"/>), while the book has room. The mark lands a few steps off the
    /// pad (<see cref="DungeonGround.StandBy(NavGraph, Point3D)"/>). No door in the Lost Lands
    /// takes a rune; those dungeons are walked to.
    /// </summary>
    private static void MarkDoors(SosariaCharacter character, Map map)
    {
        var doors = UsualFirst(
            DungeonGround.NearestFirst(
                DungeonGround.DoorsOf(map),
                character.HomeSpot,
                MoongateSeeds.LocationsFor(character.HomeFacet)
            ),
            DungeonGround.UsualFor(character)
        );

        if (doors.Count == 0)
        {
            return;
        }

        var graph = NavWorld.GraphFor(character.HomeFacet);
        var landings = new List<Point3D>(doors.Count);

        for (var i = 0; i < doors.Count; i++)
        {
            var landing = Landable(map, DungeonGround.StandBy(graph, doors[i].Door));

            if (landing != Point3D.Zero &&
                SpellHelper.CheckTravel(character, map, landing, TravelCheckType.RecallTo, out _))
            {
                landings.Add(landing);
            }
        }

        MarkInBook(character, map, PkRules.IsRed(character.Kills) ? RedLandingsOf(character, map, landings) : landings);
    }

    /// <summary>
    /// Adds a book entry for the best <see cref="TamingGroundRunes"/> taming grounds of the
    /// tamer that no mark serves yet and a recall may land at.
    /// </summary>
    private static void MarkTamingGrounds(SosariaCharacter tamer, Map map)
    {
        var grounds = TamingGrounds.Ranked(tamer, map, tamer.HomeSpot, TamingGroundRunes);
        var landings = new List<Point3D>(grounds.Count);

        for (var i = 0; i < grounds.Count; i++)
        {
            var landing = Landable(map, grounds[i].Place.Arrival);

            if (landing != Point3D.Zero && SpellHelper.CheckTravel(tamer, map, landing, TravelCheckType.RecallTo, out _))
            {
                landings.Add(landing);
            }
        }

        MarkInBook(tamer, map, landings);
    }

    /// <summary>
    /// Adds a book entry for every PvP hot spot of the facet that no mark serves yet, the
    /// heaviest first (the Yew gate leads), while the book has room. Buccaneer's Den is an
    /// island, so a red leaves its town by recall, as the era's reds did. The mark lands on
    /// the camp the red's gang takes, out of the guards' reach.
    /// </summary>
    private static void MarkHotSpots(SosariaCharacter red, Map map)
    {
        var crew = HotSpots.CrewKey(red);
        var landings = new List<Point3D>();

        foreach (var spot in HotSpots.For(map).OrderByDescending(static spot => spot.Weight))
        {
            var landing = Landable(map, spot.Camps[HotSpotRules.CampIndex(crew, spot.Camps.Count)]);

            if (landing != Point3D.Zero && SpellHelper.CheckTravel(red, map, landing, TravelCheckType.RecallTo, out _))
            {
                landings.Add(landing);
            }
        }

        MarkInBook(red, map, RedLandingsOf(red, map, landings));
    }

    /// <summary>
    /// The landings a red's book takes, in order: none under the guards, where it may not
    /// land, and none it walks to from home on a murderer's roads (the moongate camps the
    /// Den's own gate reaches), so the sixteen entries go to the camps and doors only a rune
    /// reaches. The Den's books held every moongate camp and no room was left for the Shame
    /// and Destard doors its reds were sent to. Pure.
    /// </summary>
    public static List<Point3D> RedLandings(
        IReadOnlyList<Point3D> landings,
        Func<Point3D, bool> guarded,
        Func<Point3D, bool> walks
    )
    {
        var picks = new List<Point3D>();

        for (var i = 0; i < (landings?.Count ?? 0); i++)
        {
            if (guarded?.Invoke(landings[i]) != true && walks?.Invoke(landings[i]) != true)
            {
                picks.Add(landings[i]);
            }
        }

        return picks;
    }

    private static List<Point3D> RedLandingsOf(SosariaCharacter red, Map map, IReadOnlyList<Point3D> landings) =>
        RedLandings(
            landings,
            landing => RuneShelf.BarredFor(red, landing, map),
            landing => RedGangReach.Walks(red, map, red.HomeSpot, landing)
        );

    /// <summary>
    /// Takes out of a red's book every entry that lands under the guards (see
    /// <see cref="RuneShelf.Barred"/>), as a player drags a dead rune out and throws it away.
    /// The book's default entry stays the same entry, or none when it went.
    /// </summary>
    private static void DropGuardedEntries(SosariaCharacter red)
    {
        if (RuneShelf.Book(red) is not { } book)
        {
            return;
        }

        var keptDefault = book.Default;

        for (var i = book.Entries.Count - 1; i >= 0; i--)
        {
            if (RuneShelf.BarredFor(red, book.Entries[i].Location, book.Entries[i].Map))
            {
                book.Entries.RemoveAt(i);
            }
        }

        // The engine finds the kept entry at its new index, and none for an entry that went.
        book.Default = keptDefault;
    }

    /// <summary>Marks a rune for each landing no mark the person keeps serves yet, while its book has room.</summary>
    private static void MarkInBook(SosariaCharacter character, Map map, IReadOnlyList<Point3D> landings)
    {
        if (landings.Count == 0)
        {
            return;
        }

        var book = RuneShelf.Book(character) ?? AddBook(character);
        var picks = DoorsToMark(landings, RuneShelf.MarkedTargets(character, map), RunebookRules.EntryCap - book.Entries.Count);

        for (var i = 0; i < picks.Count; i++)
        {
            var rune = AddRune(character);
            MarkAt(rune, picks[i], map);
            RuneShelf.Shelve(character, rune);
        }
    }

    /// <summary>
    /// Puts loose marked runes into the person's book, making one for an established traveler
    /// that has marked runes but no book, then charges it and banks the spare scrolls.
    /// </summary>
    private static void ShelveLoose(SosariaCharacter character, bool established)
    {
        var loose = RuneShelf.LooseMarked(character);

        if (!RunebookRules.KeepsBook(RuneShelf.Book(character) != null, established, loose.Count))
        {
            return;
        }

        if (RuneShelf.Book(character) == null)
        {
            AddBook(character);
        }

        for (var i = 0; i < loose.Count; i++)
        {
            RuneShelf.Shelve(character, loose[i]);
        }

        StockBook(character);
    }

    /// <summary>The recall scrolls charge the book; what the charges and a spare pair leave over goes to the bank box.</summary>
    private static void StockBook(SosariaCharacter character)
    {
        var book = RuneShelf.Book(character);
        var pack = character.Backpack;

        if (book == null || pack == null)
        {
            return;
        }

        RuneShelf.Recharge(character, book);

        var loose = pack.GetAmount(typeof(RecallScroll));
        var target = SupplyRules.RecallTarget(character.Skills.Magery.Value, SupplyCheck.ProfileOf(character).Wealth);
        var surplus = RunebookRules.ScrollSurplus(loose, book.CurCharges, target);

        if (surplus > 0 && character.BankBox is { } bank && pack.FindItemByType<RecallScroll>() is { } stack)
        {
            SupplyCheck.MoveUnits(stack, Math.Min(surplus, stack.Amount), bank);
        }
    }

    /// <summary>Recall scrolls in the pack and charges in the book: what the person can travel on without the spell.</summary>
    public static int RecallStock(SosariaCharacter character) =>
        (character.Backpack?.GetAmount(typeof(RecallScroll)) ?? 0) + (RuneShelf.Book(character)?.CurCharges ?? 0);

    private static Runebook AddBook(SosariaCharacter character)
    {
        var book = new Runebook();
        character.AddToBackpack(book);
        return book;
    }

    /// <summary>
    /// The places a rune could land on, each on a tile a person can stand; zero when none.
    /// A fighter's dungeon doors come from <see cref="MarkDoors"/>.
    /// </summary>
    private static List<Point3D> WantedPlaces(SosariaCharacter character, Map map)
    {
        var wanted = new List<Point3D> { Landable(map, character.HomeSpot), Landable(map, character.HomeCorner) };

        if (character.Build?.IsFighter == true && character.HuntHomeNow()?.Ground is { } ground &&
            MarkRules.TripWorthARune(NavMetric.Chebyshev(character.HomeSpot, ground.Arrival)))
        {
            wanted.Add(Landable(map, ground.Arrival));
        }

        return wanted;
    }

    /// <summary>A recall lands only where a person fits: the place, the place on the ground, or zero.</summary>
    private static Point3D Landable(Map map, Point3D place) =>
        place == Point3D.Zero ? place : GateTravel.TryExitAt(map, place, out var at) ? at : Point3D.Zero;

    private static RecallRune AddRune(SosariaCharacter character)
    {
        var rune = new RecallRune();
        character.AddToBackpack(rune);
        return rune;
    }

    private static void MarkAt(RecallRune rune, Point3D at, Map map)
    {
        rune.Target = at;
        rune.TargetMap = map;
        rune.Marked = true;
        rune.Description = BaseRegion.GetRuneNameFor(Region.Find(at, map));
    }

    private static bool NearAny(Point3D place, IReadOnlyList<Point3D> places)
    {
        for (var i = 0; i < (places?.Count ?? 0); i++)
        {
            if (NavMetric.Chebyshev(place, places[i]) <= RuneShelf.NearPlaceTiles)
            {
                return true;
            }
        }

        return false;
    }
}
