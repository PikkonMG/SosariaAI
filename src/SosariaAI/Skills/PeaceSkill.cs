using System;
using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Play in place. CheckSkill(Peacemaking); on success calm combatants in bard range.
/// </summary>
public sealed class PeaceSkill : Skill
{
    private SosariaCharacter _bard;

    public override string Name => SkillKinds.Peace;

    public override bool Begin(SosariaCharacter character)
    {
        _bard = character;
        return People.InWorld(character);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_bard) || _bard.Deleted)
        {
            return SkillStatus.Failed;
        }

        var instrument = MusicRules.FindInstrument(_bard);

        if (instrument == null)
        {
            return SkillStatus.Failed;
        }

        if (!InstrumentPlay.Try(_bard, instrument, SkillName.Peacemaking, PeaceRules.PracticeMin, PeaceRules.PracticeMax))
        {
            return SkillStatus.Failed;
        }

        var range = MusicRules.BardRange(_bard.Skills.Peacemaking.Value);

        foreach (var mobile in _bard.GetMobilesInRange(range))
        {
            if (IsCalmable(_bard, mobile, range))
            {
                Calm(_bard, mobile, TimeSpan.FromSeconds(PeaceRules.AreaPacifySeconds));
            }
        }

        return SkillStatus.Done;
    }

    public override void Abort() => _bard = null;

    /// <summary>Someone in the bard's range is fighting and could be calmed: the song has work to do.</summary>
    public static bool HasFight(Mobile bard)
    {
        var range = MusicRules.BardRange(bard.Skills.Peacemaking.Value);

        foreach (var mobile in bard.GetMobilesInRange(range))
        {
            if (IsCalmable(bard, mobile, range))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A fighting mobile in <paramref name="range"/> of the bard that its song could calm.</summary>
    public static bool IsCalmable(Mobile bard, Mobile mobile, int range) =>
        mobile is { Deleted: false } &&
        PeaceRules.IsCombatTarget(
            mobile.Combatant != null,
            mobile == bard,
            mobile is BaseCreature { Uncalmable: true },
            mobile is BaseCreature { AreaPeaceImmune: true }
        ) &&
        NavMetric.Chebyshev(bard.Location, mobile.Location) <= range;

    /// <summary>Ends the mobile's fight; a creature stays calm for <paramref name="length"/>.</summary>
    public static void Calm(Mobile bard, Mobile mobile, TimeSpan length)
    {
        mobile.Combatant = null;
        mobile.Warmode = false;

        if (mobile is BaseCreature { BardPacified: false } creature)
        {
            creature.Pacify(bard, Core.Now + length);
        }
    }
}
