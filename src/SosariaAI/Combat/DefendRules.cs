using System;
using SosariaAI.Behaviour;

namespace SosariaAI.Combat;

/// <summary>
/// A person with a weapon in hand fights the thing hitting them. Town stance must not
/// clear that fight so they can walk a moon.
/// </summary>
public static class DefendRules
{
    /// <summary>
    /// A runner does not turn back on what it runs from, a person no more than a monster:
    /// turning back on a person flipped the body between flee and fight every think. The
    /// combat brain stands it at bay when it is cornered, the blows barely hurt, it healed up,
    /// or it cannot outrun the person on it.
    /// </summary>
    public static bool ShouldFightBack(
        bool selfAlive,
        bool ghost,
        bool foeAlive,
        bool sameMap,
        bool fleeing = false
    ) => selfAlive && !ghost && foeAlive && sameMap && !fleeing;

    /// <summary>
    /// A swing that landed is enough. Guild war starts on the first hit, so IsEnemy
    /// is still false for that blow; waiting for it left the target standing.
    /// </summary>
    public static bool ShouldAnswerHit(
        bool selfAlive,
        bool ghost,
        bool fromAlive,
        bool fromIsPerson,
        bool fromIsEnemy
    ) => selfAlive && !ghost && fromAlive && (fromIsPerson || fromIsEnemy);

    public static bool MayRestoreTownStance(bool hasAliveFoe) => !hasAliveFoe;

    public static bool MayTakeGate(bool hasAliveFoe) => !hasAliveFoe;

    /// <summary>
    /// A person without its arms (<see cref="Skills.SpareKit.Armed"/>) runs from a person who hits
    /// it, as a player raised naked ran for its bank: it stood with its fists up and died, and 491
    /// of 1,354 break-offs in one run were dead within thirty seconds.
    /// </summary>
    public static bool RunsBare(bool foeIsPerson, bool armed) => foeIsPerson && !armed;

    /// <summary>A blow this recent, from an attacker still in sight, means the person is under attack.</summary>
    public static readonly TimeSpan UnderAttackWindow = TimeSpan.FromSeconds(10);

    /// <summary>Why a social job waits while the person is under attack, for the logs.</summary>
    public const string UnderAttackWhy = "under attack";

    /// <summary>The last blow between the two landed inside <see cref="UnderAttackWindow"/>.</summary>
    public static bool FreshBlow(DateTime lastBlow, DateTime now) => now - lastBlow < UnderAttackWindow;

    /// <summary>
    /// No visit, tavern, stroll or sightseeing while under attack: the person fights, runs or
    /// heads home. A Chaos fighter hunted through Britain by an Order mage set off to visit a
    /// player and greeted a passer-by between the Explosions, and died ten seconds later.
    /// </summary>
    public static bool BarsLeisure(bool underAttack, RoutineFamily family) => underAttack && family == RoutineFamily.Leisure;
}
