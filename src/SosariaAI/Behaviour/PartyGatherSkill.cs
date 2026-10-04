using SosariaAI.Mobiles;
using SosariaAI.Skills;

namespace SosariaAI.Behaviour;

/// <summary>
/// A leader who shouted for a group waits at the spot while people answer and walk over.
/// The step ends when the board stops recruiting for it; the leader's own dungeon or hunt
/// job then picks the next step and the group follows.
/// </summary>
public sealed class PartyGatherSkill : Skill
{
    public const string SkillName = "PartyGather";

    private SosariaCharacter _leader;

    public override string Name => SkillName;

    public override bool Begin(SosariaCharacter character)
    {
        _leader = character;
        return LfgBoard.IsRecruiting(character);
    }

    public override SkillStatus Tick()
    {
        if (_leader is not { Deleted: false, Alive: true } || !LfgBoard.IsRecruiting(_leader))
        {
            return SkillStatus.Done;
        }

        _leader.Motor.ClearMoveIntent();
        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _leader?.Motor.ClearMoveIntent();
        _leader = null;
    }
}
