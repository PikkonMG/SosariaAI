using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>A group that walks the roads together.</summary>
public enum RoadGroupKind
{
    /// <summary>Guildmates walking one long trip with the one who set out.</summary>
    Convoy,

    /// <summary>An Order or Chaos squad riding out to a meeting spot to find the other side.</summary>
    WarBand,

    /// <summary>Blue fighters riding out to clear the reds off a PvP hot spot.</summary>
    Sweep,

    /// <summary>A band of blue fighters riding into Buccaneer's Den to fight the reds on their own ground.</summary>
    DenRaid,

    /// <summary>A murdered blue's friends and the PK hunters near, riding after its killer.</summary>
    Posse
}

/// <summary>How the last Den raid ended, as the next band hears of it.</summary>
public enum DenRaidOutcome
{
    NoneYet,
    CameHomeWhole,
    LostFighters
}

/// <summary>
/// Guild convoys, faction war bands and anti-PK sweeps, the groups the 1999 roads were full
/// of: a guilded traveler, or a guilded person standing about who proposes the walk, takes one
/// to three free guildmates along a long trip to another town's bank, an Order or Chaos
/// fighter takes one to three free faction-mates out to a meeting spot, often the one an enemy
/// band already rides for, and a lawful fighter takes one to three friends out to a hot spot
/// the reds camp. Now and then a lawful fighter takes two or three friends into Buccaneer's
/// Den itself: blues rode into the reds' town to hunt them there, a planned raid, never a walk
/// to a murder report (see <see cref="ConflictRules.BlueAnswers"/>). A raid takes only fighters
/// strong enough for the Den. When a blue is murdered, its friends and the PK hunters in call
/// ride after the killer as a posse, into the Den too. The call carries over a town and its
/// roads; the leader waits while the mates walk over, then all set out together.
/// Each kind forms shard-wide now and then, up to a cap that grows with the people online (one
/// Den raid at a time), and breaks up at the end of its trip or its time. Pure.
/// </summary>
public static class PartyRoadRules
{
    /// <summary>Free mates this close hear the one setting out say it aloud; the rest hear it in guild chat.</summary>
    public const int RecruitRange = 30;

    /// <summary>
    /// A free mate this close hears the call and walks over. With the guilds spread thin, a
    /// guild has a handful online over a town and its roads; asked only within speech range,
    /// a live run of 34 minutes formed no convoy and no war band.
    /// </summary>
    public const int CallRange = 150;

    /// <summary>
    /// A war band's call carries this far: the home leash, a few minutes' run. The sides are
    /// spread over the whole land; within <see cref="CallRange"/> a live run of 48 minutes formed
    /// no war band. A mate from farther would trail the band for its whole life.
    /// </summary>
    public const int WarBandCallRange = 400;

    /// <summary>The leader waits this long at most for the mates walking over, then sets out with those there.</summary>
    public static readonly TimeSpan MusterLimit = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A player's "me" joins a road group this long after its call: while the mates still walk
    /// over. The call asks "anyone coming?" aloud, and a player who answered got nothing.
    /// </summary>
    public static readonly TimeSpan PlayerJoinWindow = MusterLimit;

    public const int MinMates = 1;
    public const int MaxMates = 3;

    /// <summary>
    /// A Den raid rides with at least this many mates, a band of three or four: the Den is full
    /// of reds between runs, and a pair that rode in would only feed them.
    /// </summary>
    public const int DenRaidMinMates = 2;

    /// <summary>One Den raid is out at a time, however many people are online.</summary>
    public const int MaxDenRaids = 1;

    /// <summary>
    /// A Den raid's leader and mates each fight with at least this power: about a red's own.
    /// Raids went out led by a tamer of power 59 and a novice warrior, and never reached the Den.
    /// </summary>
    public const int DenRaidMinPower = 150;

    /// <summary>A posse may ride with no mate at all: one friend avenging another.</summary>
    public const int PosseMinMates = 0;

    /// <summary>At most this many posses ride at once, however many people are online.</summary>
    public const int MaxPosses = 3;

    /// <summary>A convoy walks a real trip: not two streets, not a crossing of the whole land.</summary>
    public const int ConvoyMinTrip = 80;

    public const int ConvoyMaxTrip = 600;

    public const int PopulationPerConvoy = 200;
    public const int MinConvoys = 3;
    public const int PopulationPerWarBand = 400;
    public const int MinWarBands = 2;
    public const int PopulationPerSweep = 300;
    public const int MinSweeps = 1;

    /// <summary>A new war band rides for an enemy band's spot this often when one is out.</summary>
    public const int InterceptPercent = 65;

    public const int PercentScale = 100;

    public static readonly TimeSpan ConvoyGapMin = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan ConvoyGapMax = TimeSpan.FromMinutes(7);
    public static readonly TimeSpan WarBandGapMin = TimeSpan.FromMinutes(6);
    public static readonly TimeSpan WarBandGapMax = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan SweepGapMin = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan SweepGapMax = TimeSpan.FromMinutes(12);

    /// <summary>About two Den raids an hour shard-wide: the Den sees blues now and then, not all day.</summary>
    public static readonly TimeSpan DenRaidGapMin = TimeSpan.FromMinutes(20);

    public static readonly TimeSpan DenRaidGapMax = TimeSpan.FromMinutes(40);

    /// <summary>
    /// A fighter that rode on a Den raid, as leader or mate, rides on no other for this long:
    /// the raids go out with fresh faces, not the same anti-PK every half hour.
    /// </summary>
    public static readonly TimeSpan DenRaidRest = TimeSpan.FromHours(3);

    /// <summary>A convoy that has not arrived in this time breaks up on the road.</summary>
    public static readonly TimeSpan ConvoyLife = TimeSpan.FromMinutes(30);

    /// <summary>A war band that has not met the enemy in this time rides home.</summary>
    public static readonly TimeSpan WarBandLife = TimeSpan.FromMinutes(25);

    /// <summary>A sweep that found no red in this time rides home.</summary>
    public static readonly TimeSpan SweepLife = TimeSpan.FromMinutes(20);

    /// <summary>A Den raid that has not come home in this time breaks up where it stands.</summary>
    public static readonly TimeSpan DenRaidLife = TimeSpan.FromMinutes(30);

    /// <summary>A posse that has not found its killer in this time gives up the chase.</summary>
    public static readonly TimeSpan PosseLife = FactionRules.TrackTime;

    /// <summary>
    /// A leader whose call found no free mate calls again after this long. Every idle fighter
    /// read 150 tiles of mobiles, and every Order or Chaos one every guild, each second.
    /// </summary>
    public static readonly TimeSpan LookRetry = TimeSpan.FromSeconds(30);

    /// <summary>A leader whose last call found no one may call again at <paramref name="nextLookAt"/>.</summary>
    public static bool LookDue(DateTime nextLookAt, DateTime now) => now >= nextLookAt;

    /// <summary>
    /// A leader taken off the world, on the internal map or gone, never scans again to
    /// break its group up; its mates stood frozen, following a leader on no map.
    /// </summary>
    public static bool LeaderGone(bool leaderFound, bool onInternal) => !leaderFound || onInternal;

    /// <summary>How many groups of a kind may be out at once for this many people online.</summary>
    public static int Cap(RoadGroupKind kind, int population) =>
        kind switch
        {
            RoadGroupKind.Convoy => Math.Max(MinConvoys, Math.Max(0, population) / PopulationPerConvoy),
            RoadGroupKind.WarBand => Math.Max(MinWarBands, Math.Max(0, population) / PopulationPerWarBand),
            RoadGroupKind.Sweep => Math.Max(MinSweeps, Math.Max(0, population) / PopulationPerSweep),
            RoadGroupKind.Posse => MaxPosses,
            _ => MaxDenRaids
        };

    /// <summary>The fewest free mates a group of a kind sets out with.</summary>
    public static int MinMatesOf(RoadGroupKind kind) =>
        kind switch
        {
            RoadGroupKind.DenRaid => DenRaidMinMates,
            RoadGroupKind.Posse => PosseMinMates,
            _ => MinMates
        };

    /// <summary>A fighter of this power may ride on a Den raid (<see cref="DenRaidMinPower"/>).</summary>
    public static bool StrongEnoughForDen(int power) => power >= DenRaidMinPower;

    /// <summary>Another group of a kind may form while fewer than the cap are out.</summary>
    public static bool MayForm(RoadGroupKind kind, int groupsOut, int population) => groupsOut < Cap(kind, population);

    /// <summary>A trip this many tiles long is one guildmates walk together.</summary>
    public static bool ConvoyTrip(int tiles) => tiles >= ConvoyMinTrip && tiles <= ConvoyMaxTrip;

    /// <summary>A free mate this many tiles away hears the call of a group of <paramref name="kind"/>.</summary>
    public static bool HearsCall(RoadGroupKind kind, int tiles) =>
        tiles >= 0 && tiles <= (kind == RoadGroupKind.WarBand ? WarBandCallRange : CallRange);

    /// <summary>A mate this many tiles away hears the call spoken aloud.</summary>
    public static bool WithinSpeech(int tiles) => tiles >= 0 && tiles <= RecruitRange;

    /// <summary>A player's "me" still answers a call made at <paramref name="calledAt"/> (<see cref="PlayerJoinWindow"/>).</summary>
    public static bool PlayerJoinOpen(DateTime calledAt, DateTime now) =>
        now >= calledAt && now - calledAt <= PlayerJoinWindow;

    /// <summary>
    /// A player of the group's side may join it: a guildmate a convoy, one of the same Order or
    /// Chaos side a war band, a blue the anti-PK bands and the posse.
    /// </summary>
    public static bool PlayerFitsSide(RoadGroupKind kind, bool guildmate, bool sameFactionSide, bool lawful) =>
        kind switch
        {
            RoadGroupKind.Convoy => guildmate,
            RoadGroupKind.WarBand => sameFactionSide,
            _ => lawful
        };

    /// <summary>
    /// The bank a guilded person standing about proposes the guild walk to: one in another town
    /// a real trip away (see <see cref="ConvoyTrip"/>), not near a place it ran from, picked by
    /// the roll. Null when no bank fits.
    /// </summary>
    public static Destination ConvoyBank(
        IReadOnlyList<Destination> places,
        Point3D from,
        IReadOnlyList<Point3D> dangerSpots,
        int roll
    )
    {
        if (places == null)
        {
            return null;
        }

        var banks = new List<Destination>();

        for (var i = 0; i < places.Count; i++)
        {
            var place = places[i];

            if (place != null &&
                string.Equals(place.Kind, TownTripRules.BankKind, StringComparison.OrdinalIgnoreCase) &&
                place.Arrival != Point3D.Zero &&
                ConvoyTrip(NavMetric.Chebyshev(from, place.Arrival)) &&
                !NavSearch.IsNearAny(place.Arrival, dangerSpots))
            {
                banks.Add(place);
            }
        }

        return banks.Count == 0 ? null : banks[(roll % banks.Count + banks.Count) % banks.Count];
    }

    /// <summary>
    /// How many of the free mates come along: from the kind's fewest (see <see cref="MinMatesOf"/>)
    /// to three, as the roll says, never more than there are. A posse takes every friend who
    /// answers, up to three.
    /// </summary>
    public static int MatesToTake(RoadGroupKind kind, int available, int roll)
    {
        var fewest = kind == RoadGroupKind.Posse ? MaxMates : MinMatesOf(kind);
        var span = MaxMates - fewest + 1;
        return Math.Min(Math.Max(0, available), fewest + (roll % span + span) % span);
    }

    /// <summary>A Den raid that broke up with any rider dead lost fighters; else it came home whole.</summary>
    public static DenRaidOutcome RaidOutcome(int fallen) =>
        fallen > 0 ? DenRaidOutcome.LostFighters : DenRaidOutcome.CameHomeWhole;

    /// <summary>A new band rides for the enemy band's spot when one is out and the roll says so.</summary>
    public static bool Intercepts(bool enemyBandOut, int roll100) =>
        enemyBandOut && roll100 >= 0 && roll100 < InterceptPercent;

    /// <summary>The group has been out longer than its kind lasts.</summary>
    public static bool Expired(RoadGroupKind kind, DateTime formedAt, DateTime now) =>
        now - formedAt >= kind switch
        {
            RoadGroupKind.Convoy => ConvoyLife,
            RoadGroupKind.WarBand => WarBandLife,
            RoadGroupKind.Sweep => SweepLife,
            RoadGroupKind.Posse => PosseLife,
            _ => DenRaidLife
        };

    /// <summary>The shard-wide wait before the next group of a kind, from a roll between zero and one.</summary>
    public static TimeSpan Gap(RoadGroupKind kind, double roll01)
    {
        var (min, max) = kind switch
        {
            RoadGroupKind.Convoy => (ConvoyGapMin, ConvoyGapMax),
            RoadGroupKind.WarBand => (WarBandGapMin, WarBandGapMax),
            RoadGroupKind.Sweep => (SweepGapMin, SweepGapMax),
            _ => (DenRaidGapMin, DenRaidGapMax)
        };
        return min + (max - min) * Math.Clamp(roll01, 0, 1);
    }
}
