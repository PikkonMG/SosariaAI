using System.Collections.Generic;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// T2A taste identification: detect poison in food or potion. Classic support skill. The
/// practice itself is a <see cref="PracticeSkillTable"/> kind for tasters only.
/// </summary>
public static class TasteRules
{
    /// <summary>The build skill that makes a person a taster.</summary>
    public const string TasterSkill = nameof(SkillName.TasteID);

    /// <summary>
    /// Only a person whose build trains Taste Identification tastes food. Every inn visit once
    /// held a taste step for everyone: "taste food" was the third most common pick of a 5.5 hour
    /// run, about 20,000 of 117,000 decisions, and nobody but the alchemists has the skill.
    /// </summary>
    public static bool IsTaster(IReadOnlyDictionary<string, double> buildSkills) =>
        buildSkills?.ContainsKey(TasterSkill) == true;

    public static bool MayBegin(SosariaCharacter character) =>
        People.InWorld(character) && IsTaster(character.Build?.Skills);
}
