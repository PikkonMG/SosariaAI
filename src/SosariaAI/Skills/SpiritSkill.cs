using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Channel spirits in place. CheckSkill(SpiritSpeak); on success Done.
/// </summary>
public sealed class SpiritSkill : Skill
{
    private SosariaCharacter _speaker;

    public override string Name => SpiritRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _speaker = character;
        return People.InWorld(character);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_speaker) || _speaker.Deleted)
        {
            return SkillStatus.Failed;
        }

        _speaker.CheckSkill(
            SkillName.SpiritSpeak,
            SpiritRules.PracticeMin,
            SpiritRules.PracticeMax
        );

        return SkillStatus.Done;
    }

    public override void Abort() => _speaker = null;
}
