using System;
using SosariaAI.Combat;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>
/// Order against Chaos, and guild wars, as players fought them. Order and Chaos draw on sight
/// anywhere, the bank, the healer and the moongate included: the blows are lawful and the guards
/// stay out, as on the 1999 shards. A guild war keeps to the roads, away from a bank, a healer, a
/// moongate, a shrine and the town guards. Called street scuffles add to the town fights
/// (<see cref="TownScuffleRules"/>). A skirmish is at most four a side, ends when a fighter is
/// badly hurt or it runs long, and nobody goes straight back at the one who killed them or was
/// killed by them. Pure.
/// </summary>
public static class FactionRules
{
    /// <summary>At most four a side in one skirmish. Six Order members drew on one Chaos at the Britain bank.</summary>
    public const int MaxSide = 4;

    /// <summary>The mates a draw brings in beside the one who draws: the rest of its side.</summary>
    public const int MatesBesideDrawer = MaxSide - 1;

    /// <summary>A bank, healer, moongate or shrine this close is a place of peace for a guild war.</summary>
    public const int SafeRadius = 12;

    /// <summary>
    /// A fight starts only this far from a place of peace: its ring and the guard line's margin
    /// (<see cref="GuardLineRules.LineMarginTiles"/>). Drawn one tile past the ring, the first
    /// step took the fight in and stood it down: Osanna stopped three times in 21 seconds at the
    /// Yew moongate, each time against a new foe.
    /// </summary>
    public const int FightRoomRadius = SafeRadius + GuardLineRules.LineMarginTiles;

    /// <summary>Faction-mates this close to a skirmish are drafted in, and counted as its side.</summary>
    public const int DraftRange = 14;

    /// <summary>
    /// Order and Chaos draw on an enemy this far off: one screen. At the eight tiles of a
    /// party invite, two enemies on the same road walked past each other.
    /// </summary>
    public const int SightRange = 18;

    /// <summary>A patrol sets a course on an enemy this far off.</summary>
    public const int InterceptRange = 30;

    /// <summary>A patrol walks inside this radius of its spot.</summary>
    public const int SpotRadius = 6;

    /// <summary>A patrol closes to this range; the rally then draws.</summary>
    public const int EngageRange = 4;

    /// <summary>Below this share of its hits a fighter breaks off to heal.</summary>
    public const double DisengageHits = 0.35;

    /// <summary>A patrol keeps its spot this long, then goes home.</summary>
    public static readonly TimeSpan PatrolTime = TimeSpan.FromMinutes(6);

    /// <summary>A posse rides after its mark for this long; then it keeps to the ground it reached.</summary>
    public static readonly TimeSpan TrackTime = TimeSpan.FromMinutes(15);

    /// <summary>A posse's mark this far from where the posse rides turns the ride toward the mark.</summary>
    public const int TrackTiles = 20;

    /// <summary>A patrol looks around for an enemy this often.</summary>
    public static readonly TimeSpan PatrolScanGap = TimeSpan.FromSeconds(3);

    /// <summary>One war band at most in this time per person who leads one.</summary>
    public static readonly TimeSpan PatrolRest = TimeSpan.FromMinutes(30);

    /// <summary>The meeting spots of both sides change this often, so both sides go to the same ones.</summary>
    public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

    /// <summary>A skirmish that runs this long ends: both sides break off.</summary>
    public static readonly TimeSpan SkirmishLimit = TimeSpan.FromMinutes(3);

    /// <summary>A person trash-talks in town once in this time.</summary>
    public static readonly TimeSpan TauntRest = TimeSpan.FromMinutes(10);

    /// <summary>The same two people trade town words once in this time.</summary>
    public static readonly TimeSpan PairTauntRest = TimeSpan.FromMinutes(30);

    /// <summary>One person draws town words from one of the other side at a time, not from a whole crowd.</summary>
    public static readonly TimeSpan FoeTauntRest = TimeSpan.FromMinutes(2);

    /// <summary>One place hears town words once in this time: 449 jeers in 26 minutes filled the banks.</summary>
    public static readonly TimeSpan PlaceTauntRest = TimeSpan.FromMinutes(3);

    /// <summary>A killer and the one it killed leave each other alone this long.</summary>
    public static readonly TimeSpan RevengeRest = TimeSpan.FromMinutes(30);

    /// <summary>How long a fighter who broke off runs before it turns to healing.</summary>
    public static readonly TimeSpan DisengageRun = TimeSpan.FromSeconds(15);

    /// <summary>The number of meeting spots open at one time.</summary>
    public const int ActiveSpots = 2;

    /// <summary>
    /// A fight may start here: Order against Chaos anywhere, a guild war only with room on both
    /// sides (no guards, no bank, healer, moongate or shrine near). Order and Chaos stood side by
    /// side at the banks and only jeered: 96 jeers to 4 street scuffles in twenty minutes.
    /// </summary>
    public static bool MayFightAt(bool alignmentFoes, bool selfHasRoom, bool foeHasRoom) =>
        alignmentFoes || selfHasRoom && foeHasRoom;

    /// <summary>
    /// A place of peace this many tiles off is near: a bank inside <paramref name="bankRadius"/>,
    /// a healer, a shrine or a moongate inside <paramref name="otherRadius"/>.
    /// </summary>
    public static bool NearPeace(int distance, bool isBank, int bankRadius, int otherRadius) =>
        distance <= (isBank ? bankRadius : otherRadius);

    /// <summary>
    /// A fight starts only between two armed people: its combat kit and, for a build that
    /// carries one, a weapon in reach. A blue raised naked was drawn on again the minute its death
    /// grace ran out: 172 of 1,579 draws in one run fell on a person stripped of its gear, and it
    /// died with its fists up.
    /// </summary>
    public static bool MayFight(bool selfArmed, bool foeArmed) => selfArmed && foeArmed;

    /// <summary>A side with fewer than four in the skirmish may send one more.</summary>
    public static bool MayJoinSide(int sideFighters) => sideFighters < MaxSide;

    /// <summary>How many more of one side may be drafted into a skirmish.</summary>
    public static int DraftSlots(int sideFighters) => Math.Max(0, MaxSide - sideFighters);

    /// <summary>
    /// Town words: once per person per rest, once per pair per longer rest, once per foe at a
    /// time, and once per place in its rest.
    /// </summary>
    public static bool TauntDue(DateTime lastBySelf, DateTime lastByPair, DateTime lastOnFoe, DateTime lastInPlace, DateTime now) =>
        TimeRules.Rested(lastBySelf, now, TauntRest) && TimeRules.Rested(lastByPair, now, PairTauntRest) &&
        TimeRules.Rested(lastOnFoe, now, FoeTauntRest) && TimeRules.Rested(lastInPlace, now, PlaceTauntRest);

    /// <summary>
    /// The one who killed this person, or the one this person killed, inside the rest. The
    /// healer raised a fallen enemy and he was cut down again two hundred times in ten minutes.
    /// </summary>
    public static bool Feuding(
        string selfKiller,
        string foeName,
        string foeKiller,
        string selfName,
        DateTime selfDiedAt,
        DateTime foeDiedAt,
        DateTime now
    ) =>
        SameName(selfKiller, foeName) && !TimeRules.Rested(selfDiedAt, now, RevengeRest) ||
        SameName(foeKiller, selfName) && !TimeRules.Rested(foeDiedAt, now, RevengeRest);

    /// <summary>
    /// A skirmish ends for this fighter when badly hurt, or when it ran past its limit with the
    /// foe not nearly beaten (<see cref="FocusRules.FinishHitsFraction"/>): a winner finishes.
    /// </summary>
    public static bool ShouldDisengage(double hitsFraction, double foeHitsFraction, DateTime skirmishStart, DateTime now) =>
        hitsFraction < DisengageHits ||
        TimeRules.Passed(skirmishStart, now, SkirmishLimit) && foeHitsFraction >= FocusRules.FinishHitsFraction;

    /// <summary>
    /// The foe breaks off too when a skirmish ran past its limit. A fighter that broke off hurt
    /// leaves its foe free to chase it: 38 winners were stood down by the loser's own break-off.
    /// </summary>
    public static bool StandsFoeDown(double hitsFraction) => hitsFraction >= DisengageHits;

    /// <summary>The time slot a moment falls in. Both sides read the same slot, so they meet.</summary>
    public static long SlotOf(DateTime now) => now.Ticks / SlotLength.Ticks;

    /// <summary>
    /// The spot indexes open in a slot: <see cref="ActiveSpots"/> of them, spread round the list
    /// so the two open ones lie apart.
    /// </summary>
    public static int[] ActiveIndexes(long slot, int spotCount)
    {
        if (spotCount <= 0)
        {
            return [];
        }

        var count = Math.Min(ActiveSpots, spotCount);
        var indexes = new int[count];
        var first = (int)(Math.Abs(slot) % spotCount);
        var step = Math.Max(1, spotCount / count);

        for (var i = 0; i < count; i++)
        {
            indexes[i] = (first + i * step) % spotCount;
        }

        return indexes;
    }

    private static bool SameName(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) && string.Equals(first, second, StringComparison.Ordinal);
}
