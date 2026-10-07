using System;
using SosariaAI.Behaviour;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>
/// When an outlaw attacks. The model never decides this. Free.
/// Murder counts are the engine's own; see <see cref="Mobiles.MurderReport"/>.
/// </summary>
public static class OutlawRules
{
    /// <summary>
    /// The share of a facet's people who are red when characters.json sets no PK count
    /// (operator decision 2026-09-23: about one in ten, so 200 people carry about 20 reds).
    /// </summary>
    public const double RedShare = 0.10;

    public const double CourageFloor = 0.35;

    /// <summary>People this close to a victim are the ones who would see or help.</summary>
    public const int AloneRange = 8;

    /// <summary>Fellow reds this close make a pack; the red itself counts.</summary>
    public const int PackRange = 26;

    /// <summary>Out on the roads a red alone starts only on a lone mark (<see cref="LoneMark"/>); a pack of this many starts on any.</summary>
    public const int MinPack = 2;

    /// <summary>No one near a mark but the mark itself.</summary>
    public const int NoOneNear = 0;

    /// <summary>Blues this close to a red make the crowd it backs off from.</summary>
    public const int CrowdRange = 12;

    /// <summary>This many blues, or more than the pack, is a mob: a red bides, or breaks off.</summary>
    public const int CrowdRetreat = 3;

    /// <summary>A red whose hits fall under this share runs.</summary>
    public const double RunHitsFraction = 0.35;

    /// <summary>Another red nearby, not of the gang, piles in on a victim this often.</summary>
    public const int StrayJoinPercent = 50;

    public const int PercentScale = 100;

    /// <summary>A victim with fewer people than this near it counts as isolated.</summary>
    public const int IsolationThreshold = 2;

    /// <summary>Victim scoring: each of these adds as much as the victim standing one tile nearer.</summary>
    public const double HurtWeight = 10;
    public const double AloneWeight = 6;
    public const double CrowdWeight = 4;
    public const double TierWeight = 2;

    /// <summary>The fewest seconds a red runs from a stronger side.</summary>
    public const int RunMinSeconds = 10;

    /// <summary>The spread of whole seconds a red's run may last past <see cref="RunMinSeconds"/>.</summary>
    public const int RunSpanSeconds = 30;

    /// <summary>
    /// How many reds a facet carries. A value the operator set wins, always: an explicit
    /// <c>pkEnabled: false</c> means none and an explicit <c>pkCount</c> is the count. Only a
    /// file that is silent gets the default: reds on, one in ten of the people.
    /// </summary>
    public static int RedCount(bool? pkEnabled, int? pkCount, int population, bool enabledWhenUnset)
    {
        if (!(pkEnabled ?? enabledWhenUnset) || population <= 0)
        {
            return 0;
        }

        var count = pkCount ?? (int)Math.Round(population * RedShare, MidpointRounding.AwayFromZero);
        return Math.Clamp(count, 0, population);
    }

    /// <summary>
    /// How good a mark is: near, hurt, isolated and weaker in skill. Each person near the mark
    /// past the first makes it worse: a lone traveler is far better prey than one in a crowd.
    /// The highest score is the victim the gang converges on.
    /// </summary>
    public static double VictimScore(int distance, double hitsFraction, int peopleNear, int tierGap) =>
        -Math.Max(0, distance) +
        HurtWeight * (Vitals.FullHits - Math.Clamp(hitsFraction, 0, Vitals.FullHits)) +
        (peopleNear < IsolationThreshold ? AloneWeight : -CrowdWeight * peopleNear) +
        TierWeight * Math.Max(0, tierGap);

    /// <summary>The blues near are a mob for this pack: as many as it holds, or three.</summary>
    public static bool IsMob(int blueCrowd, int packSize) => blueCrowd >= Math.Max(CrowdRetreat, packSize + 1);

    /// <summary>
    /// A mark with no one near it: the miner or the lone traveler a 1999 mage PK took alone.
    /// Live, every gang rode out one strong; a red alone started nothing, killed about once a
    /// day, and earned nothing to re-arm with.
    /// </summary>
    public static bool LoneMark(int peopleNear) => peopleNear <= NoOneNear;

    /// <summary>
    /// A red starts on a victim out of the guards' reach on Felucca; alone on the roads only on
    /// a <paramref name="loneMark"/> (a dungeon red holding its hall is the ambush itself), never
    /// in front of a mob, and only when the pack's power beats the victim's and its nerve holds.
    /// </summary>
    public static bool MayAttack(
        DispositionKind disposition,
        bool felucca,
        bool guards,
        int packSize,
        int blueCrowd,
        bool inDungeon,
        bool loneMark,
        int selfPower,
        int targetPower,
        double courage
    )
    {
        if (disposition != DispositionKind.Outlaw || !felucca || guards)
        {
            return false;
        }

        if (packSize < MinPack && !inDungeon && !loneMark || IsMob(blueCrowd, packSize))
        {
            return false;
        }

        if (targetPower <= 0 || selfPower <= targetPower)
        {
            return false;
        }

        return courage >= CourageFloor;
    }

    /// <summary>
    /// A side must be this much stronger than the red's before the red runs from it. With no
    /// margin the run test was the start test turned over: a victim that drew its weapon or
    /// healed a little tipped it, and reds ran from 339 of 838 victims, most within 4 seconds.
    /// </summary>
    public const double RunPowerMargin = 1.25;

    /// <summary>
    /// Why a red runs: badly hurt; else, unless its victim is nearly beaten
    /// (<see cref="FocusRules.FinishHitsFraction"/>), a clearly stronger side
    /// (<see cref="RunPowerMargin"/>) or a mob. A red with its victim nearly dead finishes it.
    /// </summary>
    public static RedRunReason RunReason(
        int selfPower,
        int incomingPower,
        double hitsFraction,
        int blueCrowd,
        int packSize,
        double victimHitsFraction
    )
    {
        if (hitsFraction < RunHitsFraction)
        {
            return RedRunReason.Hurt;
        }

        if (victimHitsFraction < FocusRules.FinishHitsFraction)
        {
            return RedRunReason.None;
        }

        if (incomingPower > selfPower * RunPowerMargin)
        {
            return RedRunReason.Stronger;
        }

        return IsMob(blueCrowd, packSize) ? RedRunReason.Mob : RedRunReason.None;
    }

    /// <summary>The words the activity log gives a red's run.</summary>
    public static string RunWords(RedRunReason reason) =>
        reason switch
        {
            RedRunReason.Hurt => "badly hurt",
            RedRunReason.Stronger => "a stronger side",
            RedRunReason.Mob => "a mob",
            RedRunReason.Guards => "the guards",
            _ => "no reason"
        };

    public static bool IsLiveHumanCombatant(bool deleted, bool alive, bool isPlayer, bool isSosaria) =>
        !deleted && alive && (isPlayer || isSosaria);
}

/// <summary>Why a red runs from a fight (<see cref="OutlawRules.RunReason"/>).</summary>
public enum RedRunReason
{
    None,
    Hurt,
    Stronger,
    Mob,

    /// <summary>A red under the guards drops the fight (see <c>GuardLineRules.RedBreaksOff</c>).</summary>
    Guards
}
