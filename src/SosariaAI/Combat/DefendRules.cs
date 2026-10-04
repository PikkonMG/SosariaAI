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
    /// combat brain stands it at bay when it is cornered or the blows barely hurt.
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
}
