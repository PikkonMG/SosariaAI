using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Spawning;

/// <summary>
/// Copies of a roster template must not stack on the same tile.
/// Fixtures (no # in the unique id) keep the authored spawn.
/// </summary>
public static class SpawnSpread
{
    public const int DefaultRadius = 32;
    public const int MaxRadius = 96;
    public const int CornerMaxRadius = 64;

    /// <summary>Home anchors a facet's copies share: the town bank table plus the work sites.</summary>
    public const int SiteEstimate = 25;

    /// <summary>Tiles of reach each extra sqrt of the crowd buys.</summary>
    public const int RadiusStep = 6;

    /// <summary>
    /// Entries in the facet plan being bound right now. The spawner sets it before
    /// each bind or spawn so crowd math survives the deferred pump slices.
    /// </summary>
    public static int PlannedCount;

    /// <summary>Copies expected to share one home anchor.</summary>
    public static int CrowdPerSite => Math.Max(1, PlannedCount / SiteEstimate);

    /// <summary>
    /// The box grows with the crowd a site holds. Thirty-two tiles fit a dozen
    /// copies; a hundred need a small district, not a doorstep.
    /// </summary>
    public static int RadiusFor(int crowd, int min = DefaultRadius, int max = MaxRadius) =>
        Math.Clamp((int)Math.Ceiling(Math.Sqrt(Math.Max(1, crowd))) * RadiusStep, min, max);

    public static int RadiusFor(int crowd) => RadiusFor(crowd, DefaultRadius, MaxRadius);

    /// <summary>
    /// The spots a spawn tries in turn: its scattered spot, the same spot mirrored across the
    /// site, then the site itself. A scatter into the river west of the Britain bank found no
    /// land within reach and stood the person on the bank tile; the mirrored spot east of the
    /// bank is a street. A fixture, never scattered, tries its site once.
    /// </summary>
    public static IReadOnlyList<Point3D> Tries(Point3D site, Point3D scattered) =>
        scattered == site ? [site] : [scattered, Mirror(site, scattered), site];

    /// <summary>The spot on the far side of the site, as far off as <paramref name="spot"/>.</summary>
    public static Point3D Mirror(Point3D site, Point3D spot) =>
        new(site.X * 2 - spot.X, site.Y * 2 - spot.Y, spot.Z);

    public static Point3D Offset(Point3D spawn, string uniqueId) =>
        Offset(spawn, uniqueId, RadiusFor(CrowdPerSite));

    public static Point3D Offset(Point3D spawn, string uniqueId, int radius)
    {
        if (string.IsNullOrWhiteSpace(uniqueId) || radius <= 0)
        {
            return spawn;
        }

        var mark = uniqueId.IndexOf(SpawnPlan.DuplicateMark);

        if (mark < 0)
        {
            return spawn;
        }

        var hash = WorkSites.StableIndex(uniqueId);
        var span = radius * 2 + 1;
        var dx = ((hash % span) + span) % span - radius;
        var dy = (((hash / 17) % span) + span) % span - radius;

        if (dx == 0 && dy == 0)
        {
            dx = 1;
        }

        return new Point3D(spawn.X + dx, spawn.Y + dy, spawn.Z);
    }
}
