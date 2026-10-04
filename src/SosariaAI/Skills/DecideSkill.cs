using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

public sealed class DecideSkill : Skill
{
    public const int GiveUpSeconds = 90;
    public static readonly TimeSpan GiveUp = TimeSpan.FromSeconds(GiveUpSeconds);

    private SosariaCharacter _character;
    private DateTime _started;

    public override string Name => SkillKinds.Decide;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _started = Core.Now;
        character.BeginDecide();
        return true;
    }

    public override SkillStatus Tick() => Outcome(_character.DecideResolved, Core.Now, _started);

    public override void Abort()
    {
    }

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);

    /// <summary>A dropped Decide event must not leave the character deciding for ever.</summary>
    public static SkillStatus Outcome(bool resolved, DateTime now, DateTime started)
    {
        if (resolved)
        {
            return SkillStatus.Done;
        }

        return now - started >= GiveUp ? SkillStatus.Failed : SkillStatus.Running;
    }
}
