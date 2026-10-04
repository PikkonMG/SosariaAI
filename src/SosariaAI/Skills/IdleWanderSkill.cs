using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// Wanders about the person's spot: its own corner of town for a copy, the authored
/// centre for a fixture, where it stands when it is far from home, or the next place with
/// room when that spot is full. It walks there by road, leaves a building it is shut in,
/// then strolls and stands in the ring. One told to idle <c>aroundHere</c> strolls round
/// where it stands and walks nowhere first: the fallback of a person whose walks to its
/// spot keep failing (Tancred walked for a Minoc corner off the roads 17 times in two minutes).
/// </summary>
public sealed class IdleWanderSkill : Skill
{
    /// <summary>The stay weight of a wanderer: short stands between strolls.</summary>
    public const int WanderChanceToNotMove = 3;

    private readonly Point3D _center;
    private readonly int _radius;
    private readonly TimeSpan _duration;
    private readonly bool _aroundHere;
    private readonly LoiterStay _stay = new();
    private SosariaCharacter _character;
    private DateTime _started;
    private BuildingLeave _leave;

    public IdleWanderSkill(Point3D center, int radius, TimeSpan duration, bool aroundHere = false)
    {
        _center = center;
        _radius = radius;
        _duration = duration;
        _aroundHere = aroundHere;
    }

    public override string Name => SkillKinds.IdleWander;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        var leash = HomeLeash.ConfiguredRadius();
        var idle = HomeLeash.IdleCenter(_center, character.HomeCorner, leash, WorkSites.IsCopy(character.CharacterId));
        var center = _aroundHere ? character.Location : HomeLeash.StandCenter(character.Location, idle, leash);

        if (!_stay.Begin(character, center, _radius, WanderChanceToNotMove))
        {
            return CannotStart("no walk to a spot with room");
        }

        _started = Core.Now - _character.IdleElapsed;
        _leave = null;

        if (_stay.Walking || WalkLine.Reaches(Standable.Walker(character.Map), character.Location, _stay.Spot))
        {
            return true;
        }

        _leave = new BuildingLeave();

        if (_leave.Begin(character))
        {
            return true;
        }

        _leave = null;
        return MayStayAfterLeaveFailed(IndoorTiles.IsBuilding(character.Map, character.X, character.Y, character.Z)) ||
               CannotStart("shut in a building with no way out");
    }

    /// <summary>
    /// A trapped indoor tile has no walk out. Idle there became the Britain inn pile:
    /// greet, rest, and never leave the floor.
    /// </summary>
    public static bool MayStayAfterLeaveFailed(bool isBuilding) => !isBuilding;

    public override SkillStatus Tick()
    {
        if (_leave != null)
        {
            var leave = _leave.Tick();

            if (leave == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _leave = null;
            _started = Core.Now;
        }

        if (_character.MustRunFrom(HuntSkill.SightThreat(_character)) && _character.MayStillFlee())
        {
            return Fail("a threat is in sight");
        }

        if (_stay.Tick(out var settled) == SkillStatus.Failed)
        {
            return Fail("the walk to its spot failed");
        }

        if (settled)
        {
            _started = Core.Now - _character.IdleElapsed;
        }

        if (_stay.Walking)
        {
            return SkillStatus.Running;
        }

        _character.IdleElapsed = Core.Now - _started;

        if (_character.IdleElapsed < _duration)
        {
            return SkillStatus.Running;
        }

        _character.IdleElapsed = TimeSpan.Zero;
        return SkillStatus.Done;
    }

    public override void Abort()
    {
        _leave?.Abort();
        _leave = null;
        _stay.Abort();
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _stay.Resume(held);
    }
}
