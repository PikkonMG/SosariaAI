using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Hide in place with the engine's Hiding skill: the skill check, the ten second delay, the
/// "You have hidden yourself well" and the refusal while someone fights the hider.
/// </summary>
public sealed class HideSkill : Skill
{
    private const string AlreadyHiddenReason = "already hidden";

    private SosariaCharacter _thief;

    public override string Name => SkillKinds.Hide;

    public override bool Begin(SosariaCharacter character)
    {
        _thief = character;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        return !character.Hidden || CannotStart(AlreadyHiddenReason);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_thief) || _thief.Deleted)
        {
            return Fail(NotInWorldReason);
        }

        if (_thief.Hidden)
        {
            return Fail(AlreadyHiddenReason);
        }

        // The skill is still on its delay: this try waits for the next one.
        if (!Server.Skills.UseSkill(_thief, SkillName.Hiding))
        {
            return SkillStatus.Running;
        }

        return _thief.Hidden ? SkillStatus.Done : Fail(HideRules.NotHiddenReason);
    }

    public override void Abort() => _thief = null;
}
