namespace SosariaAI.Combat;

/// <summary>A song a bard plays in a fight, or none.</summary>
public enum CombatSong
{
    None,

    /// <summary>Set the attacker on another monster near it.</summary>
    Provoke,

    /// <summary>Calm the attacker for a few seconds.</summary>
    Peace
}

/// <summary>
/// A bard fights with its songs as well as its blade. With the skill for it, it sets the
/// monster on it against another monster standing near, and when the fight goes badly it
/// calms the one on it for a breath, long enough to bandage or step away. One song, then a
/// rest before the next, the way a player waits on the skill timer. Pure.
/// </summary>
public static class BardSongRules
{
    /// <summary>Below this a bard's song fails too often to spend a fight on it.</summary>
    public const double MinSongSkill = 60;

    public const double CalmBaseSeconds = 4;
    public const double CalmSkillDivisor = 10;

    /// <summary>A bard waits between this and <see cref="MaxRestMs"/> after one song before the next.</summary>
    public const int MinRestMs = 8000;

    public const int MaxRestMs = 15000;

    /// <summary>
    /// A bard that found no song to sing looks again after this, not on every think: the
    /// look for a partner reads every mobile in the song's range.
    /// </summary>
    public const int NoSongRetryMs = 2000;

    /// <summary>Enough of either song to sing one in a fight.</summary>
    public static bool CanSing(double provocation, double peacemaking) =>
        provocation >= MinSongSkill || peacemaking >= MinSongSkill;

    /// <summary>
    /// Provoke when the skill is there, the engine lets the attacker be provoked, and another
    /// monster stands near it; else calm the attacker when the fight is going badly and the
    /// engine lets it be calmed. Only a creature that is on the bard, and only with an
    /// instrument in the pack. A tamed pet or an unprovokable boss is never set on another.
    /// </summary>
    public static CombatSong Pick(
        double provocation,
        double peacemaking,
        bool hasInstrument,
        bool attackerIsCreatureOnSelf,
        bool attackerProvocable,
        bool hasPartner,
        bool attackerCalmable,
        bool goingBadly
    )
    {
        if (!hasInstrument || !attackerIsCreatureOnSelf)
        {
            return CombatSong.None;
        }

        if (provocation >= MinSongSkill && attackerProvocable && hasPartner)
        {
            return CombatSong.Provoke;
        }

        return peacemaking >= MinSongSkill && attackerCalmable && goingBadly ? CombatSong.Peace : CombatSong.None;
    }

    /// <summary>A calmed attacker stays calm this long: four seconds and a tenth of the skill.</summary>
    public static double CalmSeconds(double peacemaking) => CalmBaseSeconds + peacemaking / CalmSkillDivisor;

    /// <summary>Losing the trade, below the line to start this fight, or more than one on the bard.</summary>
    public static bool GoingBadly(FightOutlook outlook, double hitsFraction, double startLine, int attackers) =>
        outlook == FightOutlook.Losing || hitsFraction < startLine || attackers > RetreatRules.SingleAttacker;
}
