using System;

namespace SosariaAI.Combat;

/// <summary>
/// Nearby people help when one of their own is attacked. They do not watch an orc
/// kill a neighbour and keep chopping.
/// </summary>
public static class AssistRules
{
    /// <summary>
    /// Helpers answer a friend's fight from this far, a little past their own sight.
    /// A fight a screen away still draws the neighbours in.
    /// </summary>
    public const int HelpRange = 14;

    /// <summary>
    /// At most this many answer one foe, those already on it counted. Every blow rallies again,
    /// and with no cap one hit on a stranger drew every armed person in reach.
    /// </summary>
    public const int MaxHelpers = 3;

    public static bool ShouldHelp(
        bool selfAlive,
        bool allyAlive,
        bool sameMap,
        int distance,
        bool selfHasWeapon,
        bool aggressorIsEnemy,
        bool selfIsGhost,
        bool busyWithOtherFoe,
        bool underGuards
    ) =>
        selfAlive &&
        allyAlive &&
        sameMap &&
        !selfIsGhost &&
        selfHasWeapon &&
        aggressorIsEnemy &&
        !busyWithOtherFoe &&
        !underGuards &&
        distance >= 0 &&
        distance <= HelpRange;

    /// <summary>How many of the <paramref name="willing"/> are called when <paramref name="alreadyOnFoe"/> already fight it.</summary>
    public static int HelpersToCall(int alreadyOnFoe, int willing) =>
        Math.Clamp(MaxHelpers - Math.Max(0, alreadyOnFoe), 0, Math.Max(0, willing));

    public static bool ShouldStandAndFight(bool hasWeapon, int helpersInRange) =>
        hasWeapon && helpersInRange > 0;
}
