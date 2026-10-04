using System;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Opens a real gate and walks through it: home by default, or toward a goal for a party
/// leader, who holds it open a moment so the party can gather. The gate stays open its
/// engine lifetime for anyone else who wants the ride.
/// </summary>
public sealed class GateSkill : TravelCastSkill
{
    private const string WalkedThroughEvent = "walked through a gate";

    private readonly Point3D _goal;
    private readonly TimeSpan _hold;
    private DateTime _openedAt;

    public GateSkill()
    {
    }

    /// <summary>A gate toward <paramref name="goal"/>, held open <paramref name="hold"/> before the caster steps in.</summary>
    public GateSkill(Point3D goal, TimeSpan hold)
    {
        _goal = goal;
        _hold = hold;
    }

    public override string Name => GateRules.Kind;

    public override bool Begin(SosariaCharacter character)
    {
        _openedAt = default;
        return base.Begin(character);
    }

    public override void Resume(TimeSpan held)
    {
        base.Resume(held);
        _openedAt = SkillClock.Shift(_openedAt, held);
    }

    protected override bool BeginCast(SosariaCharacter character) =>
        character != null && GateRules.TryOpenToward(character, _goal == Point3D.Zero ? character.HomeSpot : _goal);

    protected override SkillStatus AfterCast()
    {
        if (_openedAt == default)
        {
            _openedAt = Core.Now;
        }

        if (Core.Now - _openedAt < _hold)
        {
            return SkillStatus.Running;
        }

        return GateTravel.StepIntoSpellGate(Character, TravelSpells.GateBeside(Character), WalkedThroughEvent) switch
        {
            GateStep.Through => SkillStatus.Done,
            GateStep.Waiting => SkillStatus.Running,
            _ => SkillStatus.Failed
        };
    }
}
