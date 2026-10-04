using System;
using SosariaAI.Combat;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>Where a red on a hot-spot run stands in the gang's ride out (see <see cref="RedGangRules"/>).</summary>
public enum GangRunPhase
{
    /// <summary>At the Den's muster point, waiting a short while for the gang.</summary>
    Muster,

    /// <summary>On the way to the camp.</summary>
    Ride,

    /// <summary>At the camp, waiting a short while for the mates still on the way.</summary>
    Gather,

    /// <summary>Working the spot: patrols and ambushes.</summary>
    Work
}

/// <summary>How a mustered gang rides out of the Den to its camp.</summary>
public enum GangRideWay
{
    /// <summary>On the roads, through the Den's moongate.</summary>
    OnFoot,

    /// <summary>Each by its own recall to a rune that lands by the camp.</summary>
    ByRune
}

/// <summary>
/// How a red gang works a hot spot, the 1999 way: it meets in the Den, rides out as one, the
/// first there wait for the rest, and then it patrols the roads round the spot, looking for
/// prey, and now and then (about one roll in three, every six to twelve minutes) it lies in
/// wait at the spot's camp for four to eight minutes, shuffling about. A gang member that
/// drifts more than a few steps from its mates walks back to them: a pack of three is far
/// harder to mob than three lone reds. Pure.
/// </summary>
public static class RedGangRules
{
    /// <summary>A gang meets within this many tiles of its muster point in the Den.</summary>
    public const int MusterTiles = 6;

    /// <summary>
    /// A gang musters at least this long, so the call reaches the mates about the Den before
    /// the first one sets out. Each mate once took its own way, and the first one there stood
    /// alone at the camp.
    /// </summary>
    public static readonly TimeSpan MusterMin = TimeSpan.FromSeconds(15);

    /// <summary>A gang musters at most this long; a mate not there by then is left behind.</summary>
    public static readonly TimeSpan MusterMax = TimeSpan.FromSeconds(90);

    /// <summary>The way a gang chose holds this long: a mate that comes to the muster late takes it too.</summary>
    public static readonly TimeSpan RideWayHold = MusterMax;

    /// <summary>A mate on its way this close to the camp counts as there.</summary>
    public const int GatherTiles = CohesionTiles;

    /// <summary>The first at the camp wait at most this long for the rest before they work the spot.</summary>
    public static readonly TimeSpan GatherMax = TimeSpan.FromMinutes(2);

    /// <summary>
    /// A red musters its gang before a run when it has a gang, stands in the Den and the
    /// muster point lies in the Den too: a red out on the land sets out from where it stands.
    /// </summary>
    public static bool Musters(bool inGang, bool inDen, bool pointInDen) => inGang && inDen && pointInDen;

    /// <summary>
    /// The muster is over when every mate called stands at the point after the least wait, or
    /// the wait ran out (see <see cref="PartyWaitRules.WaitForMembers"/>).
    /// </summary>
    public static bool MusterOver(PartyWaitResult wait, DateTime since, DateTime now) =>
        wait == PartyWaitResult.TimedOut || wait == PartyWaitResult.Formed && now - since >= MusterMin;

    /// <summary>
    /// The gang recalls when every one of the <paramref name="riders"/> can recall to a rune by
    /// the camp; else it rides on foot, all through the same gate, and no one goes ahead alone.
    /// </summary>
    public static GangRideWay WayOut(int riders, int canRecall) =>
        riders > 0 && canRecall >= riders ? GangRideWay.ByRune : GangRideWay.OnFoot;

    /// <summary>A way chosen at <paramref name="decidedAt"/> still holds for the gang.</summary>
    public static bool WayHolds(DateTime decidedAt, DateTime now) => decidedAt != default && now - decidedAt < RideWayHold;

    /// <summary>The words the activity log gives for a gang's way out.</summary>
    public static string WayWords(GangRideWay way) =>
        way == GangRideWay.ByRune ? "by recall to one rune" : "on foot through the Den gate";

    /// <summary>
    /// A gang mate hears a red's call to ride when it stands in pack range, or anywhere in the
    /// Den while the red musters there: the gang hangs about the whole town between runs.
    /// </summary>
    public static bool HearsRideCall(int distance, bool callerMusters, bool bothInDen) =>
        distance >= 0 && distance <= OutlawRules.PackRange || callerMusters && bothInDen;

    /// <summary>A red scans this far for a victim.</summary>
    public const int VictimScanTiles = 12;

    /// <summary>A blue scans this far for an outlaw to warn of or ride down.</summary>
    public const int OutlawScanTiles = 10;

    /// <summary>The roads a gang patrols lie this close to its hot spot.</summary>
    public const int PatrolRadiusTiles = 40;

    /// <summary>Road nodes near the spot looked at for the patrol; the nearest this many.</summary>
    public const int PatrolNodeScan = 48;

    /// <summary>A gang member farther than this from its nearest mate walks back to it.</summary>
    public const int CohesionTiles = 12;

    /// <summary>A red walking back to its pack stops this close to its mate.</summary>
    public const int RegroupTiles = 4;

    /// <summary>A mate this far or more is no longer near: the pack has split, and each patrols alone.</summary>
    public const int CohesionReachTiles = OutlawRules.PackRange;

    /// <summary>How often a gang member looks for its mates.</summary>
    public static readonly TimeSpan CohesionCheck = TimeSpan.FromSeconds(8);

    /// <summary>A patrolling gang rolls for an ambush this often, somewhere in this span.</summary>
    public static readonly TimeSpan AmbushRollMin = TimeSpan.FromMinutes(6);

    public static readonly TimeSpan AmbushRollMax = TimeSpan.FromMinutes(12);

    /// <summary>The share of ambush rolls that send the gang to lie in wait.</summary>
    public const int AmbushChancePercent = 35;

    /// <summary>A gang lies in wait at its camp for at least half the lurk and at most all of it.</summary>
    public static readonly TimeSpan LurkMin = PkGangRules.LurkTime / 2;

    public static readonly TimeSpan LurkMax = PkGangRules.LurkTime;

    /// <summary>A lurking red shuffles a step now and then, somewhere in this span.</summary>
    public static readonly TimeSpan ShuffleMin = TimeSpan.FromSeconds(4);

    public static readonly TimeSpan ShuffleMax = TimeSpan.FromSeconds(10);

    /// <summary>A lurking red farther than this from its camp steps back toward it.</summary>
    public const int ShuffleLeashTiles = 8;

    /// <summary>A patrol has no leg to walk.</summary>
    public const int NoLeg = -1;

    /// <summary>A red gives up the walk to a body it has not reached in this long, and leaves the body.</summary>
    public static readonly TimeSpan LootWalkLimit = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A gang mate rides out with a red leaving for the gang's hot spot when it is free (idle,
    /// or hanging about the Den between runs) and not already on the run. A mate on the run
    /// still reads as hanging out, and was sent out again on every scan, a full road search
    /// on the world thread each time.
    /// </summary>
    public static bool JoinsRide(bool onRun, bool idle, bool hangingOut) => !onRun && (idle || hangingOut);

    /// <summary>
    /// A mate called to ride takes the run only when its own run can start: the run does not
    /// rest after repeated failure, a camp lies within its reach, and it would set out at all
    /// (<see cref="RedGangRunRules.WhyNotSetOut"/>). The call handed Kerr a run with no camp in
    /// reach every two seconds; each one failed at once, and he left Conflict "for 60 minutes"
    /// 49 times in half an hour. Mates short of reagents or heavy with loot were called out of
    /// the Den tavern and turned back the same second: 47 of 78 such ends in forty minutes.
    /// </summary>
    public static bool MayRide(bool runRests, bool campInReach, bool setsOut) => !runRests && campInReach && setsOut;

    /// <summary>
    /// A red answers a call onto a victim only when it would stay in the fight: not inside its
    /// run grace for the victim's side, and not a fight its run test would leave at once. A
    /// mate pulled in against the odds said "too many" and ran the same second.
    /// </summary>
    public static bool JoinsCall(bool leavesSideBe, bool wouldRunAtOnce) => !leavesSideBe && !wouldRunAtOnce;

    /// <summary>
    /// A red goes for a body its gang made only once, and only out of the guards' reach: a
    /// victim that died over the guard line lies where a looting red dies.
    /// </summary>
    public static bool GoesForBody(bool bodyGuarded, bool looted) => !bodyGuarded && !looted;

    /// <summary>
    /// True when a red strips this from a body besides its best few picks: the gold and the
    /// supplies a fight burns (reagents, potions, scrolls, bandages, arrows and bolts). No shop
    /// out of the guards' reach sells a caster's reagents, so a red caster refills from the
    /// people it kills, as the PKs of the era did; with the best few picks alone, 292 reds rode
    /// home short of supplies in one night.
    /// </summary>
    public static bool StripsAsSupply(LootKind kind, bool bandageOrAmmo) =>
        bandageOrAmmo || kind is LootKind.Gold or LootKind.Reagent or LootKind.Potion or LootKind.Scroll;

    /// <summary>True when a walk to a body begun at <paramref name="since"/> has run past <see cref="LootWalkLimit"/>.</summary>
    public static bool GivesUpLootWalk(DateTime since, DateTime now) => now - since >= LootWalkLimit;

    /// <summary>True when this roll, out of <see cref="OutlawRules.PercentScale"/>, sends the gang to lie in wait.</summary>
    public static bool ShouldAmbush(int roll) => roll >= 0 && roll < AmbushChancePercent;

    /// <summary>
    /// A time in <paramref name="min"/> to <paramref name="max"/> picked by
    /// <paramref name="fraction"/>, from 0 to 1: the next ambush roll, a lurk's end, a shuffle.
    /// </summary>
    public static TimeSpan Between(TimeSpan min, TimeSpan max, double fraction) =>
        min + (max - min) * Math.Clamp(fraction, 0, 1);

    /// <summary>
    /// A gang member walks back to its nearest mate when the mate is farther than
    /// <see cref="CohesionTiles"/> but still near enough to count as its pack.
    /// </summary>
    public static bool StraysFromPack(int mateDistance) =>
        mateDistance > CohesionTiles && mateDistance < CohesionReachTiles;

    /// <summary>A lurking red wanders no farther than <see cref="ShuffleLeashTiles"/> from its camp.</summary>
    public static bool ShuffleStepsBack(int campDistance) => campDistance > ShuffleLeashTiles;

    /// <summary>
    /// The next patrol leg among <paramref name="legs"/> road nodes: a pick by
    /// <paramref name="roll"/> that is never the leg just walked, while another exists.
    /// <see cref="NoLeg"/> when there is none.
    /// </summary>
    public static int NextLeg(int legs, int previous, int roll)
    {
        if (legs <= 0)
        {
            return NoLeg;
        }

        if (legs == 1)
        {
            return 0;
        }

        var pick = Math.Abs(roll % (legs - 1));
        return previous >= 0 && pick >= previous ? pick + 1 : pick;
    }
}
