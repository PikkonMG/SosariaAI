using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// One try of a plain practice kind from <see cref="PracticeSkillTable"/>: CheckSkill in the
/// kind's practice window where the person stands. A study kind fails with nobody else near.
/// Item Identification tries a real unidentified piece first (<see cref="ItemIdWork.Rep"/>).
/// </summary>
public sealed class PracticeSkill : Skill
{
    private readonly PracticeSkillRule _rule;
    private SosariaCharacter _character;

    public PracticeSkill(PracticeSkillRule rule) => _rule = rule;

    public override string Name => _rule.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return PracticeSkillTable.MayBegin(_rule, character);
    }

    public override SkillStatus Tick()
    {
        if (!PracticeSkillTable.MayBegin(_rule, _character) || _character.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (_rule.NeedsCompany && !HasCompany(_character))
        {
            return SkillStatus.Failed;
        }

        // Item Identification practice works on a real piece first: the person's own, or a customer's.
        if (_rule.Kind != SkillKinds.ItemId || !ItemIdWork.Rep(_character))
        {
            _character.CheckSkill(_rule.Skill, _rule.PracticeMin, _rule.PracticeMax);
        }

        return SkillStatus.Done;
    }

    public override void Abort() => _character = null;

    private static bool HasCompany(Mobile character)
    {
        foreach (var mobile in character.GetMobilesInRange(PracticeSkillTable.CompanyReachTiles))
        {
            if (mobile is { Deleted: false } && mobile != character)
            {
                return true;
            }
        }

        return false;
    }
}
