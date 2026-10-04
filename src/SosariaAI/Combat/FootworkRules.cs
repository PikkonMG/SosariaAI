using System;

namespace SosariaAI.Combat;

/// <summary>What a ranged fighter does this tick.</summary>
public enum Footwork
{
    /// <summary>The next blow lands after the cast or the shot is done: act now.</summary>
    Act,

    /// <summary>A blow would ruin it: step clear first.</summary>
    OpenGap,

    /// <summary>The steps did not buy the room (pinned, or a chaser as fast): act where it stands.</summary>
    Commit
}

/// <summary>
/// Footwork for a mage or an archer with a foe on top of it, learned from how 1999 players
/// kited. Casting and shooting both root the body, so a fighter that answers every adjacent
/// foe at once never leaves melee. It walks first, and acts once the gap holds. It gives up
/// the walk only for a reason: pinned, or still caught after <see cref="KiteGraceMs"/>. Steps
/// go at the lawful run pace on the move clock; two of them when the foe is right there.
/// </summary>
public static class FootworkRules
{
    /// <summary>How long a break-away may run before it accepts that the chaser is as fast.</summary>
    public const int KiteGraceMs = 2500;

    /// <summary>After the grace, it fights where it stands this long before it tries the steps again.</summary>
    public const int CommitWindowMs = 4000;

    /// <summary>A foe this close gets two retreat steps; one step only shuffles alongside it.</summary>
    public const int DoubleStepTiles = 2;

    public const int SingleStep = 1;
    public const int DoubleStep = 2;

    /// <summary>A crowd of this many close foes is backed away from as a mass, not one by one.</summary>
    public const int CrowdCount = 2;

    /// <summary>Foes this close count toward the mass the fighter backs away from.</summary>
    public const int CrowdTiles = 4;

    /// <summary>An unreachable foe that stands still is tried again after this long.</summary>
    public const int ChaseRetryMs = 1000;

    /// <summary>A mage in the press keeps at least this far; closer, it steps out before it casts.</summary>
    public const int KeepBandTiles = 3;

    /// <summary>A kiting or healing mage, or one walking off a held foe, keeps at least this far.</summary>
    public const int KiteBandTiles = 6;

    /// <summary>A spell of this circle or higher that fits right now is worth casting instead of more steps.</summary>
    public const int WindowCircle = 3;

    /// <summary>
    /// The call for this tick. The act holds: act. Holding ground: act where it stands. Pinned:
    /// no step is possible, act. Kiting: keep walking. Otherwise walk until the grace runs out.
    /// </summary>
    public static Footwork Next(bool holds, long kitingMs, bool pinned, CombatStance stance)
    {
        if (holds)
        {
            return Footwork.Act;
        }

        if (stance == CombatStance.Hold || pinned)
        {
            return Footwork.Commit;
        }

        return stance != CombatStance.Kite && kitingMs >= KiteGraceMs ? Footwork.Commit : Footwork.OpenGap;
    }

    /// <summary>The break-away and the stand after it are both over: start a fresh break-away.</summary>
    public static bool BreakAwayExpired(long kitingMs) => kitingMs >= KiteGraceMs + CommitWindowMs;

    /// <summary>
    /// How long an archer still needs before its shot: the stillness it lacks, or its own swing
    /// clock, whichever is later.
    /// </summary>
    public static int ShotNeedMs(int bowStillMs, long stillForMs, long swingReadyInMs) =>
        (int)Math.Clamp(Math.Max(bowStillMs - stillForMs, swingReadyInMs), 0, int.MaxValue);

    /// <summary>A strong spell fits now (the foe just swung, or is still closing): cast it rather than step.</summary>
    public static bool TakesWindow(int circle) => circle >= WindowCircle;

    /// <summary>How near a mage lets the foe come before it steps out, by stance.</summary>
    public static int BandMin(CombatStance stance, bool foeHeld) =>
        stance switch
        {
            CombatStance.Kite or CombatStance.Heal => KiteBandTiles,
            CombatStance.ParalyzeThenKite when foeHeld => KiteBandTiles,
            _ => KeepBandTiles
        };

    public const int SeBowStillMs = 250;
    public const int AosBowStillMs = 500;
    public const int PreAosBowStillMs = 1000;

    /// <summary>How long an archer stands still before the bow fires, by era (BaseRanged.OnSwing).</summary>
    public static int BowStillMs(bool aos, bool se) => se ? SeBowStillMs : aos ? AosBowStillMs : PreAosBowStillMs;

    public static int RetreatSteps(int nearestDistance) =>
        nearestDistance <= DoubleStepTiles ? DoubleStep : SingleStep;

    /// <summary>Two or more foes close by: back away from their middle, not from the target alone.</summary>
    public static bool IsCrowd(int closeFoes) => closeFoes >= CrowdCount;

    /// <summary>
    /// A chase that found no way is tried again when the foe moves, or after a short wait.
    /// Without it one blocked step left a fighter standing still until the fight timed out.
    /// </summary>
    public static bool RetryChase(bool foeMoved, long blockedMs) => foeMoved || blockedMs >= ChaseRetryMs;

    /// <summary>
    /// A fight that ends on a foe still alive on the same map, lost past the grace (out of
    /// sight, too far, or no way to it), is a chase given up: the foe is left be for the
    /// stand-down grace (<see cref="GuardLineRules.StandDownGrace"/>). A foe that died or left
    /// the map is not given up. Without it the next call or scan handed the lost foe back the
    /// moment the fight ended: Ivo Reed, stuck on one tile in the Den, took up Svana again
    /// every few seconds, a fresh fight and a fresh Jev stance ask each time, 127 asks in one run.
    /// </summary>
    public static bool GivesUpChase(bool foeAlive, bool sameMap, bool lostPastGrace) => foeAlive && sameMap && lostPastGrace;

    /// <summary>The cursor of a finished cast is answered on the next think, at most this late.</summary>
    public const int CursorAnswerMs = 250;

    /// <summary>Pre-AOS a Magic Arrow's damage lands this long after it is aimed (SpellHelper.OldDamageDelay).</summary>
    public const int SpellDamageDelayMs = 500;

    /// <summary>From the first word to the blow: how long a Magic Arrow takes to break a foe's spell.</summary>
    public static int ArrowBreakMs(bool slowedByProtection) =>
        CastTiming.CastDelayMs(CastTiming.FirstCircle, slowedByProtection) + CursorAnswerMs + SpellDamageDelayMs;

    /// <summary>
    /// A person casting is worth breaking: melee turns on a caster in reach, and a mage throws
    /// a spell that lands before the foe's words end. Monsters cannot be disturbed (Spell.OnCasterHurt).
    /// </summary>
    public static bool CanBreakCast(bool foeIsPlayer, long foeCastLeftMs, int breakMs) =>
        foeIsPlayer && foeCastLeftMs > 0 && CastTiming.Holds(breakMs, foeCastLeftMs);

    /// <summary>A melee fighter turns to a casting person within this many tiles of its reach.</summary>
    public const int InterruptSlackTiles = 1;

    public static bool ShouldTurnOnCaster(bool foeIsPlayer, bool casting, int distance, int reach) =>
        foeIsPlayer && casting && distance <= reach + InterruptSlackTiles;
}
