using System;
using Server;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Sitting down to meditate: the engine's Meditation skill puts the body in a trance and the
/// mana comes back faster. A sitting ends when the mana is rested or after a while.
/// </summary>
public static class MeditateRules
{
    public const string Kind = SkillKinds.Meditate;
    public const int MinManaDeficit = 1;

    /// <summary>A sitting ends after this long, rested or not.</summary>
    public static readonly TimeSpan SitLimit = TimeSpan.FromMinutes(3);

    public static bool MayBegin(SosariaCharacter character) =>
        character is { Deleted: false, Alive: true } && People.InWorld(character);

    public static bool MayMeditate(int mana, int manaMax) =>
        manaMax - mana >= MinManaDeficit;

    public static bool IsRested(int mana, int manaMax) =>
        manaMax <= 0 || mana >= manaMax * SelfCareRules.RestedManaFraction;

    public static bool SatTooLong(DateTime now, DateTime started) => TimeRules.Passed(started, now, SitLimit);

    /// <summary>A sitting that ran out of time still worked when the mana rose.</summary>
    public static SkillStatus Outcome(int manaAtStart, int mana) =>
        mana > manaAtStart ? SkillStatus.Done : SkillStatus.Failed;
}
