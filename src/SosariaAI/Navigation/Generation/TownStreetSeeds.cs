using System;
using System.Collections.Generic;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// Street nodes for every town. The generator lays roads between the pieces of the
/// graph, but a town's own seeds are its shops, which sit under roofs, and a walk from
/// the bank to the inn in the same town had no node to start from. A grid over each town
/// region, on open standable ground, gives every town its streets.
/// </summary>
public static class TownStreetSeeds
{
    public const int GridStep = 12;
    public const string NamePrefix = "street";
    public const string Kind = "";

    public static List<GraphSeed> Collect(
        IReadOnlyList<RegionSeed> regions,
        Func<int, int, int, bool> canStand,
        Func<int, int, int> groundZ,
        Func<int, int, int, bool> isIndoor
    )
    {
        var seeds = new List<GraphSeed>();

        if (regions == null || canStand == null || groundZ == null)
        {
            return seeds;
        }

        bool Stand(int x, int y, int z) => canStand(x, y, z) && isIndoor?.Invoke(x, y, z) != true;

        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];

            if (!WorldDataSeeds.IsTown(region.Type) || region.Areas == null)
            {
                continue;
            }

            for (var a = 0; a < region.Areas.Count; a++)
            {
                AddGrid(seeds, region.Areas[a], Stand, groundZ);
            }
        }

        return seeds;
    }

    private static void AddGrid(
        List<GraphSeed> seeds,
        RegionArea area,
        Func<int, int, int, bool> stand,
        Func<int, int, int> groundZ
    )
    {
        for (var y = area.Y; y <= area.Y + area.Height; y += GridStep)
        {
            for (var x = area.X; x <= area.X + area.Width; x += GridStep)
            {
                var z = groundZ(x, y);

                if (!stand(x, y, z))
                {
                    continue;
                }

                seeds.Add(new GraphSeed($"{NamePrefix}-{x}-{y}", x, y, z, Kind));
            }
        }
    }
}
