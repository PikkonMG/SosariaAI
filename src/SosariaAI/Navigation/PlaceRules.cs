using System;
using System.Collections.Generic;
using System.Text;
using Server;
using SosariaAI.Behaviour;

namespace SosariaAI.Navigation;

/// <summary>A named town or dungeon region of the world's data, as the place rule reads it.</summary>
/// <param name="Entrance">The door the data records outside the region, or <see cref="Point3D.Zero"/> for a town walked into.</param>
/// <param name="GoLocation">Where the data sends a visitor inside the region.</param>
public sealed record PlaceRegion(
    string Name,
    bool IsTown,
    IReadOnlyList<Rectangle3D> Areas,
    Point3D Entrance,
    Point3D GoLocation
)
{
    public bool Contains(Point3D point)
    {
        var tile = new Point2D(point.X, point.Y);

        for (var i = 0; i < Areas.Count; i++)
        {
            if (Areas[i].Contains(tile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The recorded entrance when it lies outside the region, where a person leaving stands; else zero.</summary>
    public Point3D OutsideEntrance => Entrance != Point3D.Zero && !Contains(Entrance) ? Entrance : Point3D.Zero;
}

/// <summary>A pad that sends a person from its tile to a landing, one way: a teleporter end.</summary>
public readonly record struct WayIn(Point3D Pad, Point3D Landing);

/// <summary>One of the engine's spawn files for this era and facet: its name and where its spawners stand.</summary>
public sealed record SpawnFile(string Name, IReadOnlyList<Point3D> Spawners);

/// <summary>
/// A town or dungeon that stays in the world: the name people give it, the region it lies
/// in, its door (the pad or mouth a player goes in by, or a town's go location), and its
/// spawners, which tell the parts of a shared region apart.
/// </summary>
public sealed record Place(string Name, bool IsTown, string Region, Point3D Door, IReadOnlyList<Point3D> Spawners);

/// <summary>
/// Which towns and dungeons of the world's data belong in the world, and under what name.
/// A place stays when the running era has it (<see cref="EraRules.PlaceAllowed"/>), and it
/// has a working way in and live spawns. The way in is its recorded
/// door: a teleporter pad near the entrance the data records that lands inside the place,
/// an entrance inside its own area, or for a town with no entrance its streets. The spawns
/// are the era's own spawn files. The spawn files alone are no era test: ModernUO's
/// "shared" set holds Blighted Grove and Sanctuary, whose pads work in every era.
/// A dungeon region holding the halls of several spawn files is a grouping of dungeons, as
/// ModernUO's "Misc Dungeons": each file there is a dungeon of its own, named by its file
/// ("BritainSewer" is the Britain Sewer) and entered by its own pad. Pure.
/// </summary>
public static class PlaceRules
{
    /// <summary>
    /// A pad this close to a recorded entrance is its door. Wrong's recorded entrance is the
    /// cave mouth, eleven tiles short of its pad.
    /// </summary>
    public const int DoorReach = 16;

    /// <summary>A landing this close to a part's spawners opens onto that part of a shared region.</summary>
    public const int PartReach = 60;

    /// <summary>A spawn file belongs to a region holding at least one part in this many of its spawners.</summary>
    public const int OwnShareDivisor = 2;

    /// <summary>The places that stay in <paramref name="expansion"/>, in the order of <paramref name="regions"/>.</summary>
    public static List<Place> Book(
        IReadOnlyList<PlaceRegion> regions,
        IReadOnlyList<WayIn> waysIn,
        IReadOnlyList<SpawnFile> spawns,
        Expansion expansion
    )
    {
        var places = new List<Place>();

        foreach (var region in regions ?? [])
        {
            if (!EraRules.PlaceAllowed(region.Name, expansion))
            {
                continue;
            }

            var inside = SpawnersIn(region, spawns);

            if (inside.Count == 0 || !TryDoor(region, waysIn, out var door))
            {
                continue;
            }

            var parts = region.IsTown ? new List<SpawnFile>() : OwnFiles(region, spawns);

            if (parts.Count <= 1)
            {
                places.Add(new Place(region.Name, region.IsTown, region.Name, door, inside));
                continue;
            }

            foreach (var part in parts)
            {
                var partSpawners = SpawnersIn(region, [part]);

                if (TryPartDoor(region, partSpawners, waysIn, out var partDoor))
                {
                    places.Add(new Place(SpokenFileName(part.Name), false, region.Name, partDoor, partSpawners));
                }
            }
        }

        return places;
    }

    /// <summary>A spawn file's name as people say it: "BritainSewer" is "Britain Sewer".</summary>
    public static string SpokenFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        var spoken = new StringBuilder(fileName.Length + fileName.Length / 2);

        for (var i = 0; i < fileName.Length; i++)
        {
            var letter = fileName[i];

            if (i > 0 && char.IsUpper(letter) && char.IsLower(fileName[i - 1]))
            {
                spoken.Append(' ');
            }

            spoken.Append(letter);
        }

        return spoken.ToString();
    }

    /// <summary>
    /// The door of a place: for a town with no recorded entrance, its go location; for an
    /// entrance inside the place's own area, the entrance; else the pad nearest the entrance,
    /// within <see cref="DoorReach"/>, that lands inside the place.
    /// </summary>
    private static bool TryDoor(PlaceRegion region, IReadOnlyList<WayIn> waysIn, out Point3D door)
    {
        if (region.Entrance == Point3D.Zero)
        {
            door = region.GoLocation;
            return region.IsTown && door != Point3D.Zero;
        }

        if (region.Contains(region.Entrance))
        {
            door = region.Entrance;
            return true;
        }

        door = Point3D.Zero;
        var best = int.MaxValue;

        foreach (var way in waysIn ?? [])
        {
            var tiles = NavMetric.Chebyshev(way.Pad, region.Entrance);

            if (tiles <= DoorReach && tiles < best && IsWayInto(region, way))
            {
                best = tiles;
                door = way.Pad;
            }
        }

        return best != int.MaxValue;
    }

    /// <summary>
    /// The door of one part of a shared region: of the pads that land within
    /// <see cref="PartReach"/> of the part's spawners, the one nearest the region's recorded
    /// entrance, which is the side the data calls the front.
    /// </summary>
    private static bool TryPartDoor(PlaceRegion region, List<Point3D> spawners, IReadOnlyList<WayIn> waysIn, out Point3D door)
    {
        door = Point3D.Zero;
        var best = int.MaxValue;

        foreach (var way in waysIn ?? [])
        {
            if (!IsWayInto(region, way) || NavMetric.NearestOf(way.Landing, spawners) > PartReach)
            {
                continue;
            }

            var tiles = NavMetric.Chebyshev(way.Pad, region.Entrance);

            if (tiles < best)
            {
                best = tiles;
                door = way.Pad;
            }
        }

        return best != int.MaxValue;
    }

    private static bool IsWayInto(PlaceRegion region, WayIn way) =>
        !region.Contains(way.Pad) && region.Contains(way.Landing);

    /// <summary>The spawn files most of whose spawners stand inside the region, in file order.</summary>
    private static List<SpawnFile> OwnFiles(PlaceRegion region, IReadOnlyList<SpawnFile> spawns)
    {
        var own = new List<SpawnFile>();

        foreach (var file in spawns ?? [])
        {
            var total = file.Spawners?.Count ?? 0;

            if (total > 0 && SpawnersIn(region, [file]).Count * OwnShareDivisor >= total)
            {
                own.Add(file);
            }
        }

        return own;
    }

    private static List<Point3D> SpawnersIn(PlaceRegion region, IReadOnlyList<SpawnFile> spawns)
    {
        var inside = new List<Point3D>();

        foreach (var file in spawns ?? [])
        {
            foreach (var spawner in file.Spawners ?? [])
            {
                if (region.Contains(spawner))
                {
                    inside.Add(spawner);
                }
            }
        }

        return inside;
    }
}
