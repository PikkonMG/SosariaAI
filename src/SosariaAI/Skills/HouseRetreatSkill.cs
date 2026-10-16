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

    private enum Leg
    {
        In,
        Inside,
        Out,
        Back
    }

    private readonly HouseBase _home;
    private readonly HouseEntry _entry;
    private SosariaCharacter _character;
    private Skill _walk;
    private Leg _leg;
    private Point3D _returnTo;
    private DateTime _insideSince;

    public HouseRetreatSkill(HouseBase home)
    {
        _home = home;
        _entry = new HouseEntry(home.Way, unlockedInside: false);
    }

    public override string Name => SkillName;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _returnTo = character.Location;
        _leg = Leg.In;

        if (!_home.Way.Stands || character.Map != _home.House.Map)
        {
            return CannotStart("the house is gone");
        }

        _home.Way.HandKey(character);
        return _entry.BeginIn(character);
    }

    public override SkillStatus Tick()
    {
        if (_character is not { Deleted: false, Alive: true } || !_home.Way.Stands)
        {
            return Fail("the house or its keeper is gone");
        }

        return _leg switch
        {
            Leg.In => TickIn(),
            Leg.Inside => TickInside(),
            Leg.Out => TickOut(),
            _ => TickBack()
        };
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
        _insideSince = SkillClock.Shift(_insideSince, held);
    }

    private SkillStatus TickIn()
    {
        switch (_entry.TickIn())
        {
            case SkillStatus.Running:
                return SkillStatus.Running;
            case SkillStatus.Failed:
                return Fail("could not get inside");
        }

        _leg = Leg.Inside;
        _insideSince = Core.Now;
        var taken = HouseChest.Restock(_character, _home.Way.Chest());
        WorldPlay.Log($"{_character.Name} is inside {HouseBases.OwnerName(_home)}'s house and took {taken} things from the chest");
        return SkillStatus.Running;
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

        _leg = Leg.Out;
        return _entry.BeginOut(_character) ? SkillStatus.Running : Fail("could not get out");
    }

    private SkillStatus TickOut()
    {
        switch (_entry.TickOut())
        {
            case SkillStatus.Running:
                return SkillStatus.Running;
            case SkillStatus.Failed:
                return Fail("no way to the door");
        }

        Talk.Say(_character, TalkCategory.HouseReturn);
        WorldPlay.Log($"{_character.Name} came back out of {HouseBases.OwnerName(_home)}'s house to fight by {_home.Spot.Name}");
        _leg = Leg.Back;
        _walk = new TravelSkill(_returnTo, CharactersFile.DefaultGoToRange);
        return _walk.Begin(_character) ? SkillStatus.Running : SkillStatus.Done;
    }

    // Back outside, a walk that cannot finish still leaves the member out and ready.
    private SkillStatus TickBack() => _walk.Tick() == SkillStatus.Running ? SkillStatus.Running : SkillStatus.Done;
}
