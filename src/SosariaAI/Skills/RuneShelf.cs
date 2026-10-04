using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Where a marked rune leads, whether that is on the map in question, and whether the
/// traveler may not land there (see <see cref="RuneShelf.Barred"/>).
/// </summary>
public readonly record struct RuneMark(Point3D Target, bool SameMap, bool Barred = false);

/// <summary>
/// One place a person can travel to: a loose rune, or one entry of its runebook. A blank
/// rune is a mark too, for the Mark spell to fill.
/// </summary>
public sealed class TravelMark
{
    private TravelMark(RecallRune rune, Runebook book, RunebookEntry entry, Point3D target, Map targetMap)
    {
        Rune = rune;
        Book = book;
        Entry = entry;
        Target = target;
        TargetMap = targetMap;
    }

    public RecallRune Rune { get; }

    public Runebook Book { get; }

    public RunebookEntry Entry { get; }

    public Point3D Target { get; }

    public Map TargetMap { get; }

    public bool InBook => Entry != null;

    public bool Marked => InBook || Rune?.Marked == true;

    public bool Deleted => InBook ? Book.Deleted || !Book.Entries.Contains(Entry) : Rune == null || Rune.Deleted;

    public static TravelMark Of(RecallRune rune) => rune == null ? null : new(rune, null, null, rune.Target, rune.TargetMap);

    public static TravelMark Of(Runebook book, RunebookEntry entry) =>
        book == null || entry == null ? null : new(null, book, entry, entry.Location, entry.Map);
}

/// <summary>
/// The marked places a person carries: the entries of its runebook and the odd loose rune,
/// plus blank runes to mark. A mark counts for a place when it lands near it: at the home
/// bank, at a dungeon door. The engine refuses Mark, Recall and Gate inside a Felucca
/// dungeon, so the mark for a hall is the one at the door outside, as players kept a
/// "Destard" entry for the entrance. A newly marked rune goes into the book.
/// </summary>
public static class RuneShelf
{
    /// <summary>A rune marked this close to a place serves it: the walk from the landing is short.</summary>
    public const int NearPlaceTiles = 48;

    /// <summary>
    /// Where a rune for <paramref name="goal"/> has to land: the door of the dungeon the goal
    /// lies in, or the goal itself on open ground or when the door is not known.
    /// </summary>
    public static Point3D LandingFor(Point3D goal, string dungeon, Point3D door) =>
        dungeon != null && door != Point3D.Zero ? door : goal;

    /// <summary>World side of <see cref="LandingFor(Point3D, string, Point3D)"/>: the dungeon region and its door on the map.</summary>
    public static Point3D LandingFor(Map map, Point3D goal)
    {
        var dungeon = DungeonGround.RegionNames(map)(goal);
        return LandingFor(goal, dungeon, dungeon == null ? Point3D.Zero : DungeonGround.DoorOf(map, dungeon));
    }

    /// <summary>The index of the mark nearest <paramref name="place"/> within reach that the traveler may land on, or -1.</summary>
    public static int IndexNearest(Point3D place, IReadOnlyList<RuneMark> marks, int withinTiles)
    {
        if (marks == null)
        {
            return -1;
        }

        var best = -1;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < marks.Count; i++)
        {
            if (!marks[i].SameMap || marks[i].Barred)
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(place, marks[i].Target);

            if (distance <= withinTiles && distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// A red lands nowhere under the guards: they strike it down where it lands. An old town
    /// rune in the book of a character that turned red is a mark it may not use.
    /// </summary>
    public static bool Barred(bool red, bool landsGuarded) => !PkRules.MayVisit(red, landsGuarded);

    /// <summary>World side of <see cref="Barred"/>: true when this traveler may not land at the target.</summary>
    public static bool BarredFor(Mobile traveler, Point3D target, Map targetMap)
    {
        var red = traveler != null && PkRules.IsRed(traveler.Kills);
        return red && Barred(red, GuardCall.IsGuardedPlace(target, targetMap));
    }

    /// <summary>The marked place, loose rune or book entry, that lands nearest a place on a map and that the person may land on, or null.</summary>
    public static TravelMark MarkedNear(SosariaCharacter character, Point3D place, Map map, int withinTiles = NearPlaceTiles)
    {
        var found = Marks(character);
        var marks = new List<RuneMark>(found.Count);

        for (var i = 0; i < found.Count; i++)
        {
            var sameMap = found[i].TargetMap == map;
            marks.Add(new RuneMark(found[i].Target, sameMap, sameMap && BarredFor(character, found[i].Target, map)));
        }

        var index = IndexNearest(place, marks, withinTiles);
        return index < 0 ? null : found[index];
    }

    /// <summary>Where each marked place the person carries and may land on lands on <paramref name="map"/>.</summary>
    public static List<Point3D> MarkedTargets(SosariaCharacter character, Map map)
    {
        var found = Marks(character);
        var targets = new List<Point3D>(found.Count);

        for (var i = 0; i < found.Count; i++)
        {
            if (found[i].TargetMap == map && !BarredFor(character, found[i].Target, map))
            {
                targets.Add(found[i].Target);
            }
        }

        return targets;
    }

    /// <summary>True when the person carries at least one marked place, loose or in its book.</summary>
    public static bool CarriesMarked(SosariaCharacter character) => Marks(character).Count > 0;

    public static RecallRune Blank(SosariaCharacter character)
    {
        foreach (var rune in Runes(character))
        {
            if (!rune.Marked)
            {
                return rune;
            }
        }

        return null;
    }

    /// <summary>The runebook in the pack, or null. It rides in the pack, never in the hand.</summary>
    public static Runebook Book(SosariaCharacter character)
    {
        var pack = character?.Backpack;

        if (pack == null)
        {
            return null;
        }

        foreach (var item in pack.Items)
        {
            if (item is Runebook { Deleted: false } book)
            {
                return book;
            }
        }

        return null;
    }

    /// <summary>Marked runes lying loose in the pack.</summary>
    public static List<RecallRune> LooseMarked(SosariaCharacter character)
    {
        var loose = new List<RecallRune>();

        foreach (var rune in Runes(character))
        {
            if (rune.Marked && rune.TargetMap != null)
            {
                loose.Add(rune);
            }
        }

        return loose;
    }

    /// <summary>
    /// Drops a marked rune into the book, as a player drags it onto the book: the engine
    /// takes the entry and the rune is gone. False when there is no book or no room.
    /// </summary>
    public static bool Shelve(SosariaCharacter character, RecallRune rune)
    {
        var book = Book(character);

        return book != null && rune is { Deleted: false, Marked: true } &&
               RunebookRules.HasRoom(book.Entries.Count) && book.OnDragDrop(character, rune);
    }

    /// <summary>Drops the pack's recall scrolls on the book up to its charge cap. The engine takes what fits.</summary>
    public static void Recharge(SosariaCharacter character, Runebook book)
    {
        if (book == null || book.CurCharges >= book.MaxCharges ||
            character?.Backpack?.FindItemByType<RecallScroll>() is not { } scrolls)
        {
            return;
        }

        book.OnDragDrop(character, scrolls);
    }

    /// <summary>Every marked place the person carries: its book's entries first, then loose runes.</summary>
    private static List<TravelMark> Marks(SosariaCharacter character)
    {
        var marks = new List<TravelMark>();
        var book = Book(character);

        for (var i = 0; i < (book?.Entries.Count ?? 0); i++)
        {
            if (book.Entries[i].Map != null)
            {
                marks.Add(TravelMark.Of(book, book.Entries[i]));
            }
        }

        foreach (var rune in LooseMarked(character))
        {
            marks.Add(TravelMark.Of(rune));
        }

        return marks;
    }

    private static IEnumerable<RecallRune> Runes(SosariaCharacter character)
    {
        var pack = character?.Backpack;

        if (pack == null)
        {
            yield break;
        }

        foreach (var item in pack.Items)
        {
            if (item is RecallRune { Deleted: false } rune)
            {
                yield return rune;
            }
        }
    }
}
