using System;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Bind wounds: bandage, drink or cast on oneself until whole, or walk to someone hurt nearby
/// and bandage them. Every remedy is the engine's own, so the Healing skill, the bandage
/// timer, the potion delay and the spell's reagents all apply.
/// </summary>
public sealed class HealSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(HealSkill));

    private SosariaCharacter _healer;
    private Mobile _patient;
    private DateTime _started;

    public override string Name => SkillKinds.Heal;

    public override bool Begin(SosariaCharacter character)
    {
        _healer = character;
        _patient = null;
        _started = default;

        if (!HealRules.MayBegin(character) || character.IsGhost)
        {
            return false;
        }

        if (HealRules.NeedsHeal(character.Hits, character.HitsMax, character.Poisoned))
        {
            if (!CombatBrain.HasRemedy(character))
            {
                LogNoRemedy(character);
                return false;
            }

            _patient = character;
        }
        else
        {
            _patient = FindPatient(character);

            if (_patient == null || character.Backpack?.FindItemByType<Bandage>() == null)
            {
                return false;
            }
        }

        _started = Core.Now;
        return true;
    }

    public override SkillStatus Tick()
    {
        if (!HealRules.MayBegin(_healer) || _healer.IsGhost || _patient is not { Deleted: false, Alive: true })
        {
            return SkillStatus.Failed;
        }

        if (!HealRules.NeedsHeal(_patient.Hits, _patient.HitsMax, _patient.Poisoned))
        {
            return SkillStatus.Done;
        }

        if (HealRules.TimedOut(Core.Now, _started))
        {
            return HealRules.Outcome(Vitals.HitsFraction(_patient));
        }

        return _patient == _healer ? TendSelf() : TendOther();
    }

    public override void Abort()
    {
        _healer = null;
        _patient = null;
        _started = default;
    }

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);

    private SkillStatus TendSelf()
    {
        CombatBrain.TendWounds(_healer);

        if (Busy(_healer) || CombatBrain.HasRemedy(_healer))
        {
            return SkillStatus.Running;
        }

        return HealRules.Outcome(Vitals.HitsFraction(_healer));
    }

    private SkillStatus TendOther()
    {
        if (_patient.Map != _healer.Map || !_healer.InRange(_patient, HealRules.PatientRange))
        {
            return SkillStatus.Failed;
        }

        if (!HealRules.InReach(_healer.Location, _patient.Location))
        {
            return _healer.Motor.MoveTo(_patient, HealRules.ReachTiles) ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (CombatBrain.BandageOther(_healer, _patient) || Busy(_healer) ||
            _healer.Backpack?.FindItemByType<Bandage>() != null)
        {
            return SkillStatus.Running;
        }

        return HealRules.Outcome(Vitals.HitsFraction(_patient));
    }

    private static bool Busy(SosariaCharacter healer) =>
        BandageContext.GetContext(healer) != null || healer.Spell != null;

    private static Mobile FindPatient(SosariaCharacter healer)
    {
        foreach (var mobile in healer.GetMobilesInRange(HealRules.PatientRange))
        {
            if (mobile != healer && People.IsLivingPlayer(mobile) && healer.CanSee(mobile) &&
                !healer.IsEnemy(mobile) &&
                HealRules.MayTend(PkRules.IsRed(healer.Kills), WorldPlay.IsOutlaw(mobile)) &&
                HealRules.NeedsHeal(mobile.Hits, mobile.HitsMax, mobile.Poisoned))
            {
                return mobile;
            }
        }

        return null;
    }

    private static void LogNoRemedy(SosariaCharacter character)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} has no bandage, potion or heal spell to bind wounds", character.Name);
        }
    }
}
