namespace SosariaAI.Combat;

/// <summary>
/// How a party fights as one, as 1999 groups did: the strongest melee fighter is the tank and
/// pulls first, and the others take up the tank's foe, so the party does not split onto many
/// monsters. A member still answers a foe on itself or on a mate; an archer or a mage with a
/// foe on it deals with that foe first. Without it every member of a hunting party picked its
/// own foe by its own scan, and a bot leader's attack order named only the leader's foe. Pure.
/// </summary>
public static class PartyTacticsRules
{
    /// <summary>A party of fewer has nobody to tank for.</summary>
    public const int MinPartySize = 2;

    /// <summary>A mate farther than this is not in this fight: the helpers' answer range (<see cref="AssistRules.HelpRange"/>).</summary>
    public const int TankRange = AssistRules.HelpRange;

    /// <summary>
    /// A mate can tank when it is alive, on this map within <see cref="TankRange"/>, fights up
    /// close, has a fighting build (a worker only runs), and is not running.
    /// </summary>
    public static bool CanTank(bool alive, bool sameMap, int distance, bool melee, bool fighter, bool fleeing) =>
        alive && sameMap && distance <= TankRange && melee && fighter && !fleeing;

    /// <summary>
    /// The stronger of two tanks by its power once healed (<see cref="CharacterPower.Healthy"/>),
    /// the lower serial on a tie, so every member names the same tank and a hurt tank does not
    /// hand the role round mid-fight.
    /// </summary>
    public static bool StrongerTank(int power, uint serial, int bestPower, uint bestSerial) =>
        power > bestPower || power == bestPower && serial < bestSerial;

    /// <summary>
    /// A member takes up the tank's foe when that foe is in its sight and its own foe is not on
    /// it, and nothing is close on it (<see cref="RoomSurvey.CloseTiles"/>): an archer shoots the
    /// monster on a party mate only while nothing is in melee with it.
    /// </summary>
    public static bool TakesTankTarget(bool tankFoeInSight, bool ownFoeOnSelf, int closeAttackers) =>
        tankFoeInSight && !ownFoeOnSelf && closeAttackers == 0;

    /// <summary>
    /// A member that is not the tank opens no fresh fight while a ready tank is near: the tank
    /// pulls first. A foe already on the member or on a mate (<see cref="FocusRules.FriendAttackerTier"/>
    /// and up) is fought. A tank is ready when it hunts, or for a person, when it is in war mode;
    /// a tank that is neither never pulls, and waiting on it would leave the party standing.
    /// </summary>
    public static bool WaitsForTank(bool tankReady, int foeTier) => tankReady && foeTier < FocusRules.FriendAttackerTier;
}
