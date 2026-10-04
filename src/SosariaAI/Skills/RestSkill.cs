using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

public sealed class RestSkill : Skill
{
    private readonly TimeSpan _duration;
    private SosariaCharacter _character;
    private DateTime _started;

    public RestSkill(TimeSpan duration) => _duration = duration;

    public override string Name => SkillKinds.Rest;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _started = Core.Now;
        character.Home = character.Location;
        character.RangeHome = CrowdSpread.LingerRadius;
        return true;
    }

    public override SkillStatus Tick()
    {
        _character.Motor.LoiterInHome(LoiterPaceRules.StandStill);

        if (_character.Hits >= _character.HitsMax || Core.Now - _started >= _duration)
        {
            return SkillStatus.Done;
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
    }

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);
}
