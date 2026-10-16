using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// A walk that passes the owner's own front door. The shared walker treats a locked door as a
/// wall, so an owner going in walks to the doorstep and lets itself in with its key before it
/// walks on inside, and an owner going out lets itself out before it walks on. Any other walk
/// is a plain <see cref="TravelSkill"/>.
/// </summary>
public sealed class HomeWalk : Skill
{
    public const string SkillName = "HomeWalk";

    private readonly HouseEntry _entry;
    private readonly Point3D _target;
    private readonly int _range;
    private readonly bool _arrivalFloor;
    private readonly bool _goingIn;
    private SosariaCharacter _character;
    private Skill _walk;

    private HomeWalk(HouseDoorway way, Point3D target, int range, bool arrivalFloor, bool goingIn)
    {
        _entry = new HouseEntry(way, unlockedInside: true);
        _target = target;
        _range = range;
        _arrivalFloor = arrivalFloor;
        _goingIn = goingIn;
    }

    public override string Name => SkillName;

    /// <summary>The walk to <paramref name="target"/>: through the owner's door when it lies on the other side of it.</summary>
    public static Skill To(SosariaCharacter character, Point3D target, int range, bool arrivalFloor = false)
    {
        if (HouseVisit.OwnedHouse(character) is { } house && house.Map == character.Map &&
            HouseDoorway.Of(house) is { } way)
        {
            var inside = house.IsInside(character);
            var targetInside = house.IsInside(target, PersonBody.Height);

            if (inside != targetInside)
            {
                return new HomeWalk(way, target, range, arrivalFloor, targetInside);
            }
        }

        return new TravelSkill(target, range, arrivalFloor: arrivalFloor);
    }

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        return _goingIn ? _entry.BeginIn(character) : _entry.BeginOut(character);
    }

    public override SkillStatus Tick()
    {
        if (_walk != null)
        {
            return _walk.Tick();
        }

        var door = _goingIn ? _entry.TickIn() : _entry.TickOut();

        if (door != SkillStatus.Done)
        {
            return door;
        }

        _walk = _goingIn ? new GoToSkill(_target, _range) : new TravelSkill(_target, _range, arrivalFloor: _arrivalFloor);
        return _walk.Begin(_character) ? SkillStatus.Running : SkillStatus.Failed;
    }

    public override void Abort()
    {
        _entry.Abort();
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held)
    {
        _entry.Resume(held);
        _walk?.Resume(held);
    }
}
