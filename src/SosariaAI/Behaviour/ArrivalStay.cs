using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// The person stays where the last step of the job left them: lingers or waits, or walks
/// straight on. The job plan then hands off to what the place offers. It works in any
/// town, so a traveller far from home stays in the town it came to see. A spot with no
/// room, such as a bank plaza that already holds its crowd, sends the person on to its
/// own corner or another place in the same town before it stays.
/// </summary>
public sealed class ArrivalStay : Skill
{
    private readonly LoiterStay _stay = new();
    private SosariaCharacter _character;
    private ArrivalStyle _style;
    private DateTime _started;
    private TimeSpan _length;

    public override string Name => SkillKinds.Arrive;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;

        if (!People.InWorld(character))
        {
            return CannotStart(Skill.NotInWorldReason);
        }

        _style = ArrivalRules.Style(Utility.Random(ArrivalRules.PercentScale));
        _length = ArrivalRules.Length(_style, Utility.Random(int.MaxValue));
        _started = Core.Now;

        if (!ArrivalRules.Stays(_style))
        {
            return true;
        }

        return _stay.Begin(character, character.Location, ArrivalRules.StandRadius, ArrivalRules.StandStayChance) ||
               CannotStart("no room to stand here");
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return Fail(Skill.LeftWorldReason);
        }

        if (!ArrivalRules.Stays(_style))
        {
            return SkillStatus.Done;
        }

        if (_stay.Tick(out var settled) == SkillStatus.Failed)
        {
            return Fail("the walk to a free spot failed");
        }

        if (settled || _stay.Walking)
        {
            _started = Core.Now;
            return SkillStatus.Running;
        }

        return Core.Now - _started >= _length ? SkillStatus.Done : SkillStatus.Running;
    }

    public override void Abort()
    {
        _stay.Abort();
        _character = null;
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _stay.Resume(held);
    }
}
