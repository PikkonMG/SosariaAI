using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Search nearby tiles. CheckSkill(Tracking); on success notice a nearby mobile.
/// </summary>
public sealed class TrackSkill : Skill
{
    private SosariaCharacter _tracker;

    public override string Name => SkillKinds.Track;

    public override bool Begin(SosariaCharacter character)
    {
        _tracker = character;
        return People.InWorld(character);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_tracker) || _tracker.Deleted)
        {
            return SkillStatus.Failed;
        }

        if (!_tracker.CheckSkill(SkillName.Tracking, TrackRules.PracticeMin, TrackRules.FirstCheckMax))
        {
            return SkillStatus.Failed;
        }

        var range = TrackRules.RangeFor(_tracker.Skills.Tracking.Value);
        Mobile found = null;

        foreach (var mobile in _tracker.GetMobilesInRange(range))
        {
            if (mobile is not { Deleted: false } || mobile == _tracker)
            {
                continue;
            }

            // Tracking smells a hiding beast but never sees through a person's Hiding.
            if (mobile is PlayerMobile && !_tracker.CanSee(mobile))
            {
                continue;
            }

            found = mobile;
            break;
        }

        if (found != null)
        {
            _tracker.CheckSkill(SkillName.Tracking, TrackRules.SecondCheckMin, TrackRules.PracticeMax);
        }

        return SkillStatus.Done;
    }

    public override void Abort() => _tracker = null;
}
