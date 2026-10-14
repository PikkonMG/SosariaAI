using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Navigation;

namespace SosariaAI.Spawning;

/// <summary>A town's bank and the side whose town it is.</summary>
public readonly record struct TownSide(Point3D Bank, GuildType Side);

/// <summary>
/// Order and Chaos fighters do not share a home town. They draw on sight anywhere, so a
/// fresh world that put both sides at one bank opened with 32 fights in its first minute.
/// Every town belongs to one side in one fixed table per facet (<see cref="FacetAnchors.TownSides"/>),
/// the same for every job: when each job list split its own towns by list order, Minoc was
/// Order for hunters and Chaos for town folk, and both sides met at its bank again. Both sides
/// still live all over the map and meet on the roads, at the hunts and in each other's towns.
/// Everyone else uses every town. Pure.
/// </summary>
public static class SideHomes
{
    /// <summary>
    /// A home this near a town's bank belongs to that town. The Britain fishing shore lies 45
    /// tiles from its bank; the next town is hundreds of tiles away.
    /// </summary>
    public const int TownReach = 64;

    /// <summary>
    /// A map with fewer bank towns than this gives no town a side: a one-bank map would
    /// otherwise leave one side with no home at all.
    /// </summary>
    public const int MinSidedTowns = 2;

    /// <summary>Britannia's towns by side.</summary>
    public static readonly TownSide[] BritanniaTowns =
    [
        new(WorkSites.BritainTown, GuildType.Order),
        new(WorkSites.MinocTown, GuildType.Order),
        new(WorkSites.SkaraTown, GuildType.Order),
        new(WorkSites.JhelomTown, GuildType.Order),
        new(WorkSites.CoveTown, GuildType.Order),
        new(WorkSites.YewTown, GuildType.Chaos),
        new(WorkSites.TrinsicTown, GuildType.Chaos),
        new(WorkSites.MoonglowTown, GuildType.Chaos),
        new(WorkSites.MaginciaTown, GuildType.Chaos),
        new(WorkSites.VesperTown, GuildType.Chaos)
    ];

    /// <summary>
    /// The sides of a map's own bank towns (<see cref="WorldAnchors"/>): Order and Chaos in
    /// turn, in the catalog's order, so each side has towns; none below <see cref="MinSidedTowns"/>.
    /// </summary>
    public static IReadOnlyList<TownSide> InTurn(IReadOnlyList<WorkSite> towns)
    {
        var sides = new List<TownSide>();

        if (towns == null || towns.Count < MinSidedTowns)
        {
            return sides;
        }

        for (var i = 0; i < towns.Count; i++)
        {
            sides.Add(new TownSide(towns[i].Home, i % 2 == 0 ? GuildType.Order : GuildType.Chaos));
        }

        return sides;
    }

    /// <summary>The side whose town holds this home, or Regular for a home in no town (the Den, a mine ledge).</summary>
    public static GuildType SideOf(FacetAnchors anchors, Point3D home)
    {
        var side = GuildType.Regular;
        var nearest = TownReach + 1;
        var towns = anchors.TownSides;

        for (var i = 0; i < towns.Count; i++)
        {
            var distance = NavMetric.Chebyshev(home, towns[i].Bank);

            if (distance < nearest)
            {
                nearest = distance;
                side = towns[i].Side;
            }
        }

        return side;
    }

    /// <summary>True when a person of this side may call this home its own.</summary>
    public static bool Allows(FacetAnchors anchors, GuildType side, Point3D home)
    {
        if (side is not (GuildType.Order or GuildType.Chaos))
        {
            return true;
        }

        var town = SideOf(anchors, home);
        return town == GuildType.Regular || town == side;
    }

    /// <summary>
    /// The side a person's home holds it to. A fixture of a stock map keeps its authored home,
    /// so its side follows that town: a Chaos fixture at the Britain bank opened a fresh world
    /// with a fight. A home the tables give (<see cref="WorkSites.HomedByTable"/>) follows its
    /// side, so it is held to none.
    /// </summary>
    public static GuildType HomeSide(string uniqueId, Point3D authored) =>
        WorkSites.HomedByTable(uniqueId) ? GuildType.Regular : SideOf(WorldAnchors.ForId(uniqueId), authored);

    /// <summary>
    /// The sites a person of this side may call home, in list order. A list with no town of
    /// the side gives that side's bank towns instead, and a map with no town of the side gives
    /// the list as it is.
    /// </summary>
    public static IReadOnlyList<WorkSite> For(FacetAnchors anchors, GuildType side, IReadOnlyList<WorkSite> sites)
    {
        if (side is not (GuildType.Order or GuildType.Chaos) || sites == null)
        {
            return sites;
        }

        var own = Allowed(anchors, side, sites);

        if (own.Count > 0)
        {
            return own;
        }

        var towns = Allowed(anchors, side, anchors.Towns);
        return towns.Count > 0 ? towns : sites;
    }

    private static List<WorkSite> Allowed(FacetAnchors anchors, GuildType side, IReadOnlyList<WorkSite> sites)
    {
        var own = new List<WorkSite>();

        for (var i = 0; i < sites.Count; i++)
        {
            if (Allows(anchors, side, sites[i].Home))
            {
                own.Add(sites[i]);
            }
        }

        return own;
    }
}
