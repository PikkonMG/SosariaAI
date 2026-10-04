using System;
using SosariaAI.Configuration;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A practice skill is one skill check. Run alone it ended on its first tick and the
/// character stood frozen until the next choice. A practice session keeps the person at
/// the spot for a few minutes, milling about, and uses the skill every few seconds the way
/// a player works a skill at the bank. Pure. No world objects.
/// </summary>
public static class PracticeRules
{
    /// <summary>A player's skill use has a ten second delay between tries.</summary>
    public const int RepIntervalSeconds = 10;

    public const int MinSessionSeconds = 120;
    public const int MaxSessionSeconds = 300;

    /// <summary>A practice try failed and gave no reason of its own.</summary>
    public const string NothingToWorkOnWhy = "the skill had nothing to work on";

    /// <summary>Tries that fail in a row before the session stops: the skill has nothing to work on.</summary>
    public const int FailuresBeforeStop = 3;

    /// <summary>Tiles a practising person may drift from where the session began.</summary>
    public const int MillRadius = 3;

    /// <summary>Chance weight to stand still on a mill tick. Most ticks the person stays put.</summary>
    public const int MillStayChance = 8;

    private const int InclusiveSpanPad = 1;

    public static readonly TimeSpan RepInterval = TimeSpan.FromSeconds(RepIntervalSeconds);

    /// <summary>Skills that are one check with nothing to walk to. They run as a practice session.</summary>
    public static bool IsPractice(string skillKind) =>
        skillKind is SkillKinds.Anatomy or SkillKinds.Archery or SkillKinds.ArmsLore or SkillKinds.Beg
            or SkillKinds.Camp or SkillKinds.DetectHidden or SkillKinds.Discord or SkillKinds.EvalInt
            or SkillKinds.Fence or SkillKinds.Forensic or SkillKinds.Herd or SkillKinds.Hide
            or SkillKinds.ItemId or SkillKinds.Lockpick or SkillKinds.Lore or SkillKinds.Mace
            or SkillKinds.Mage or SkillKinds.Meditate or SkillKinds.Music or SkillKinds.Necro
            or SkillKinds.Parry or SkillKinds.Peace or SkillKinds.Poison or SkillKinds.Provoke
            or SkillKinds.RemoveTrap or SkillKinds.Resist or SkillKinds.Spirit or SkillKinds.Stealth
            or SkillKinds.Sword or SkillKinds.Tactics or SkillKinds.Taste or SkillKinds.Track
            or SkillKinds.Vet or SkillKinds.Wrestle;

    /// <summary>A step moves the person and breaks the skill: meditation, hiding and a camp fire.</summary>
    public static bool StandsStill(string skillKind) =>
        skillKind is SkillKinds.Meditate or SkillKinds.Hide or SkillKinds.Stealth or SkillKinds.Camp;

    public static TimeSpan SessionLength(int roll) =>
        TimeSpan.FromSeconds(
            MinSessionSeconds + Math.Abs(roll % (MaxSessionSeconds - MinSessionSeconds + InclusiveSpanPad))
        );

    /// <summary>
    /// A session that worked the skill at least once is done when time runs out or the
    /// skill has nothing left to work on. One that never worked it failed.
    /// </summary>
    public static SkillStatus Outcome(int successes, int failuresInRow, TimeSpan elapsed, TimeSpan length)
    {
        if (failuresInRow < FailuresBeforeStop && elapsed < length)
        {
            return SkillStatus.Running;
        }

        return successes > 0 ? SkillStatus.Done : SkillStatus.Failed;
    }
}
