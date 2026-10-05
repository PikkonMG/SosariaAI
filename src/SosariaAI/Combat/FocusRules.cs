using System;
using Server.Mobiles;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>
/// Who a player swings at. A red, or a gray who is attacking people, outranks every
/// monster: nobody who drew on a murderer turns round for a rat. Next come whoever is
/// hitting me, then whoever is hitting a friend or a party mate, then the rest. Inside a
/// rank the near, the hurt and the alone come first. A foe past a character's own sight
/// counts only while it is in a fight with that character or a friend.
/// </summary>
public static class FocusRules
{
    public const int OutlawTier = 3;
    public const int AttackerTier = 2;
    public const int FriendAttackerTier = 1;
    public const int PlainTier = 0;

    public const double TierWeight = 100;
    public const double DistanceWeight = 3;
    public const double HurtWeight = 10;
    public const double PackmateWeight = 4;
    public const double IsolationWeight = 10;
    public const int NeutralKarma = 0;

    /// <summary>A new attacker must be this much nearer than a current foe that is not hitting back.</summary>
    public const int SwitchSlackTiles = 3;

    /// <summary>A new attacker must be this much nearer than a current foe that is hitting back.</summary>
    public const int ClearSwitchTiles = 6;

    /// <summary>
    /// Hysteresis: a new foe is kept at least this long. Two reds made one anti-PK turn
    /// between them 35 times in an hour.
    /// </summary>
    public const int SwitchCooldownMs = 10000;

    /// <summary>
    /// A foe under this share of its hits is nearly beaten, and the fight stays on it. Fighters
    /// turned from a fleeing foe at a sixth of its hits to a fresh one, and ran from that one.
    /// </summary>
    public const double FinishHitsFraction = 0.35;

    /// <summary>Only an attacker this close takes the fight from a nearly beaten foe.</summary>
    public const int ArmsReachTiles = 1;

    /// <summary>A group the engage call left alone stays left alone this long: a few danger scans that each ask again.</summary>
    public const int DeclineHoldMs = 3000;

    /// <summary>
    /// A mob standing in the group the engage call just left alone. A hunter does not walk up
    /// to it: walking up to a group it would not fight brought the whole group onto it.
    /// </summary>
    public static bool InDeclinedGroup(long declinedAt, long now, int tilesFromDeclined) =>
        declinedAt != 0 && now - declinedAt < DeclineHoldMs && tilesFromDeclined <= FightPullRules.IsolateRange;

    public static int Tier(bool isPerson, bool isRed, bool isGray, bool attacksPerson, bool attacksSelf, bool attacksFriend)
    {
        if (isPerson && (isRed || isGray && attacksPerson))
        {
            return OutlawTier;
        }

        return attacksSelf ? AttackerTier : attacksFriend ? FriendAttackerTier : PlainTier;
    }

    /// <summary>Inside the character's own sight anything counts; past it, only a foe already on it or on a friend.</summary>
    public static bool Registers(int distance, int sightRange, bool attacksSelf, bool attacksFriend) =>
        distance <= sightRange || attacksSelf || attacksFriend;

    public static double Score(int tier, int distance, double hitsFraction, int packmates) =>
        tier * TierWeight -
        distance * DistanceWeight +
        (Vitals.FullHits - hitsFraction) * HurtWeight -
        packmates * PackmateWeight;

    /// <summary>For a pull: the foe with the fewest neighbours first, the nearer on a tie.</summary>
    public static double IsolationScore(int packmates, int distance) =>
        -packmates * IsolationWeight - distance;

    /// <summary>ModernUO fight modes: None takes nothing, Aggressor only who fights me, Evil also the evil.</summary>
    public static bool PassesFightMode(FightMode mode, bool hostile, int karma) =>
        mode switch
        {
            FightMode.None => false,
            FightMode.Aggressor => hostile,
            FightMode.Evil => hostile || karma < NeutralKarma,
            _ => true
        };

    /// <summary>
    /// The foe a fighter is on holds at least the attacker rank, whether it hit back yet or
    /// not: the fight is the fighter's own. Ranked plain, a mob walked up to lost the fight to
    /// every other mob that swung on the way, and a hunter hit one or two blows on each mob
    /// of a forest and had them all on it.
    /// </summary>
    public static int HeldTier(int tier) => Math.Max(tier, AttackerTier);

    /// <summary>
    /// Whether a new foe takes the fight: a higher rank than the held one (<see cref="HeldTier"/>),
    /// or a clearly nearer attacker of the same rank. A nearly beaten foe
    /// (<see cref="FinishHitsFraction"/>) is finished first: only an attacker at arm's reach
    /// takes the fight from it.
    /// </summary>
    public static bool ShouldSwitch(
        int currentTier,
        int currentDistance,
        bool currentAttacksSelf,
        double currentHitsFraction,
        int candidateTier,
        int candidateDistance,
        bool candidateAttacksSelf
    )
    {
        if (currentHitsFraction < FinishHitsFraction)
        {
            return candidateAttacksSelf && candidateDistance <= ArmsReachTiles;
        }

        var heldTier = HeldTier(currentTier);

        if (candidateTier != heldTier)
        {
            return candidateTier > heldTier;
        }

        return candidateAttacksSelf && ClearlyNearer(candidateDistance, currentDistance, currentAttacksSelf);
    }

    /// <summary>
    /// A new attacker of the same rank takes the fight only when it is clearly nearer: by
    /// <see cref="SwitchSlackTiles"/> from a foe that is not hitting back, by
    /// <see cref="ClearSwitchTiles"/> from one that is.
    /// </summary>
    public static bool ClearlyNearer(int candidateDistance, int currentDistance, bool currentAttacksSelf) =>
        candidateDistance + (currentAttacksSelf ? ClearSwitchTiles : SwitchSlackTiles) <= currentDistance;
}
