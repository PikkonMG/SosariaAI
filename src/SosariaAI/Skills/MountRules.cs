using System;

namespace SosariaAI.Skills;

/// <summary>
/// ModernUO BaseMount: one tile, owned or summoned, no rider, not ill.
/// Prefer the nearest owned mount.
/// </summary>
public static class MountRules
{
    public const int ReachTiles = 1;

    /// <summary>
    /// How far a rider looks, and walks, for a mount of its own. A pet told to Follow
    /// trails a tile or two behind, and a tamed beast stands where it was tamed. Asking
    /// for one within arm's reach failed every mount the log recorded.
    /// </summary>
    public const int SearchTiles = 12;

    public const int NoCandidate = -1;

    /// <summary>How long a remount that did not take waits before it is tried first again.</summary>
    public static readonly TimeSpan RemountRetry = TimeSpan.FromMinutes(1);

    /// <summary>
    /// A player on foot beside its own horse gets on before it does anything else: after a
    /// resurrection, after the corpse run, after any dismount. Not in a fight, and not again
    /// inside <see cref="RemountRetry"/> of a try that did not take.
    /// </summary>
    public static bool RemountsFirst(bool mounted, bool ownMountInReach, bool inFight, bool retryDue) =>
        !mounted && ownMountInReach && !inFight && retryDue;

    /// <summary>The rider must close the gap before it can climb on.</summary>
    public static bool NeedsWalk(int chebyshev) => chebyshev > ReachTiles;

    public static bool RiderMayMount(bool alreadyMounted) => !alreadyMounted;

    public static bool MountIsReady(bool deleted, bool isDeadPet, bool hasRider, bool poisoned) =>
        !deleted && !isDeadPet && !hasRider && !poisoned;

    public static bool RiderControls(
        bool controlled,
        bool controlMasterIsRider,
        bool summoned,
        bool summonMasterIsRider
    ) =>
        controlled && controlMasterIsRider || summoned && summonMasterIsRider;

    public static bool GenderAllowed(bool femaleRider, bool allowMale, bool allowFemale) =>
        femaleRider ? allowFemale : allowMale;

    public static int ChooseIndex(ReadOnlySpan<(bool Owned, int Chebyshev)> candidates)
    {
        var best = NoCandidate;
        var bestDist = int.MaxValue;

        for (var i = 0; i < candidates.Length; i++)
        {
            var (owned, dist) = candidates[i];

            if (!owned || dist > SearchTiles)
            {
                continue;
            }

            if (best == NoCandidate || dist < bestDist)
            {
                best = i;
                bestDist = dist;
            }
        }

        return best;
    }
}
