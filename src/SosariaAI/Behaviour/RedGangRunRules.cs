using System;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;

namespace SosariaAI.Behaviour;

/// <summary>
/// A red's day, the 1999 way: a run out of Buccaneer's Den, then home to it, again and again.
/// Most runs work a PvP hot spot with the gang (see <see cref="RedGangRules"/>); the rest delve
/// a dungeon or farm monsters in the open. A run ends when the pack is heavy with loot, the red
/// is hurt again and again, the reagents or bandages run short, a band of blue fighters holds
/// the spot, or the run time is up; the red rides back to the Den, banks the take, heals,
/// restocks and hangs about a while, then rides out again (see
/// <see cref="GoalPlanRules.RedConflictRun"/>). A red that died sits out the runs a while
/// (<see cref="DeathRest"/>). Pure.
/// </summary>
public static class RedGangRunRules
{
    /// <summary>The hot spots draw most runs: patrols and ambushes were what a red lived for.</summary>
    public const int ConflictWeight = 3;

    /// <summary>A delve for loot now and then, where a red also meets the crews inside.</summary>
    public const int DungeonWeight = 1;

    /// <summary>A monster farm in the open now and then, for the gold a red spends on reagents.</summary>
    public const int HuntWeight = 1;

    /// <summary>The outings a run is made for, in the order the roll reads them.</summary>
    public static readonly string[] Outings = [SkillKinds.Conflict, SkillKinds.Dungeon, SkillKinds.Hunt];

    /// <summary>A red on a hot-spot run heads home when its hits dip under this again and again.</summary>
    public const double HeadHomeBelowHitsFraction = RecoveryRules.RecoverBelowHitsFraction;

    /// <summary>A red on a hot-spot run looks at its pack, its wounds and its supplies this often.</summary>
    public static readonly TimeSpan RunCheck = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A red run off its hot spot this many times on one run gives the spot up: the blues hold it.
    /// Reds at the Skara Brae and Yew moongates ran, got clear, patrolled back and ran again, 45 to
    /// 60 times each in two hours.
    /// </summary>
    public const int RunsOffLimit = 3;

    /// <summary>
    /// A red that died sits out the runs this long after its death: it rises, gets home,
    /// re-arms and rests before it rides out again. Live, 452 of 511 reds killed on a hot-spot
    /// run had died less than 30 minutes before, back out on the next run with a fresh body.
    /// </summary>
    public static readonly TimeSpan DeathRest = TimeSpan.FromMinutes(30);

    /// <summary>
    /// A red on its spot watches this far for blue fighters: the reach of a player's screen,
    /// past the blues' own draw (<see cref="RedGangRules.OutlawScanTiles"/>), so it sees a band coming.
    /// </summary>
    public const int WatchTiles = 18;

    /// <summary>The words the activity log gives a run the blues ended (<see cref="BluesHoldSpot"/>).</summary>
    public const string BluesHoldWords = "a band of blue fighters holds the spot";

    /// <summary>No low-hits looks yet: a red that has not set out.</summary>
    private const int NoLowHits = 0;

    private const int OutingSalt = 0x2ED;

    public static bool IsOuting(string skillKind) => Array.IndexOf(Outings, skillKind) >= 0;

    /// <summary>Why a red without arms takes no hot-spot run or delve, for the logs.</summary>
    public const string UnarmedWhy = "it has no arms to fight with";

    /// <summary>
    /// A red that lacks arms (<see cref="SpareKitRules.LacksArms"/>) takes no hot-spot run and
    /// no delve: it hunts for the gold to buy arms back. Three broke reds rode out to Destard in
    /// shirts and pants, with nothing in the bank to re-arm from, and stood at the door unable
    /// to kill anyone. "You don't go red by being a noob."
    /// </summary>
    public static bool BarredUnarmed(string outing, bool lacksArms) =>
        lacksArms && outing is SkillKinds.Conflict or SkillKinds.Dungeon;

    /// <summary>
    /// The outing of the next run: a weighted draw by <paramref name="seed"/> over the outings
    /// <paramref name="canRun"/> allows. With none open, the hot-spot run, whose step the plan
    /// skips until it can run, so the red hangs about the Den meanwhile.
    /// </summary>
    public static string PickOuting(int seed, Func<string, bool> canRun)
    {
        var total = 0;

        for (var i = 0; i < Outings.Length; i++)
        {
            total += Open(Outings[i], canRun);
        }

        if (total == 0)
        {
            return SkillKinds.Conflict;
        }

        var roll = ChoiceSeed.Unit(seed, OutingSalt) * total;

        for (var i = 0; i < Outings.Length; i++)
        {
            roll -= Open(Outings[i], canRun);

            if (roll < 0)
            {
                return Outings[i];
            }
        }

        return SkillKinds.Conflict;
    }

    /// <summary>
    /// Why a hot-spot run ends, or <see cref="HuntEndReason.None"/> while it goes on: the reasons
    /// a hunt ends, save for want of prey, since a red waits for its prey to come by; and a spot
    /// the red was run off <see cref="RunsOffLimit"/> times, as fights gone badly.
    /// </summary>
    public static HuntEndReason WhyHeadHome(
        DateTime now,
        DateTime runEnds,
        bool packFull,
        double hitsFraction,
        int lowHitsCount,
        bool suppliesLow,
        int runsOff
    )
    {
        var reason = HuntEndDecision.Reason(
            now,
            runEnds,
            packFull,
            hitsFraction,
            HeadHomeBelowHitsFraction,
            lowHitsCount,
            lastPreyAt: default,
            HuntEndDecision.EmptyHuntLimit,
            suppliesLow
        );

        return HuntEndDecision.Routed(reason, runsOff, RunsOffLimit);
    }

    /// <summary>
    /// Why a red does not set out on a hot-spot run at all, or <see cref="HuntEndReason.None"/>:
    /// a pack heavy with loot or short supplies would end the run at its first look. A red with
    /// no way home stood at the Shame camp and began and ended its run every two seconds, 97
    /// times in twenty minutes.
    /// </summary>
    public static HuntEndReason WhyNotSetOut(bool packFull, bool suppliesLow) =>
        WhyHeadHome(default, default, packFull, Vitals.FullHits, NoLowHits, suppliesLow, runsOff: 0);

    /// <summary>
    /// True while a red that died at <paramref name="lastDeathAt"/> sits out the runs
    /// (<see cref="DeathRest"/>). A red that never died rides.
    /// </summary>
    public static bool RestsAfterDeath(DateTime lastDeathAt, DateTime now) =>
        lastDeathAt != default && now >= lastDeathAt && now - lastDeathAt < DeathRest;

    /// <summary>
    /// The blue fighters in a red's sight hold its spot: a mob for its pack
    /// (<see cref="OutlawRules.IsMob"/>), or a side clearly stronger than the gang near it
    /// (<see cref="OutlawRules.RunPowerMargin"/>). The gang gives the spot up before the band
    /// draws, as the reds of the era left when an anti-PK guild rode in. Live, 511 reds died on
    /// a hot-spot run in one night, and 93 of the 94 rides home came when the time ran out.
    /// </summary>
    public static bool BluesHoldSpot(int blueFighters, int bluePower, int packSize, int gangPower) =>
        blueFighters > 0 &&
        (OutlawRules.IsMob(blueFighters, packSize) || bluePower > gangPower * OutlawRules.RunPowerMargin);

    /// <summary>The words the activity log gives for a run's end.</summary>
    public static string Because(HuntEndReason reason) =>
        reason switch
        {
            HuntEndReason.PackFull => "the pack is heavy with loot",
            HuntEndReason.SuppliesLow => "the reagents or bandages ran short",
            HuntEndReason.Hurt => "the fights went badly",
            HuntEndReason.TimeUp => "the run is over",
            _ => "the gang rides home"
        };

    private static int WeightOf(string outing) =>
        outing switch
        {
            SkillKinds.Conflict => ConflictWeight,
            SkillKinds.Dungeon => DungeonWeight,
            _ => HuntWeight
        };

    private static int Open(string outing, Func<string, bool> canRun) =>
        canRun != null && canRun(outing) ? WeightOf(outing) : 0;
}
