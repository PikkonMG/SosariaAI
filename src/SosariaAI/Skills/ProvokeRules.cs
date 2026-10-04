namespace SosariaAI.Skills;

/// <summary>
/// T2A provocation: CheckSkill 0-120, then set two mobiles in bard range
/// (<see cref="MusicRules.BardRange"/>) on each other.
/// </summary>
public static class ProvokeRules
{
    /// <summary>Tiles from the foe in which a fighting bard looks for a creature to set on it.</summary>
    public const int PartnerReachTiles = 8;

    public const double PracticeMin = 0;
    public const double PracticeMax = 120;

    /// <summary>
    /// The engine's provoke target: a creature, not the bard, not a pet, not unprovokable,
    /// one the bard may harm, and not an innocent the town would see provoked.
    /// </summary>
    public static bool IsProvokeTarget(
        bool isSelf,
        bool isCreature,
        bool controlled,
        bool unprovokable,
        bool canHarm,
        bool innocent
    ) =>
        !isSelf && isCreature && !controlled && !unprovokable && canHarm && !innocent;
}
