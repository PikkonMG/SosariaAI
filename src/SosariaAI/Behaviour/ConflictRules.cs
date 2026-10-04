using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Behaviour;

/// <summary>Where a character goes to seek player conflict. Pure.</summary>
public static class ConflictRules
{
    /// <summary>A murder is answered only this long: then the killer is long gone.</summary>
    public static readonly TimeSpan AnswerAge = TimeSpan.FromMinutes(10);

    /// <summary>A murder draws at most this many blues: the first to hear of it go, the rest keep their day.</summary>
    public const int MaxAnswerers = 3;

    /// <summary>
    /// A blue answers a fresh murder (<see cref="AnswerAge"/>) that fewer than
    /// <see cref="MaxAnswerers"/> ride to already, reported anywhere but Buccaneer's Den. A
    /// report was answered for three hours by every lawful fighter within 400 tiles: "seek player
    /// conflict where it happened" was the shard's top pick, 4,111 in 86 minutes, and 33 of 957
    /// fighters stood in a dungeon. The Den is the reds' own town, where somebody is always
    /// dying: one anti-PK walked to "where it happened" there 62 times in an hour and never left.
    /// Blues go to the Den only on a raid, a posse or a PK hunter's run (<see cref="PartyRoads"/>).
    /// </summary>
    public static bool BlueAnswers(ShardEvent report, DateTime now) =>
        report != null && !PkRules.InBuccaneersDen(report.X, report.Y) &&
        now - report.At <= AnswerAge && report.Answerers < MaxAnswerers;

    /// <summary>
    /// A blue rides only to a murder it can get to: fresh, not answered enough, out of the Den (<see cref="BlueAnswers"/>),
    /// inside its leash from where it stands, not at a spot its router just proved it cannot
    /// reach, and not where it just ran from a threat (<see cref="DangerSpots"/>). Minoc tamers
    /// chose a report in the hills with no road near it, failed at the start, and chose it
    /// again: 160 outings in 150 minutes never began. Magincia blues rode back to the murder
    /// the red still stood at, ran home from it, and rode back: six times in fifteen minutes.
    /// </summary>
    public static bool BlueRides(
        ShardEvent report,
        Point3D from,
        int leashRadius,
        IReadOnlyList<Point3D> unreachable,
        IReadOnlyList<Point3D> fledFrom,
        DateTime now
    )
    {
        if (!BlueAnswers(report, now))
        {
            return false;
        }

        var spot = SpotOf(report);

        if (HomeLeash.BeyondLeash(spot, from, leashRadius) || NavSearch.IsNearAny(spot, fledFrom))
        {
            return false;
        }

        for (var i = 0; i < (unreachable?.Count ?? 0); i++)
        {
            if (NavMetric.Chebyshev(unreachable[i], spot) <= SpotMemory.SameSpotTiles)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Where the murder in a report happened.</summary>
    public static Point3D SpotOf(ShardEvent report) =>
        report == null ? Point3D.Zero : new Point3D(report.X, report.Y, report.Z);

    public static Point3D PickTraffic(DestinationCatalog catalog, Point3D from, int seed)
    {
        if (catalog == null)
        {
            return Point3D.Zero;
        }

        var hunts = catalog.NearestFirst("graveyard", from, 3);
        var dungeons = catalog.NearestFirst("despise", from, 3);
        var useDungeon = (seed & 1) == 0;
        var first = useDungeon ? First(dungeons) : First(hunts);
        var second = useDungeon ? First(hunts) : First(dungeons);
        return first ?? second ?? Point3D.Zero;
    }

    private static Point3D? First(IReadOnlyList<Destination> destinations) =>
        destinations is { Count: > 0 } ? destinations[0].Arrival : null;

    /// <summary>
    /// What a conflict outing is for, in words its chat can use, or null when nothing names it:
    /// a red's run names its hot spot and its phase, a PK hunter hunts reds, a lawful fighter
    /// rides to a murder. The bare "seek player conflict" left the chat to guess, and a red
    /// camping Destard asked a player along to the town its long goal named.
    /// </summary>
    public static string DoingPhrase(
        string spot,
        GangRunPhase phase,
        bool lurking,
        bool huntsReds,
        bool huntsDen,
        bool answersReport)
    {
        if (huntsReds)
        {
            return huntsDen ? "hunt reds in Buccaneer's Den" : "hunt reds at their camps";
        }

        if (!string.IsNullOrEmpty(spot))
        {
            return phase switch
            {
                GangRunPhase.Muster => $"gather the gang to ride to {spot} and kill players",
                GangRunPhase.Work when lurking => $"lie in wait at {spot} to kill players",
                GangRunPhase.Work => $"patrol {spot} to kill players",
                _ => $"ride with the gang to {spot} to kill players"
            };
        }

        return answersReport ? "ride to a fresh murder to catch the killer" : null;
    }
}
