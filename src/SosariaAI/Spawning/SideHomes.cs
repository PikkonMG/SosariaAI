using System.Collections.Generic;
using Server;
using Server.Guilds;

namespace SosariaAI.Spawning;

/// <summary>
/// Order and Chaos fighters do not share a home town. They draw on sight anywhere, so a
/// fresh world that put both sides at one bank opened with 32 fights in its first minute. The
/// distinct home towns of a site list are split by their order in the list: the first, third,
/// fifth town belong to Order, the rest to Chaos. Both sides still live all over the map and
/// meet on the roads, at the hunts and in each other's towns. Everyone else uses every town.
/// Pure.
/// </summary>
public static class SideHomes
{
    /// <summary>Order takes the towns at even places in the list, Chaos the odd ones.</summary>
    public const int SideCount = 2;

    public const int OrderPlace = 0;
    public const int ChaosPlace = 1;

    /// <summary>A list with fewer distinct towns than the sides cannot be split, and is kept whole.</summary>
    public const int MinTownsToSplit = SideCount;

    /// <summary>The sites a person of this side may call home, in list order.</summary>
    public static IReadOnlyList<WorkSite> For(GuildType side, IReadOnlyList<WorkSite> sites)
    {
        if (side is not (GuildType.Order or GuildType.Chaos) || sites == null)
        {
            return sites;
        }

        var towns = new List<Point3D>();

        for (var i = 0; i < sites.Count; i++)
        {
            if (!towns.Contains(sites[i].Home))
            {
                towns.Add(sites[i].Home);
            }
        }

        if (towns.Count < MinTownsToSplit)
        {
            return sites;
        }

        var place = side == GuildType.Order ? OrderPlace : ChaosPlace;
        var own = new List<WorkSite>();

        for (var i = 0; i < sites.Count; i++)
        {
            if (towns.IndexOf(sites[i].Home) % SideCount == place)
            {
                own.Add(sites[i]);
            }
        }

        return own;
    }
}
