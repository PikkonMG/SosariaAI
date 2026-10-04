using System;
using System.Collections.Generic;
using System.IO;
using Server;
using SosariaAI.Admin;
using SosariaAI.Behaviour;
using SosariaAI.Navigation.Generation;

namespace SosariaAI.Navigation;

/// <summary>
/// Reads what <see cref="PlaceRules"/> judges from the server's data folder: the teleporter
/// ends that carry a person into a region, and the spawners of the era's spawn files. The
/// live world and the real-map tests read the same files through it.
/// </summary>
public static class PlaceData
{
    /// <summary>
    /// The book of a facet's places in <paramref name="expansion"/>. Without spawn files the
    /// data folder is missing, not the places: every region the era has then stays as its
    /// data records it, and only the later-era places are dropped.
    /// </summary>
    /// <param name="spawnSets">The era's spawn sets, such as "shared" for The Second Age.</param>
    public static PlaceBook Read(
        IReadOnlyList<PlaceRegion> regions,
        string dataRoot,
        string facet,
        IReadOnlyList<string> spawnSets,
        Expansion expansion
    )
    {
        var spawns = SpawnFiles(WorldGenerator.SpawnFolders(dataRoot, facet, spawnSets), facet);

        if (spawns.Count == 0)
        {
            return Unjudged(regions, expansion);
        }

        var teleporters = Path.Combine(dataRoot, WorldGenerator.TeleportersFileName);
        IReadOnlyList<TeleporterLink> links = File.Exists(teleporters)
            ? WorldDataSeeds.ParseTeleporters(File.ReadAllText(teleporters))
            : [];
        return new PlaceBook(regions, PlaceRules.Book(regions, WaysIn(links, facet), spawns, expansion));
    }

    /// <summary>
    /// Every teleporter end on the facet that sends a person on: each pad to its landing, and
    /// a two-way pad's landing back to it.
    /// </summary>
    public static List<WayIn> WaysIn(IReadOnlyList<TeleporterLink> links, string facet)
    {
        var ways = new List<WayIn>();

        foreach (var link in links ?? [])
        {
            if (!WorldDataSeeds.OnFacet(link.SrcMap, facet) || !WorldDataSeeds.OnFacet(link.DstMap, facet))
            {
                continue;
            }

            var pad = new Point3D(link.Sx, link.Sy, link.Sz);
            var landing = new Point3D(link.Dx, link.Dy, link.Dz);
            ways.Add(new WayIn(pad, landing));

            if (link.Back)
            {
                ways.Add(new WayIn(landing, pad));
            }
        }

        return ways;
    }

    /// <summary>The facet's spawners in each spawn file of the folders, by file name, in name order.</summary>
    public static List<SpawnFile> SpawnFiles(IEnumerable<string> folders, string facet)
    {
        var files = new List<SpawnFile>();

        foreach (var folder in folders ?? [])
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            var paths = Directory.GetFiles(folder, WorldSetupRules.SpawnFilePattern);
            Array.Sort(paths, StringComparer.Ordinal);

            foreach (var path in paths)
            {
                var spawners = new List<Point3D>();

                foreach (var seed in SpawnerSeeds.Parse(File.ReadAllText(path)))
                {
                    if (WorldDataSeeds.OnFacet(seed.Map, facet))
                    {
                        spawners.Add(new Point3D(seed.X, seed.Y, seed.Z));
                    }
                }

                if (spawners.Count > 0)
                {
                    files.Add(new SpawnFile(Path.GetFileNameWithoutExtension(path), spawners));
                }
            }
        }

        return files;
    }

    private static PlaceBook Unjudged(IReadOnlyList<PlaceRegion> regions, Expansion expansion)
    {
        var places = new List<Place>();

        foreach (var region in regions ?? [])
        {
            if (!EraRules.PlaceAllowed(region.Name, expansion))
            {
                continue;
            }

            var door = region.Entrance != Point3D.Zero ? region.Entrance : region.GoLocation;
            places.Add(new Place(region.Name, region.IsTown, region.Name, door, []));
        }

        return new PlaceBook(regions, places);
    }
}
