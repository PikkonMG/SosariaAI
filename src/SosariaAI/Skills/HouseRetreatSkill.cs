using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A PvP member ducks into its base (see <see cref="HouseBases"/>): it walks to the doorstep,
/// unlocks the door with its key, steps inside and shuts the door, takes what it runs short of
/// from the chest, and stands while its wounds are tended; healed, or after the stay limit, it
/// opens up, steps out, and walks back to where it left the fight. The engine's own door and
/// lock rules apply; the wounds are tended by the same care as anywhere out of a fight.
/// </summary>
public sealed class HouseRetreatSkill : Skill
{
    public const string SkillName = "HouseRetreat";

    private const int DoorstepTiles = 1;

    private enum Leg
    {
        ToDoor,
        Enter,
        Inside,
        Leave,
        Back
    }

    private readonly HouseBase _home;
    private SosariaCharacter _character;
    private Skill _walk;
    private Leg _leg;
    private Point3D _returnTo;
    private DateTime _insideSince;

    public HouseRetreatSkill(HouseBase home) => _home = home;

    public override string Name => SkillName;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _returnTo = character.Location;
        _leg = Leg.ToDoor;

        if (_home.House.Deleted || _home.Door.Deleted || character.Map != _home.House.Map)
        {
            return CannotStart("the house is gone");
        }

        HouseBases.HandKey(character, _home);
        return StartWalk(new TravelSkill(_home.Front, DoorstepTiles));
    }

    public override SkillStatus Tick()
    {
        if (_character is not { Deleted: false, Alive: true } || _home.House.Deleted || _home.Door.Deleted)
        {
            return Fail("the house or its keeper is gone");
        }

        return _leg switch
        {
            Leg.ToDoor => AfterWalk(EnterDoor),
            Leg.Enter => TickEnter(),
            Leg.Inside => TickInside(),
            Leg.Leave => AfterWalk(StepBack),
            _ => TickBack()
        };
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _insideSince = SkillClock.Shift(_insideSince, held);
    }

    private SkillStatus AfterWalk(Func<SkillStatus> next) =>
        _walk.Tick() switch
        {
            SkillStatus.Running => SkillStatus.Running,
            SkillStatus.Failed => Fail("no way to the door"),
            _ => next()
        };

    private SkillStatus EnterDoor()
    {
        OpenDoor();
        _leg = Leg.Enter;
        return StartWalk(new GoToSkill(_home.Inside, 0)) ? SkillStatus.Running : Fail("the door would not open");
    }

    private SkillStatus TickEnter()
    {
        if (_home.House.IsInside(_character))
        {
            _walk.Abort();
            ShutDoor();
            _leg = Leg.Inside;
            _insideSince = Core.Now;
            var taken = HouseBases.Restock(_character, _home);
            WorldPlay.Log($"{_character.Name} is inside {HouseBases.OwnerName(_home)}'s house and took {taken} things from the chest");
            return SkillStatus.Running;
        }

        OpenDoor();
        return _walk.Tick() == SkillStatus.Failed ? Fail("could not get inside") : SkillStatus.Running;
    }

    private SkillStatus TickInside()
    {
        _character.Motor.ClearMoveIntent();

        if (!HouseBaseRules.ReadyToReturn(
                Vitals.HitsFraction(_character),
                _character.Poisoned,
                Core.Now - _insideSince
            ))
        {
            return SkillStatus.Running;
        }

        OpenDoor();
        _leg = Leg.Leave;
        return StartWalk(new GoToSkill(_home.Front, 0)) ? SkillStatus.Running : Fail("could not get out");
    }

    private SkillStatus StepBack()
    {
        Talk.Say(_character, TalkCategory.HouseReturn);
        WorldPlay.Log($"{_character.Name} came back out of {HouseBases.OwnerName(_home)}'s house to fight by {_home.Spot.Name}");
        _leg = Leg.Back;
        return StartWalk(new TravelSkill(_returnTo, CharactersFile.DefaultGoToRange)) ? SkillStatus.Running : SkillStatus.Done;
    }

    // Back outside, a walk that cannot finish still leaves the member out and ready.
    private SkillStatus TickBack() => _walk.Tick() == SkillStatus.Running ? SkillStatus.Running : SkillStatus.Done;

    private bool StartWalk(Skill walk)
    {
        _walk?.Abort();
        _walk = walk;
        return walk.Begin(_character);
    }

    private void OpenDoor()
    {
        if (!_home.Door.Open)
        {
            _home.Door.Use(_character);
        }
    }

    private void ShutDoor()
    {
        if (_home.Door.Open)
        {
            _home.Door.Use(_character);
        }
    }
}
