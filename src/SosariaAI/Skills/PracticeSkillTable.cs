using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// One plain practice kind: the skill it checks, the engine's practice window, and the
/// conditions that are the kind's own. <paramref name="NeedsCompany"/>: the skill studies
/// someone, so another mobile must stand in <see cref="PracticeSkillTable.CompanyReachTiles"/>.
/// <paramref name="TrainedOnly"/>: only a build that trains the skill uses it
/// (<see cref="Combat.ResolvedBuild.Trains"/>).
/// </summary>
public readonly record struct PracticeSkillRule(
    string Kind,
    SkillName Skill,
    double PracticeMin,
    double PracticeMax,
    bool NeedsCompany = false,
    bool TrainedOnly = false
);

/// <summary>
/// The plain practice kinds: each is one skill check where the person stands, worked as a
/// practice session. Weapon, lore, camp, music and taste practice do not need a target,
/// gear or an instrument; anatomy and evaluating intelligence study a nearby mobile.
/// Necromancy is Age of Shadows only (<see cref="Behaviour.EraRules"/> filters it).
/// Fighting and taste practice are for a build that trains the skill. Every inn visit once
/// held a taste step for everyone: "taste food" was the third most common pick of a 5.5 hour
/// run, about 20,000 of 117,000 decisions, and nobody but the alchemists has the skill. A mage
/// copied from a fighter's template keeps that template's routines, and with them its mace,
/// sword and archery practice at the bank.
/// </summary>
public static class PracticeSkillTable
{
    /// <summary>Tiles in which a study skill (anatomy, evaluating intelligence) finds someone to study.</summary>
    public const int CompanyReachTiles = 8;

    public const double PracticeFloor = 0;

    /// <summary>The engine's usual practice cap.</summary>
    public const double ClassicCap = 100;

    /// <summary>Evaluating intelligence and musicianship check up to 120.</summary>
    public const double ExtendedCap = 120;

    /// <summary>Necromancy practice is Curse Weapon, 0 to 40. No live spell is cast.</summary>
    public const double CurseWeaponCap = 40;

    private static readonly Dictionary<string, PracticeSkillRule> Rules = Build(
        new PracticeSkillRule(SkillKinds.Anatomy, SkillName.Anatomy, PracticeFloor, ClassicCap, NeedsCompany: true),
        new PracticeSkillRule(SkillKinds.Archery, SkillName.Archery, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.ArmsLore, SkillName.ArmsLore, PracticeFloor, ClassicCap),
        new PracticeSkillRule(SkillKinds.Camp, SkillName.Camping, PracticeFloor, ClassicCap),
        new PracticeSkillRule(SkillKinds.EvalInt, SkillName.EvalInt, PracticeFloor, ExtendedCap, NeedsCompany: true),
        new PracticeSkillRule(SkillKinds.Fence, SkillName.Fencing, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.Forensic, SkillName.Forensics, PracticeFloor, ClassicCap),
        new PracticeSkillRule(SkillKinds.Herd, SkillName.Herding, PracticeFloor, ClassicCap),
        new PracticeSkillRule(SkillKinds.ItemId, SkillName.ItemID, PracticeFloor, ClassicCap),
        new PracticeSkillRule(SkillKinds.Mace, SkillName.Macing, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.Music, SkillName.Musicianship, PracticeFloor, ExtendedCap),
        new PracticeSkillRule(SkillKinds.Necro, SkillName.Necromancy, PracticeFloor, CurseWeaponCap),
        new PracticeSkillRule(SkillKinds.Parry, SkillName.Parry, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.Sword, SkillName.Swords, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.Tactics, SkillName.Tactics, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.Taste, SkillName.TasteID, PracticeFloor, ClassicCap, TrainedOnly: true),
        new PracticeSkillRule(SkillKinds.Wrestle, SkillName.Wrestling, PracticeFloor, ClassicCap, TrainedOnly: true)
    );

    /// <summary>The kinds only a build that trains their skill practises (<see cref="PracticeSkillRule.TrainedOnly"/>).</summary>
    public static readonly string[] TrainedOnlyKinds = [.. Rules.Values.Where(rule => rule.TrainedOnly).Select(rule => rule.Kind)];

    public static bool TryGet(string kind, out PracticeSkillRule rule) => Rules.TryGetValue(kind, out rule);

    /// <summary>True when only a build that trains <paramref name="skill"/> practises it (<see cref="PracticeSkillRule.TrainedOnly"/>).</summary>
    public static bool IsTrainedOnly(SkillName skill) => Rules.Values.Any(rule => rule.TrainedOnly && rule.Skill == skill);

    /// <summary>The person is in the world and, for a trained-only kind, its build trains the skill.</summary>
    public static bool MayBegin(PracticeSkillRule rule, SosariaCharacter character) =>
        People.InWorld(character) && (!rule.TrainedOnly || character.Build?.Trains(rule.Skill) == true);

    private static Dictionary<string, PracticeSkillRule> Build(params PracticeSkillRule[] rules)
    {
        var table = new Dictionary<string, PracticeSkillRule>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            table.Add(rule.Kind, rule);
        }

        return table;
    }
}
