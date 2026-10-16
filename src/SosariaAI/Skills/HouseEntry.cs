using System;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// The walk into a house and back out, as a player with the key goes in: to the doorstep, the
/// door opened with the key in the pack, a step inside, and the door shut behind; out again,
/// the door opened, a step to the doorstep, and the door shut and locked behind. The engine's
/// own door and lock rules apply. An owner at home leaves its door unlocked while it is in, as
/// a player did: the shared walker takes a locked door for a wall, so any walk the owner sets
/// out on from inside finds its way out; a PvP base stays locked with its members inside.
/// </summary>
public sealed class HouseEntry
{
    private const int DoorstepTiles = 1;

    private enum Leg
    {
        ToDoor,
        Enter,
        Leave
    }

    private readonly HouseDoorway _way;
    private readonly bool _unlockedInside;
    private SosariaCharacter _character;
    private Skill _walk;
    private Leg _leg;

    public HouseEntry(HouseDoorway way, bool unlockedInside)
    {
        _way = way;
        _unlockedInside = unlockedInside;
    }

    /// <summary>Sets out for the doorstep. False when no walk there starts.</summary>
    public bool BeginIn(SosariaCharacter character)
    {
        _character = character;
        _leg = Leg.ToDoor;
        return StartWalk(new TravelSkill(_way.Front, DoorstepTiles));
    }

    /// <summary>Running on the way in; Done once inside with the door shut; Failed when the way in is shut.</summary>
    public SkillStatus TickIn()
    {
        if (_leg == Leg.ToDoor)
        {
            switch (_walk.Tick())
            {
                case SkillStatus.Running:
                    return SkillStatus.Running;
                case SkillStatus.Failed:
                    return SkillStatus.Failed;
            }

            _way.Open(_character);
            _leg = Leg.Enter;
            return StartWalk(new GoToSkill(_way.Inside, 0)) ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (_way.House.IsInside(_character))
        {
            _walk.Abort();
            _walk = null;
            _way.Shut(_character);
            _way.Door.Locked = !_unlockedInside;
            return SkillStatus.Done;
        }

        _way.Open(_character);
        return _walk.Tick() == SkillStatus.Failed ? SkillStatus.Failed : SkillStatus.Running;
    }

    /// <summary>Opens up and steps out to the doorstep. False when no step out starts.</summary>
    public bool BeginOut(SosariaCharacter character)
    {
        _character = character;
        _way.Open(_character);
        _leg = Leg.Leave;
        return StartWalk(new GoToSkill(_way.Front, 0));
    }

    /// <summary>Running on the way out; Done on the doorstep with the door shut behind; Failed when the way out is shut.</summary>
    public SkillStatus TickOut()
    {
        var status = _walk?.Tick() ?? SkillStatus.Failed;

        if (status == SkillStatus.Done)
        {
            _walk = null;
            _way.Shut(_character);
            _way.Door.Locked = true;
        }

        return status;
    }

    public void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public void Resume(TimeSpan held) => _walk?.Resume(held);

    private bool StartWalk(Skill walk)
    {
        _walk?.Abort();
        _walk = walk;
        return walk.Begin(_character);
    }
}
