using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Navigation;

/// <summary>
/// One creature of one spawner: where the spawner stands, how far its creatures roam, how many
/// of this one stand at once, and the dungeon it stands in (null on open ground).
/// </summary>
public readonly record struct SpawnPoint(Point3D At, int HomeRange, string Creature, double Count, string Place);

/// <summary>
/// How hard a hunt spot is, from the spawners the world actually runs. A dungeon hall is as
/// hard as the whole floor it stands on (<see cref="DungeonMap"/> scores each floor); a spot
/// on open ground is as hard as the spawn whose walk reaches its hunt square. Both count every
/// foe by how many stand at once and weigh the strong ones (<see cref="AreaDifficulty.SpawnScore"/>).
/// The old rating took the two weakest creature types within 48 tiles on any floor, so a
/// novice's best ground was the Cove swamp of bog things and plague beasts, and the dragon
/// cave of Destard read as its giant serpents. Saved catalogs are rated again at every boot,
/// so an old file needs no rebuild. Pure.
/// </summary>
public static class GroundRating
{
    /// <summary>
    /// A spawner's creatures reach a hunt square when the spawner stands within the square's
    /// half side of it, plus its home range up to that much again.
    /// </summary>
    public const int SpotRange = HuntGround.AreaRadius;

    /// <summary>
    /// Every foe of every spawner, with how many of it stand at once. Vendors and creatures a
    /// fighter does not hunt (<paramref name="isEnemy"/>, null for all) are left out.
    /// <paramref name="placeAt"/> names the dungeon a spawner stands in.
    /// </summary>
    public static List<SpawnPoint> Points(
        IReadOnlyList<SpawnerSeed> spawners,
        Func<string, bool> isEnemy,
        Func<Point3D, string> placeAt
    )
    {
        var points = new List<SpawnPoint>();

        for (var i = 0; i < (spawners?.Count ?? 0); i++)
        {
            var spawner = spawners[i];
            var at = new Point3D(spawner.X, spawner.Y, spawner.Z);
            var standing = SpawnerSeeds.Standing(spawner);
            string place = null;
            var placed = false;

            for (var j = 0; j < standing.Count; j++)
            {
                var creature = standing[j].Creature;

                if (string.IsNullOrWhiteSpace(creature) || SpawnerSeeds.IsVendorName(creature) ||
                    isEnemy?.Invoke(creature) == false)
                {
                    continue;
                }

                if (!placed)
                {
                    place = placeAt?.Invoke(at);
                    placed = true;
                }

                points.Add(new SpawnPoint(at, spawner.HomeRange, creature, standing[j].Count, place));
            }
        }

        return points;
    }

    /// <summary>The spawn whose walk reaches the hunt square round <paramref name="spot"/>, in the same place as it.</summary>
    public static List<SpawnCount> AroundSpot(IReadOnlyList<SpawnPoint> points, Point3D spot, string place)
    {
        var spawn = new List<SpawnCount>();

        for (var i = 0; i < (points?.Count ?? 0); i++)
        {
            var point = points[i];

            if (string.Equals(point.Place, place, StringComparison.OrdinalIgnoreCase) &&
                NavMetric.Chebyshev(spot, point.At) <= SpotRange + Math.Clamp(point.HomeRange, 0, SpotRange))
            {
                spawn.Add(new SpawnCount(point.Creature, point.Count));
            }
        }

        return spawn;
    }

    /// <summary>
    /// Rates every hunt spot of the catalog in place: a hall by its floor, when the floor holds
    /// any spawn, else by the spawn round it. One spawner lists a spot for each of its
    /// creatures, so each spot is scored once.
    /// </summary>
    public static void Rate(
        DestinationCatalog catalog,
        DungeonMap floors,
        IReadOnlyList<SpawnPoint> points,
        Func<Point3D, string> placeAt,
        Func<string, HostileStats?> lookup
    )
    {
        var scored = new Dictionary<Point3D, int>();

        for (var i = 0; i < (catalog?.All.Count ?? 0); i++)
        {
            var place = catalog.All[i];

            if (place.ParsedKind != DestinationKind.Hunt)
            {
                continue;
            }

            var at = place.Arrival;

            if (!scored.TryGetValue(at, out var difficulty))
            {
                difficulty = floors?.FloorAt(at) is { Difficulty: > 0 } floor
                    ? floor.Difficulty
                    : AreaDifficulty.SpawnScore(AroundSpot(points, at, placeAt?.Invoke(at)), lookup);
                scored[at] = difficulty;
            }

            place.Difficulty = difficulty;
        }
    }
}
