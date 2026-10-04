using System;
using SosariaAI.Common;

namespace SosariaAI.Combat;

/// <summary>
/// In a guarded town a person shouts for the guards. A moon pad in Britain is still
/// under the town watch. A person foe is called on only when the law is on the caller's
/// side: a red, or a gray, standing under the same watch. A duel, an Order-against-Chaos fight or a guild war is no crime.
/// One foe draws one shout at a time: four people shouted "Guards!" in the same second.
/// </summary>
public static class GuardCallRules
{
    public const string Shout = "Guards!";
    public const int CooldownSeconds = 8;

    /// <summary>After one shout on a foe, nobody else shouts on it for this long.</summary>
    public const int FoeCooldownSeconds = 20;

    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(CooldownSeconds);
    private static readonly TimeSpan FoeCooldown = TimeSpan.FromSeconds(FoeCooldownSeconds);

    /// <summary>
    /// The law's view of a person foe. A red always; a gray only when its flag did not come
    /// from the lawful fight it is in (a guild war, Order against Chaos, a duel).
    /// </summary>
    public static bool IsOutlaw(bool murderer, bool criminal, bool inLawfulFight) =>
        murderer || criminal && !inLawfulFight;

    /// <summary>The caller's own rest and the foe's rest have both run out.</summary>
    public static bool CallDue(DateTime lastBySelf, DateTime lastOnFoe, DateTime now) =>
        TimeRules.Rested(lastBySelf, now, Cooldown) && TimeRules.Rested(lastOnFoe, now, FoeCooldown);

    public static bool ShouldCall(
        bool selfAlive,
        bool ghost,
        bool inGuardedRegion,
        bool foeAlive,
        bool sameMap,
        bool foeIsGuard,
        bool foeIsPerson,
        bool foeIsOutlaw,
        bool foeUnderGuards
    ) =>
        selfAlive &&
        !ghost &&
        inGuardedRegion &&
        foeAlive &&
        sameMap &&
        !foeIsGuard &&
        (!foeIsPerson || foeIsOutlaw && foeUnderGuards);

    /// <summary>A monster in town is made a guard target; a person keeps the flag the law gave it.</summary>
    public static bool MarksFoeCriminal(bool foeIsPerson) => !foeIsPerson;
}
