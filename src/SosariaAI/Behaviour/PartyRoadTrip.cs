using System;
using Server;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// The leader's side of a road group (see <see cref="PartyRoads"/>): it stands where it called
/// while the mates walk over, up to <see cref="PartyRoadRules.MusterLimit"/>, and then sets out
/// on the trip with the group behind it. The trip begins only then, so a leader never recalls
/// out from under a group still on its way. It carries the trip's name, so a job plan moves on
/// when the trip ends.
/// </summary>
public sealed class PartyRoadTrip : Skill
{
    private readonly Skill _trip;
    private SosariaCharacter _leader;
    private DateTime _musterStarted;
    private bool _setOut;

    public PartyRoadTrip(Skill trip) => _trip = trip;

    public override string Name => _trip.Name;

    public override bool Begin(SosariaCharacter character)
    {
        _leader = character;
        _musterStarted = Core.Now;
        _setOut = false;
        return character is { Deleted: false, Alive: true };
    }

    public override SkillStatus Tick()
    {
        if (_setOut)
        {
            return _trip.Tick();
        }

        if (PartyRoads.Muster(_leader, _musterStarted, Core.Now) == PartyWaitResult.Waiting)
        {
            _leader.Motor.ClearMoveIntent();
            return SkillStatus.Running;
        }

        _setOut = true;
        return _trip.Begin(_leader) ? SkillStatus.Running : SkillStatus.Failed;
    }

    public override void Abort()
    {
        if (_setOut)
        {
            _trip.Abort();
        }

        _leader?.Motor.ClearMoveIntent();
    }

    public override void Resume(TimeSpan held)
    {
        _musterStarted = SkillClock.Shift(_musterStarted, held);

        if (_setOut)
        {
            _trip.Resume(held);
        }
    }
}
