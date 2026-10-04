using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Practice Resisting Spells with a real self-cast curse. One try starts the words of power;
/// the next aims the finished curse at oneself.
/// </summary>
public sealed class ResistSkill : Skill
{
    private SosariaCharacter _character;
    private int _rotation;

    public override string Name => ResistRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        return People.InWorld(character) && SelfCurse.CanTrain(character);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character) || _character.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (SelfCurse.TryAimSelf(_character))
        {
            return SkillStatus.Done;
        }

        if (_character.Spell != null)
        {
            return SkillStatus.Running;
        }

        return SelfCurse.TryCast(_character, _rotation++) ? SkillStatus.Running : SkillStatus.Failed;
    }

    public override void Abort() => _character = null;
}
