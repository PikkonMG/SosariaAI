using Server;
using Server.SkillHandlers;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Practice Stealth with the engine's skills: hide, use Stealth, then creep a quiet step
/// around a small ring. Each try does the next thing the practice needs.
/// </summary>
public sealed class StealthSkill : Skill
{
    private const string CannotCreepReason = "too little hiding or too much armour to move quietly";
    private const string NoQuietStepsReason = "could not move quietly";

    private SosariaCharacter _thief;
    private Point3D _anchor;
    private int _ring;

    public override string Name => StealthRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _thief = character;

        if (!StealthRules.MayBegin(character, Stealth.HidingRequirement))
        {
            return CannotStart(CannotCreepReason);
        }

        if (_anchor == Point3D.Zero || !character.InRange(_anchor, StealthRules.RingRadius * 2))
        {
            _anchor = character.Location;
        }

        return true;
    }

    public override SkillStatus Tick()
    {
        if (!StealthRules.MayBegin(_thief, Stealth.HidingRequirement) || _thief.Deleted)
        {
            return Fail(CannotCreepReason);
        }

        if (!_thief.Hidden)
        {
            Server.Skills.UseSkill(_thief, SkillName.Hiding);
            return _thief.Hidden ? SkillStatus.Running : Fail(HideRules.NotHiddenReason);
        }

        if (_thief.AllowedStealthSteps <= 0 && Server.Skills.UseSkill(_thief, SkillName.Stealth) &&
            _thief.AllowedStealthSteps <= 0)
        {
            return Fail(NoQuietStepsReason);
        }

        return _thief.AllowedStealthSteps > 0 && StealthCreep.Step(_thief, _anchor, ref _ring)
            ? SkillStatus.Done
            : SkillStatus.Running;
    }

    public override void Abort() => _thief = null;
}
