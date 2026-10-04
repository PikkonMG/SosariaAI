using Server;
using Server.Items;
using Server.Mobiles;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Bandage a hurt animal with a real bandage: walk beside it, use the bandage on it, and
/// stay while the engine's heal timer runs. The tamer's own pets come first. With no hurt
/// beast in reach or no bandage the tending does not start (<see cref="HasWork"/>): tamers
/// chose it at the bank with nothing to tend 267 times in 150 minutes.
/// </summary>
public sealed class VetSkill : Skill
{
    private readonly BaseCreature _chosen;
    private SosariaCharacter _vet;
    private BaseCreature _patient;
    private bool _applied;

    public VetSkill()
    {
    }

    public VetSkill(BaseCreature patient) => _chosen = patient;

    public override string Name => VetRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _vet = character;
        _applied = false;
        _patient = People.InWorld(character) ? _chosen ?? FindPatient(character) : null;

        if (_patient == null)
        {
            return CannotStart(VetRules.NoPatientWhy);
        }

        return character.Backpack?.FindItemByType<Bandage>() != null || CannotStart(VetRules.NoBandageWhy);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_vet) || _vet.Deleted)
        {
            return Fail(VetRules.AwayWhy);
        }

        if (_patient is not { Deleted: false, Alive: true } || _patient.Map != _vet.Map)
        {
            return Fail(VetRules.PatientGoneWhy);
        }

        if (_applied)
        {
            return VetBandage.IsTending(_vet, _patient) ? SkillStatus.Running : SkillStatus.Done;
        }

        if (!_vet.InRange(_patient, Bandage.Range))
        {
            return _vet.Motor.MoveTo(_patient, Bandage.Range) ? SkillStatus.Running : Fail(VetRules.NoWalkWhy);
        }

        _vet.Motor.ClearMoveIntent();
        _applied = VetBandage.TryApply(_vet, _patient);
        return _applied ? SkillStatus.Running : Fail(VetRules.BandageRefusedWhy);
    }

    public override void Abort()
    {
        _vet = null;
        _patient = null;
    }

    /// <summary>A bandage in the pack and a hurt beast in reach: the scorer asks it before it offers the tending.</summary>
    public static bool HasWork(SosariaCharacter vet) =>
        People.InWorld(vet) && vet.Backpack?.FindItemByType<Bandage>() != null && FindPatient(vet) != null;

    /// <summary>A hurt pet of the vet's own, else a hurt tame or passive animal in reach.</summary>
    private static BaseCreature FindPatient(SosariaCharacter vet)
    {
        BaseCreature other = null;

        foreach (var mobile in vet.GetMobilesInRange(VetRules.ReachTiles))
        {
            if (mobile is not BaseCreature { Deleted: false, Alive: true } creature ||
                !VetRules.MayVet(creature.Hits, creature.HitsMax, creature.Poisoned) ||
                !creature.Controlled && creature.FightMode != FightMode.None)
            {
                continue;
            }

            if (creature.ControlMaster == vet)
            {
                return creature;
            }

            other ??= creature;
        }

        return other;
    }
}
