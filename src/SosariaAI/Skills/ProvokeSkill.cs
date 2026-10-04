using Server;
using Server.Mobiles;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Play in place. CheckSkill(Provocation); on success make two nearby mobiles fight.
/// </summary>
public sealed class ProvokeSkill : Skill
{
    private SosariaCharacter _bard;

    public override string Name => SkillKinds.Provoke;

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

        if (!FindPair(_bard, out var first, out var second))
        {
            return SkillStatus.Failed;
        }

        if (!InstrumentPlay.Try(_bard, instrument, SkillName.Provocation, ProvokeRules.PracticeMin, ProvokeRules.PracticeMax))
        {
            return SkillStatus.Failed;
        }

        first.Combatant = second;
        second.Combatant = first;
        return SkillStatus.Done;
    }

    public override void Abort() => _bard = null;

    /// <summary>
    /// Two creatures in the bard's range, and in range of each other, that the engine
    /// would let the bard set on each other. False when there is no such pair.
    /// </summary>
    public static bool FindPair(Mobile bard, out Mobile first, out Mobile second)
    {
        first = null;
        second = null;
        var range = MusicRules.BardRange(bard.Skills.Provocation.Value);

        foreach (var mobile in bard.GetMobilesInRange(range))
        {
            if (!IsTarget(bard, mobile))
            {
                continue;
            }

            if (first == null)
            {
                first = mobile;
            }
            else if (NavMetric.Chebyshev(first.Location, mobile.Location) <= range)
            {
                second = mobile;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Another creature in the bard's range and within <see cref="ProvokeRules.PartnerReachTiles"/>
    /// of <paramref name="foe"/> that the bard could set it on, or null.
    /// </summary>
    public static Mobile PartnerFor(Mobile bard, Mobile foe)
    {
        foreach (var mobile in bard.GetMobilesInRange(MusicRules.BardRange(bard.Skills.Provocation.Value)))
        {
            if (mobile != foe && IsTarget(bard, mobile) &&
                NavMetric.Chebyshev(foe.Location, mobile.Location) <= ProvokeRules.PartnerReachTiles)
            {
                return mobile;
            }
        }

        return null;
    }

    /// <summary>A mobile the engine would let this bard provoke.</summary>
    public static bool IsTarget(Mobile bard, Mobile mobile) =>
        mobile is { Deleted: false } &&
        ProvokeRules.IsProvokeTarget(
            mobile == bard,
            mobile is BaseCreature,
            mobile is BaseCreature { Controlled: true },
            mobile is BaseCreature { Unprovokable: true },
            bard.CanBeHarmful(mobile, false),
            Notoriety.Compute(bard, mobile) == Notoriety.Innocent
        );
}
