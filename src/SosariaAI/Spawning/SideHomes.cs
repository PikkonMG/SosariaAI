using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Navigation;

namespace SosariaAI.Spawning;

/// <summary>
/// Order and Chaos fighters do not share a home town. They draw on sight anywhere, so a
/// fresh world that put both sides at one bank opened with 32 fights in its first minute.
/// Every town belongs to one side in one fixed table, the same for every job: when each job
/// list split its own towns by list order, Minoc was Order for hunters and Chaos for town
/// folk, and both sides met at its bank again. Both sides still live all over the map and
/// meet on the roads, at the hunts and in each other's towns. Everyone else uses every town.
/// Pure.
/// </summary>
public static class SideHomes
{
    /// <summary>
    /// A home this near a town's bank belongs to that town. The Britain fishing shore lies 45
    /// tiles from its bank; the next town is hundreds of tiles away.
    /// </summary>
    public const int TownReach = 64;

    private static readonly (Point3D Bank, GuildType Side)[] TownSides =
    [
        (WorkSites.BritainTown, GuildType.Order),
        (WorkSites.MinocTown, GuildType.Order),
        (WorkSites.SkaraTown, GuildType.Order),
        (WorkSites.JhelomTown, GuildType.Order),
        (WorkSites.CoveTown, GuildType.Order),
        (WorkSites.YewTown, GuildType.Chaos),
        (WorkSites.TrinsicTown, GuildType.Chaos),
        (WorkSites.MoonglowTown, GuildType.Chaos),
        (WorkSites.MaginciaTown, GuildType.Chaos),
        (WorkSites.VesperTown, GuildType.Chaos)
    ];

    /// <summary>The side whose town holds this home, or Regular for a home in no town (the Den, a mine ledge).</summary>
    public static GuildType SideOf(Point3D home)
    {
        var side = GuildType.Regular;
        var nearest = TownReach + 1;

        for (var i = 0; i < TownSides.Length; i++)
        {
            var distance = NavMetric.Chebyshev(home, TownSides[i].Bank);

            if (distance < nearest)
            {
                nearest = distance;
                side = TownSides[i].Side;
            }
        }

        return side;
    }

    /// <summary>True when a person of this side may call this home its own.</summary>
    public static bool Allows(GuildType side, Point3D home)
    {
        if (side is not (GuildType.Order or GuildType.Chaos))
        {
            return true;
        }

        var town = SideOf(home);
        return town == GuildType.Regular || town == side;
    }

    /// <summary>
    /// The side a person's home holds it to. A fixture keeps its authored home, so its side
    /// follows that town: a Chaos fixture at the Britain bank opened a fresh world with a fight.
    /// A copy's home follows its side, so it is held to none.
    /// </summary>
    public static GuildType HomeSide(string uniqueId, Point3D authored) =>
        WorkSites.IsCopy(uniqueId) ? GuildType.Regular : SideOf(authored);

    /// <summary>
    /// The sites a person of this side may call home, in list order. A list with no town of
    /// the side gives that side's bank towns instead.
    /// </summary>
    public static IReadOnlyList<WorkSite> For(GuildType side, IReadOnlyList<WorkSite> sites)
    {
        if (side is not (GuildType.Order or GuildType.Chaos) || sites == null)
        {
            return sites;
        }

        var own = new List<WorkSite>();

        for (var i = 0; i < sites.Count; i++)
        {
            if (Allows(side, sites[i].Home))
            {
                own.Add(sites[i]);
            }
        }

        return own.Count > 0 ? own : For(side, WorkSites.TownSites);
    }
}
