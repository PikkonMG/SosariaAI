using SosariaAI.Configuration;

namespace SosariaAI.Skills;

/// <summary>
/// T2A animal lore: knowledge check on a nearby creature. Does not tame.
/// </summary>
public static class LoreRules
{
    public const string Kind = SkillKinds.Lore;
    public const int ReachTiles = 8;
    public const double PracticeMin = 0;
    public const double PracticeMax = 120;

    /// <summary>The engine lores a wild beast only from this skill up; below it, only a tame one.</summary>
    public const double WildLoreSkill = 100;

    /// <summary>The engine lores a beast that can never be tamed only from this skill up.</summary>
    public const double UntamableLoreSkill = 110;

    public const string NoSubjectWhy = "no beast in reach to study";

    /// <summary>
    /// The engine's own test for a lore target: an animal, monster or sea body, not a dead
    /// pet, and tame unless the skill is high enough for a wild one (a tamable one at 100,
    /// any at 110). A tamer studies its own pets, the way players worked the skill.
    /// </summary>
    public static bool MayLore(bool beastBody, bool deadPet, bool controlled, bool tamable, double lore) =>
        beastBody && !deadPet &&
        (controlled || lore >= WildLoreSkill && (tamable || lore >= UntamableLoreSkill));
}
