using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// How a person crawls a dungeon: room by room across a floor, down a stair as often as its
/// skill dares, camping one room for a long stretch when that is its way, and back out when
/// the run has lasted long enough, the wounds are bad, the pack is full or the supplies are
/// gone. Each next stop is rolled by weight: a fresh room weighs one, a room already cleared a sliver, so a swept floor still
/// offers something, a room with prey in reach many times more, and the stair down weighs by
/// the crawler's tier, so the crowd thins with depth. A run is a long session, twenty minutes to three quarters of an hour, and a
/// camper stays about twice that. Rooms and stairs come from the generated floors, never
/// from a hand-made list.
///
/// A stair down counts only when the floor below fits the crawler's power, with its party and
/// pets (<see cref="FloorFits"/>), and the crawler does not remember that floor as too hard.
/// A bare floor whose only stairs down lead too deep sends the crawler up a stair to a floor
/// that fits, or out. A crawler losing on its floor (<see cref="LosingWhy"/>) remembers the
/// floor as too hard for <see cref="TooHardRest"/> and goes up or out. Robard Hawke, power 191,
/// was carried down Shame's bare floors by the stair roll to the blood elementals of its
/// fourth level. Pure. No world objects.
/// </summary>
public static class DungeonCrawlRules
{
    /// <summary>Half the side of the square fought in round one room.</summary>
    public const int RoomRadius = 6;

    /// <summary>A room this close to one already cleared on the floor counts as cleared.</summary>
    public const int RoomMinSpacing = 10;

    /// <summary>A room within this walk of where the person stands weighs in full; one further off weighs <see cref="FarRoomShare"/> of that.</summary>
    public const int RoomMaxStep = 40;

    /// <summary>A fresh room's weight in the roll for the next stop.</summary>
    public const double RoomWeight = 1.0;

    /// <summary>
    /// A cleared room keeps this sliver of weight, so a fully swept floor still rolls
    /// something and the crawl sweeps forward instead of turning for home.
    /// </summary>
    public const double VisitedRoomWeight = 0.15;

    /// <summary>A room past <see cref="RoomMaxStep"/> weighs this share: the crawl works outward from where it stands.</summary>
    public const double FarRoomShare = 0.35;

    /// <summary>
    /// A room with prey in reach weighs this many times a bare one, fresh or cleared: a player
    /// walks toward the spawn it sees, not down an empty corridor. The Wrong crawl rolled the
    /// corridor points of its floor as often as the juka rooms, and two hunters met no juka.
    /// </summary>
    public const double PreyRoomFactor = 10.0;

    /// <summary>A room counts as having prey when some stand this close: what the room's hunt would see.</summary>
    public const int RoomPreyRange = RoomRadius + HuntSkill.ReachSlack;

    /// <summary>
    /// The stair down's weight by tier, novice to grandmaster, beside a fresh room's one: a
    /// novice rarely goes down, a grandmaster twice as readily as into any one room.
    /// </summary>
    public static readonly double[] DescendWeightByTier = [0.10, 0.20, 0.40, 0.70, 1.00, 1.50, 2.00];

    /// <summary>The roll's answer for the stair down.</summary>
    public const int Descend = -2;

    /// <summary>The roll's answer when the floor offers nothing: the crawl turns for home.</summary>
    public const int NoStop = -1;

    /// <summary>The roll's answer for a stair up to a floor that fits.</summary>
    public const int Ascend = -3;

    /// <summary>A crawler that ran this many times on one floor is losing there.</summary>
    public const int LosingRuns = 2;

    /// <summary>A floor a crawler lost on stays too hard for it this long; the save keeps it (<see cref="Behaviour.RuleClock.FloorTooHard"/>).</summary>
    public static readonly TimeSpan TooHardRest = TimeSpan.FromHours(2);

    /// <summary>The word a log line names a dungeon floor by.</summary>
    public const string FloorWord = "floor";

    public const int RoomMinutes = 2;

    /// <summary>
    /// A room with no prey in sight this long is left for the next. A player glances into an
    /// empty room and walks on; waiting out the whole room clock in the bare entrance hall of
    /// Trinsic Passage cost Kerensa two minutes a room, fourteen rooms, not one kill.
    /// </summary>
    public static readonly TimeSpan RoomEmptyLimit = TimeSpan.FromSeconds(30);

    /// <summary>
    /// This many rooms in a row with no prey and no kill make the floor bare: the crawl takes
    /// a stair down that fits when there is one. The prey of Trinsic Passage all lies past a
    /// stair from its entrance floor, and the crawl rolled that stair only by tier.
    /// </summary>
    public const int BareFloorRooms = 2;

    /// <summary>A room that failed this many times in a row ends the crawl: the floor is closed to this person.</summary>
    public const int MaxRoomFailures = 3;

    public const double LeaveHitsFraction = 0.35;

    public const double CampBase = 0.15;
    public const double CampHomebody = 0.2;
    public const double CampCautious = 0.15;
    public const double CampRestlessCut = 0.1;

    /// <summary>The shortest run, in minutes, before the person thinks of heading up.</summary>
    public const int RunMinMinutes = 20;

    /// <summary>The longest run, in minutes, for one who does not camp.</summary>
    public const int RunMaxMinutes = 45;

    /// <summary>A camper came to farm its hall: its run is this many runs long.</summary>
    public const int CamperRuns = 2;

    public const double RunRestless = 0.75;
    public const double RunHomebody = 1.25;
    public const double RunNeutral = 1.0;

    public const int CampSalt = 0xCA;
    public const int RoomSalt = 0x5E;
    public const int RunSalt = 0x9A;

    private const double BareRoomFactor = 1.0;
    private const double NoChance = 0;
    private const double Certain = 1;

    /// <summary>What the stair down weighs for a crawler of <paramref name="tier"/>.</summary>
    public static double DescendWeight(SkillTier tier) =>
        DescendWeightByTier[Math.Clamp((int)tier, 0, DescendWeightByTier.Length - 1)];

    public static double CampChance(PersonTrait traits)
    {
        var chance = CampBase;

        if ((traits & PersonTrait.Homebody) != 0)
        {
            chance += CampHomebody;
        }

        if ((traits & PersonTrait.Cautious) != 0)
        {
            chance += CampCautious;
        }

        if ((traits & PersonTrait.Restless) != 0)
        {
            chance -= CampRestlessCut;
        }

        return Math.Clamp(chance, NoChance, Certain);
    }

    public static bool ShouldCamp(PersonTrait traits, int seed) => ChoiceSeed.Unit(seed, CampSalt) < CampChance(traits);

    /// <summary>
    /// A camper farms its hall again when the hall's hunt ran out its clock with prey still
    /// about. A bare hall, bad wounds, a full pack or spent supplies end the camp: the run
    /// goes on across the floor, or out.
    /// </summary>
    public static bool CampsOn(HuntEndReason reason) => reason == HuntEndReason.TimeUp;

    /// <summary>
    /// A camper holds its hall only when the hall lies on the floor it stands on. One who
    /// logged back in on another floor, or in another dungeon, crawls where it is.
    /// </summary>
    public static bool CampsAtHall(bool campRoll, bool hallOnThisFloor) => campRoll && hallOnThisFloor;

    /// <summary>
    /// How long one run lasts before the person heads back up: a seeded stretch between
    /// <see cref="RunMinMinutes"/> and <see cref="RunMaxMinutes"/>, a restless one sooner, a
    /// homebody later, and a camper <see cref="CamperRuns"/> times as long.
    /// </summary>
    public static TimeSpan RunLength(PersonTrait traits, int seed, bool camper)
    {
        var scale = (traits & PersonTrait.Restless) != 0 ? RunRestless
            : (traits & PersonTrait.Homebody) != 0 ? RunHomebody
            : RunNeutral;
        var minutes = RunMinMinutes + ChoiceSeed.Unit(seed, RunSalt) * (RunMaxMinutes - RunMinMinutes);
        return TimeSpan.FromMinutes(minutes * scale * (camper ? CamperRuns : 1));
    }

    public const string RunDoneWhy = "the run is over";
    public const string WoundsWhy = "bad wounds";
    public const string SuppliesWhy = "its supplies ran low";
    public const string PackFullWhy = "a full pack";
    public const string RoomsFailedWhy = "no room could be reached";
    public const string FloorEmptyWhy = "the floor offers nothing more";
    public const string OffFloorWhy = "it stands on no mapped floor";
    public const string TooHardWhy = "the floor is too hard for it";
    public const string RunsWhy = "it keeps running from the fights here";
    public const string PetLostWhy = "it lost a pet here";
    public const string MateLostWhy = "it lost a party mate here";

    /// <summary>
    /// A floor fits a crawler of <paramref name="power"/> when its difficulty lies within the
    /// power's reach (<see cref="HuntGround.Reach"/>). A floor with no spawn fits anyone.
    /// </summary>
    public static bool FloorFits(int difficulty, int power) => difficulty <= HuntGround.Reach(power);

    /// <summary>
    /// A crawler stays on its floor while the floor lies within this share of its reach: a
    /// floor it took by <see cref="FloorFits"/> is not left for the mana a few spells spent.
    /// </summary>
    public const double StaySlack = 1.2;

    /// <summary>
    /// True while a crawler of <paramref name="power"/> may stay on a floor of
    /// <paramref name="difficulty"/>. A pad that dropped it on a floor far past its reach, as
    /// Destard's third level is from Fire and Despise, is left at once.
    /// </summary>
    public static bool StaysOn(int difficulty, int power) => difficulty <= HuntGround.Reach(power) * StaySlack;

    /// <summary>True while a floor marked too hard at <paramref name="markedAt"/> is still too hard.</summary>
    public static bool StillTooHard(DateTime markedAt, DateTime now) =>
        markedAt != default && now >= markedAt && now - markedAt < TooHardRest;

    /// <summary>
    /// Why a crawler is losing on its floor, or null while it holds: it ran from fights here
    /// <see cref="LosingRuns"/> times, a pet or a party mate of its fell here, or it took bad
    /// wounds here (it came onto the floor above <see cref="LeaveHitsFraction"/> and is below it
    /// now; wounds from the road are not the floor's). Norbert Greymane, power 146, ran from
    /// Destard's drakes again and again and stayed.
    /// </summary>
    public static string LosingWhy(int runsHere, int petsLost, int matesLost, double hitsAtEntry, double hitsNow) =>
        runsHere >= LosingRuns ? RunsWhy
        : petsLost > 0 ? PetLostWhy
        : matesLost > 0 ? MateLostWhy
        : hitsNow < LeaveHitsFraction && hitsAtEntry >= LeaveHitsFraction ? WoundsWhy
        : null;

    /// <summary>A crawler leaving a floor goes up a stair that fits while its run and its wounds allow; else it heads out.</summary>
    public static bool GoesUp(bool upFits, string leaveReason, double hitsFraction) =>
        upFits && leaveReason == null && hitsFraction >= LeaveHitsFraction;

    /// <summary>The line a crawler writes when it takes a floor: where, and its power against the floor's difficulty.</summary>
    public static string FitLine(string name, string verb, string dungeon, int level, int power, int difficulty) =>
        $"{name} {verb} {dungeon} level {level}{HuntGround.FitNote(power, difficulty, FloorWord)}";

    /// <summary>The line a crawler writes when it leaves a floor for another way: where, why, and its power against the floor.</summary>
    public static string KeepOffLine(string name, string dungeon, int level, int power, int difficulty, string why) =>
        $"{name} keeps off {dungeon} level {level}{HuntGround.FitNote(power, difficulty, FloorWord)}: {why}";

    /// <summary>
    /// Why the run is over, or null while it goes on: its time, bad wounds, spent supplies,
    /// a full pack, or rooms that failed in a row. A live run left most dungeons after two to
    /// eleven minutes of a twenty-minute run and said nothing of why. <paramref name="suppliesLow"/>
    /// is supplies that ran low during this crawl (<see cref="HuntEndDecision.RanLowOnRun"/>): a
    /// crawl that came in low fights on with what it has, and leaves on its wounds as any other.
    /// </summary>
    public static string LeaveReason(
        TimeSpan elapsed,
        TimeSpan runLength,
        double hitsFraction,
        bool suppliesLow,
        bool packFull,
        int roomFailures
    ) =>
        elapsed >= runLength ? RunDoneWhy
        : hitsFraction < LeaveHitsFraction ? WoundsWhy
        : suppliesLow ? SuppliesWhy
        : packFull ? PackFullWhy
        : roomFailures >= MaxRoomFailures ? RoomsFailedWhy
        : null;

    /// <summary>
    /// A crawl that reached no room at all and closed on failed rooms failed the trip. A
    /// fighter on a ledge of Trinsic Passage with no walk off it ended twenty-one runs as
    /// done in two minutes, so the failures never rested the job.
    /// </summary>
    public static bool CrawlFailed(int roomsReached, string leaveReason) =>
        roomsReached == 0 && leaveReason == RoomsFailedWhy;

    /// <summary>The line a run that turns for the surface writes, easy to count by reason.</summary>
    public static string LeaveLine(string name, string dungeon, TimeSpan inside, string why) =>
        $"{name} leaves {dungeon} after {(int)inside.TotalMinutes} minutes: {why}";

    /// <summary>Rooms in a row that gave nothing: one more after a room with no prey and no kill, none after any other.</summary>
    public static int CountBareRooms(int before, HuntEndReason roomEnd, int roomKills) =>
        roomEnd == HuntEndReason.Empty && roomKills == 0 ? before + 1 : 0;

    public static bool FloorIsBare(int bareRooms) => bareRooms >= BareFloorRooms;

    /// <summary>
    /// The next stop on the floor: the index of a room, <see cref="Descend"/> for a stair down
    /// that fits (<see cref="FloorWays.Down"/>), <see cref="Ascend"/> for a stair up, or
    /// <see cref="NoStop"/> when the floor offers nothing. A bare floor with no prey in reach of
    /// any room goes down a stair that fits; when every stair down leads too deep
    /// (<see cref="FloorWays.DownBarred"/>) it goes up a stair that fits, or out. Otherwise fresh
    /// rooms weigh <see cref="RoomWeight"/>, cleared ones <see cref="VisitedRoomWeight"/>, rooms
    /// with prey (<paramref name="roomHasPrey"/>, by index; a missing entry is no prey)
    /// <see cref="PreyRoomFactor"/> times that, far ones a share of it, and a stair down that
    /// fits <see cref="DescendWeight"/>; the seed rolls among them.
    /// </summary>
    public static int NextStop(
        Point3D at,
        IReadOnlyList<Point3D> rooms,
        IReadOnlyList<Point3D> cleared,
        IReadOnlyList<bool> roomHasPrey,
        FloorWays ways,
        bool floorBare,
        SkillTier tier,
        int seed
    )
    {
        var count = rooms?.Count ?? 0;

        if (floorBare && !AnyPrey(roomHasPrey, count))
        {
            if (ways.Down)
            {
                return Descend;
            }

            if (ways.DownBarred)
            {
                return ways.Up ? Ascend : NoStop;
            }
        }

        var weights = new double[count + 1];

        for (var i = 0; i < count; i++)
        {
            var weight = (FarFromCleared(rooms[i], cleared) ? RoomWeight : VisitedRoomWeight) *
                         (HasPrey(roomHasPrey, i) ? PreyRoomFactor : BareRoomFactor);
            weights[i] = NavMetric.Chebyshev(at, rooms[i]) <= RoomMaxStep ? weight : weight * FarRoomShare;
        }

        weights[count] = ways.Down ? DescendWeight(tier) : 0;
        var pick = WeightedChoice.Index(weights, ChoiceSeed.Unit(seed, RoomSalt));
        return pick < 0 ? NoStop : pick == count ? Descend : pick;
    }

    public static Rectangle2D RoomArea(Point3D room) =>
        new(room.X - RoomRadius, room.Y - RoomRadius, RoomRadius * 2 + 1, RoomRadius * 2 + 1);

    private static bool HasPrey(IReadOnlyList<bool> roomHasPrey, int room) =>
        roomHasPrey != null && room < roomHasPrey.Count && roomHasPrey[room];

    private static bool AnyPrey(IReadOnlyList<bool> roomHasPrey, int rooms)
    {
        for (var i = 0; i < rooms; i++)
        {
            if (HasPrey(roomHasPrey, i))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FarFromCleared(Point3D room, IReadOnlyList<Point3D> cleared)
    {
        if (cleared == null)
        {
            return true;
        }

        for (var i = 0; i < cleared.Count; i++)
        {
            if (NavMetric.Chebyshev(room, cleared[i]) < RoomMinSpacing)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// The stairs a crawler may take from its floor: down to a floor that fits it, whether stairs
/// down exist that all lead too deep for it, and up to a floor that fits it.
/// </summary>
public readonly record struct FloorWays(bool Down, bool DownBarred, bool Up);
