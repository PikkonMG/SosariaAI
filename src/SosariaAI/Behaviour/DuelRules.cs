using System;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

/// <summary>Where a duel stands. Only a fight in <see cref="Fighting"/> makes blows lawful.</summary>
public enum DuelStage
{
    Walking,
    Fighting,
    Over
}

/// <summary>
/// A friendly duel outside the bank: challenge, "gl", walk ten tiles clear, fight to low
/// hits, "gf". Nobody dies: the fight stops at a hit floor, and a fight that runs out its
/// clock goes to the one less hurt. Pure. No world objects.
/// </summary>
public static class DuelRules
{
    /// <summary>The duel ground lies this far from the banker.</summary>
    public const int ClearTiles = 10;

    /// <summary>The two duelists stand this far apart when the fight starts.</summary>
    public const int SpacingTiles = 2;

    /// <summary>A partner this close to the challenger may be asked.</summary>
    public const int ChallengeRange = 8;

    /// <summary>Both fighters start near full health.</summary>
    public const double ReadyHitsFraction = 0.9;

    /// <summary>The fight stops once a duelist falls to this share of its hits.</summary>
    public const double StopHitsFraction = 0.25;

    /// <summary>However frail the duelist, the floor keeps it alive.</summary>
    public const int MinStopHits = 1;

    /// <summary>
    /// A new duel starts shard-wide at most once in a gap between these two, and at most
    /// <see cref="MaxActive"/> run at once: a duel is a show at the bank, not a queue.
    /// </summary>
    public static readonly TimeSpan AttemptGapMin = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan AttemptGapMax = TimeSpan.FromMinutes(12);

    public const int MaxActive = 2;

    /// <summary>Duelists are an even match: their skill tiers lie at most this far apart.</summary>
    public const int MaxTierGap = 2;

    /// <summary>A good duel warms the two duelists to each other by this much.</summary>
    public const int FriendlyBond = 10;

    /// <summary>The reason a good duel gives the bond.</summary>
    public const string FriendlyReason = "a good duel";

    public static readonly TimeSpan WalkLimit = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan FightLimit = TimeSpan.FromMinutes(3);

    /// <summary>A duelist rests this long before the next challenge.</summary>
    public static readonly TimeSpan Rest = TimeSpan.FromMinutes(40);

    /// <summary>
    /// Two idle fighters may duel when both stand near full health, are an even match, are not
    /// Order against Chaos (those fight for real) and are not from two different guilds: a blow
    /// between rival guilds would start a guild war, not a spar.
    /// </summary>
    public static bool MayChallenge(
        double challengerHits,
        double partnerHits,
        int challengerGuild,
        int partnerGuild,
        int noGuild,
        int tierGap,
        bool swornFoes
    ) =>
        challengerHits >= ReadyHitsFraction &&
        partnerHits >= ReadyHitsFraction &&
        Math.Abs(tierGap) <= MaxTierGap &&
        !swornFoes &&
        (challengerGuild == noGuild || partnerGuild == noGuild || challengerGuild == partnerGuild);

    /// <summary>Another duel may start while fewer than <see cref="MaxActive"/> run.</summary>
    public static bool MayStartAnother(int active) => active < MaxActive;

    /// <summary>
    /// A fight that runs out its clock goes to the judges: the one with the smaller share of
    /// its hits lost. A tie goes to the challenger.
    /// </summary>
    public static bool ChallengerLosesOnPoints(double challengerHits, double partnerHits) =>
        challengerHits < partnerHits;

    public static bool Rested(DateTime lastDuel, DateTime now) =>
        TimeRules.Rested(lastDuel, now, Rest);

    /// <summary>The hits a duelist keeps: blows past this end the duel instead.</summary>
    public static int StopHits(int hitsMax) => Math.Max(MinStopHits, (int)(hitsMax * StopHitsFraction));

    /// <summary>The damage a blow may still do before the duel floor stops it.</summary>
    public static int CappedDamage(int hits, int hitsMax, int amount) =>
        Math.Clamp(Math.Min(amount, hits - StopHits(hitsMax)), 0, Math.Max(0, amount));

    /// <summary>True when this blow reaches the floor and ends the duel.</summary>
    public static bool EndsDuel(int hits, int hitsMax, int amount) => hits - amount <= StopHits(hitsMax);

    public static bool TimedOut(DuelStage stage, DateTime stageStarted, DateTime now) =>
        stage switch
        {
            DuelStage.Walking => now - stageStarted >= WalkLimit,
            DuelStage.Fighting => now - stageStarted >= FightLimit,
            _ => false
        };
}
