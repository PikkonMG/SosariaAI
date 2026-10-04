using System;
using Server;
using SosariaAI.Combat;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Sit and meditate through the engine's Meditation skill until the mana is back. The hands
/// are emptied for the trance and filled again when the sitting ends.
/// </summary>
public sealed class MeditateSkill : Skill
{
    private SosariaCharacter _mage;
    private DateTime _started;
    private int _manaAtStart;

    public override string Name => MeditateRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _mage = character;

        if (!MeditateRules.MayBegin(character) || !MeditateRules.MayMeditate(character.Mana, character.ManaMax))
        {
            return false;
        }

        _started = Core.Now;
        _manaAtStart = character.Mana;
        return true;
    }

    public override SkillStatus Tick()
    {
        if (!MeditateRules.MayBegin(_mage))
        {
            return End(SkillStatus.Failed);
        }

        if (MeditateRules.IsRested(_mage.Mana, _mage.ManaMax))
        {
            return End(SkillStatus.Done);
        }

        if (MeditateRules.SatTooLong(Core.Now, _started))
        {
            return End(MeditateRules.Outcome(_manaAtStart, _mage.Mana));
        }

        CombatBrain.KeepMeditating(_mage);
        return SkillStatus.Running;
    }

    public override void Abort()
    {
        CombatBrain.EndMeditation(_mage);
        _mage = null;
    }

    public override void Resume(TimeSpan held) => _started = SkillClock.Shift(_started, held);

    private SkillStatus End(SkillStatus status)
    {
        CombatBrain.EndMeditation(_mage);
        return status;
    }
}
