using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Each character has a home town and a bank spot. The leash is a soft pull: idling
/// stays near home, and a person far from home with no trip to make heads back. It
/// never stops a trip a job makes to another town, a dungeon, or a hunt ground.
/// </summary>
public static class HomeLeash
{
    public const int FailedWalksBeforeMoongate = 3;

    /// <summary>
    /// Stalls within this many tiles of the last one count as the same spot. A stalled
    /// walker who moved between failures is on bad ground ahead, not sealed in.
    /// </summary>
    public const int MaroonedStallRadius = 4;

    /// <summary>
    /// How often a character may re-check whether its tile is sealed after stalls.
    /// The scan is bounded but not free; a walker who keeps stalling retries the
    /// proof on this clock, not on every stall.
    /// </summary>
    public static readonly TimeSpan MaroonedCheckCooldown = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Travel stalls clustered on one spot before the character checks whether the spot is
    /// sealed. Failed home walks keep their own count, <see cref="FailedWalksBeforeMoongate"/>.
    /// </summary>
    public const int StallsBeforeMaroonedCheck = 3;

    /// <summary>
    /// The same-spot stall count after one more stall at <paramref name="at"/>. A stall
    /// away from the last one starts the count again.
    /// </summary>
    public static int SameSpotStalls(int stallsSoFar, Point3D at, Point3D lastStallAt) =>
        NavMetric.Chebyshev(at, lastStallAt) <= MaroonedStallRadius ? stallsSoFar + 1 : 1;

    /// <summary>True when enough stalls cluster on one spot and the last sealed check has cooled down.</summary>
    public static bool MayCheckMarooned(int sameSpotStalls, DateTime now, DateTime nextCheckAt) =>
        sameSpotStalls >= StallsBeforeMaroonedCheck && now >= nextCheckAt;

    /// <summary>A road node this close to the side step round a danger stands in for it.</summary>
    public const int BypassSnapTiles = 16;

    /// <summary>True when the road node is near enough to the side step to walk to instead.</summary>
    public static bool SnapsBypass(Point3D bypass, Point3D node) =>
        NavMetric.Chebyshev(bypass, node) <= BypassSnapTiles;

    /// <summary>
    /// Where a person stands about: its idle centre within the leash, else where it stands.
    /// A traveller idles in the town it came to see; walking it to its idle centre at home
    /// in a straight line was the old pile in the sea.
    /// </summary>
    public static Point3D StandCenter(Point3D location, Point3D idleCenter, int leashRadius) =>
        MayIdleAt(location, idleCenter, leashRadius) ? idleCenter : location;

    public static int ConfiguredRadius() =>
        SosariaSettings.Characters?.Career?.LeashRadius ?? CareerSettings.DefaultLeashRadius;

    public static bool BeyondLeash(Point3D location, Point3D homeSpot, int leashRadius)
    {
        var reach = leashRadius > 0 ? leashRadius : CareerSettings.DefaultLeashRadius;
        return homeSpot != Point3D.Zero && NavMetric.Chebyshev(location, homeSpot) > reach;
    }

    /// <summary>
    /// Where a character idles. Every authored idle centre is a Britain tile, most of them
    /// in front of the bank; a copy that lives in Moonglow got "home = Britain" and the
    /// engine walked it straight toward Britain until it stood in the sea with thirty
    /// others, and every Britain copy of the thirteen templates idled at the bank. A copy
    /// idles at its own corner of town; only the fixture keeps its authored centre, and a
    /// centre beyond the leash of the home spot is replaced by that home spot.
    /// </summary>
    public static Point3D IdleCenter(Point3D authored, Point3D homeSpot, int leashRadius, bool isCopy)
    {
        if (authored == Point3D.Zero || isCopy && homeSpot != Point3D.Zero)
        {
            return homeSpot;
        }

        return BeyondLeash(authored, homeSpot, leashRadius) ? homeSpot : authored;
    }

    /// <summary>
    /// A character far from its idle centre must not idle: the engine's wander walks it
    /// there in a straight line. It goes home by the road first.
    /// </summary>
    public static bool MayIdleAt(Point3D location, Point3D center, int leashRadius) =>
        !BeyondLeash(location, center, leashRadius);

    public static int DistanceFromHome(Point3D location, Point3D homeSpot) =>
        homeSpot == Point3D.Zero ? 0 : NavMetric.Chebyshev(location, homeSpot);

    public static string CannotFindHomeLine(string name, Point3D from) =>
        $"{name} cannot find a way home from ({from.X},{from.Y})";
}
