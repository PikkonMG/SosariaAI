namespace SosariaAI.Skills;

/// <summary>
/// T2A peacemaking: area CheckSkill 0-120, then clear Combatant in bard range
/// (<see cref="MusicRules.BardRange"/>).
/// </summary>
public static class PeaceRules
{
    public const double PracticeMin = 0;
    public const double PracticeMax = 120;
    public const double AreaPacifySeconds = 1;

    public static bool IsCombatTarget(
        bool hasCombatant,
        bool isSelf,
        bool uncalmable,
        bool areaPeaceImmune
    ) =>
        hasCombatant && !isSelf && !uncalmable && !areaPeaceImmune;
}
