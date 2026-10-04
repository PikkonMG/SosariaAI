namespace SosariaAI.Combat;

/// <summary>
/// Hostiles in sight interrupt the current action. Free. No model.
/// </summary>
public static class DangerRules
{
    /// <summary>
    /// A fighter hit by a foe fights back up to this many times what it would dare start
    /// </summary>
    public const double FightBackMultiple = 1.5;

    public static bool MustFlee(CharacterRole role, int threat) =>
        role == CharacterRole.Worker && threat >= ThreatRating.MinThreatToFlee;

    public static bool IsIntolerable(
        int power,
        int threat,
        double hitsFraction,
        bool hasHealing,
        int alliesPower,
        double threatMultiple
    ) =>
        threat > 0 &&
        !ThreatRating.ShouldEngage(
            power,
            threat,
            hitsFraction,
            hasHealing,
            alliesPower,
            threatMultiple,
            ThreatRating.OpenFightHitsFraction,
            alreadyAttacked: false
        );

    /// <summary>
    /// A fighter already hit answers the blow: a fight it did not choose is not declined on
    /// paper. It runs at once only from a foe far past what it dares, or when it is already
    /// below the fighter's hit line. Once it fights, the fight's trend decides whether it stays.
    /// </summary>
    public static bool ShouldFight(
        int power,
        int threat,
        double hitsFraction,
        bool hasHealing,
        int alliesPower,
        double threatMultiple
    ) =>
        ThreatRating.ShouldEngage(
            power,
            threat,
            hitsFraction,
            hasHealing,
            alliesPower,
            threatMultiple * FightBackMultiple,
            RetreatRules.FighterLine,
            alreadyAttacked: false
        );

    /// <summary>
    /// A foe past <see cref="FightBackMultiple"/> of what this character dares, friends in the
    /// party counted, is not fought even at full health: the character breaks off. A hunt
    /// answered every attacker, and a hunter hit by a dragon traded blows with it.
    /// </summary>
    public static bool Overwhelms(int foeThreat, int dare, int alliesPower, double threatMultiple) =>
        foeThreat > (dare + alliesPower * ThreatRating.PartyPowerShare) * threatMultiple * FightBackMultiple;
}
