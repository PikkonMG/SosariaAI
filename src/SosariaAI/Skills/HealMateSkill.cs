using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A short side job: walk to a hurt party mate and heal it, a bandage next to it the way a
/// client puts one on, or a heal spell from a few tiles off for a mage. Done when the mate is
/// well, gone, or the tend limit passed; the goal loop then takes the standing order up again.
/// </summary>
public sealed class HealMateSkill(Serial patient) : Skill
{
    public const string NoPatientWhy = "nobody to heal";

    private SosariaCharacter _character;
    private Mobile _patient;
    private DateTime _started;

    public override string Name => SkillKinds.HealMate;

    /// <summary>Who this errand goes to heal, so a second healer is not sent after the first.</summary>
    public Serial Patient => patient;

    public override JobTarget? AimedAt =>
        _patient == null ? null : new JobTarget(_patient.Name ?? patient.ToString(), _patient.Location);

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _patient = World.FindMobile(patient);
        _started = Core.Now;

        if (!HealRules.MayBegin(character))
        {
            return CannotStart(NotInWorldReason);
        }

        return Reachable() || CannotStart(NoPatientWhy);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        if (!Reachable() || HealRules.TimedOut(Core.Now, _started) ||
            !HealRules.NeedsHeal(_patient.Hits, _patient.HitsMax, _patient.Poisoned))
        {
            _character.Motor.Stop();
            return SkillStatus.Done;
        }

        if (HealRules.InReach(_character.Location, _patient.Location))
        {
            _character.Motor.Stop();
            GameParty.TryHeal(_character, _patient);
            return SkillStatus.Running;
        }

        if (CombatBrain.CastHealOn(_character, _patient))
        {
            return SkillStatus.Running;
        }

        _character.Motor.MoveTo(_patient, HealRules.ReachTiles);
        return SkillStatus.Running;
    }

    public override void Abort() => _character?.Motor.Stop();

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);

    private bool Reachable() =>
        _patient is { Deleted: false, Alive: true } && _patient.Map == _character.Map;
}
