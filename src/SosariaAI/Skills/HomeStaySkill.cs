using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// An owner's stay at home (see <see cref="HomeLifeRules"/>): it lets itself in and locks up
/// behind, puts away what it would bank, takes the supplies it runs short of, sets up its
/// trade's station, and rests behind the locked door while its wounds are tended. A friend
/// who comes to visit and stands at the door is let in, and the owner stays while the guest
/// is inside. Then it lets itself out and locks up.
/// </summary>
public sealed class HomeStaySkill : Skill
{
    public const string SkillName = "HomeStay";

    private static readonly Dictionary<Serial, DateTime> LastStays = new();

    private enum Leg
    {
        In,
        Inside,
        Out
    }

    private readonly HouseDoorway _way;
    private readonly HouseEntry _entry;
    private SosariaCharacter _character;
    private Leg _leg;
    private DateTime _insideSince;
    private TimeSpan _stay;

    public HomeStaySkill(HouseDoorway way)
    {
        _way = way;
        _entry = new HouseEntry(way, unlockedInside: true);
    }

    public override string Name => SkillName;

    /// <summary>True while the owner is inside its house.</summary>
    public bool Inside => _leg == Leg.Inside;

    /// <summary>When the character last went in at home; default when never since the boot.</summary>
    public static DateTime LastStay(SosariaCharacter character) => LastStays.GetValueOrDefault(character.Serial);

    /// <summary>True when the character stands inside its own house, staying there.</summary>
    public static bool AtHome(SosariaCharacter character) =>
        character.Routine?.CurrentSkill is HouseSkill { Stay.Inside: true };

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _leg = Leg.In;

        if (!_way.Stands || character.Map != _way.House.Map)
        {
            return CannotStart("the house is gone");
        }

        _way.HandKey(character);
        return _entry.BeginIn(character) || CannotStart("no walk to the door");
    }

    public override SkillStatus Tick()
    {
        if (_character is not { Deleted: false, Alive: true } || !_way.Stands)
        {
            return Fail("the house or its owner is gone");
        }

        return _leg switch
        {
            Leg.In => TickIn(),
            Leg.Inside => TickInside(),
            _ => _entry.TickOut()
        };
    }

    public override void Abort()
    {
        _entry.Abort();

        // Pulled out of its stay by a fight or a new plan, the owner leaves the door shut; it
        // is unlocked while the owner is in, so the next walk finds its way out.
        if (_leg == Leg.Inside && _character != null)
        {
            _way.Shut(_character);
        }
    }

    public override void Resume(TimeSpan held)
    {
        _entry.Resume(held);
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
        _stay = HomeLifeRules.StayLength(Utility.Random(int.MaxValue));
        LastStays[_character.Serial] = Core.Now;
        var chest = _way.ChestOrNew(_character);
        var stored = HouseChest.Stash(_character, chest);
        var taken = HouseChest.Restock(_character, chest);
        var furnished = HomeWorkshop.Furnish(_character, _way);
        WorldPlay.Log(
            $"{_character.Name} is home: put {stored} pieces away, took {taken} supplies, set up {furnished} station pieces"
        );
        return SkillStatus.Running;
    }

    private SkillStatus TickInside()
    {
        _character.Motor.ClearMoveIntent();
        var guestInside = LetGuestsIn();

        if (!HomeLifeRules.ReadyToLeave(
                Core.Now - _insideSince,
                _stay,
                Vitals.HitsFraction(_character),
                _character.Poisoned,
                guestInside
            ))
        {
            return SkillStatus.Running;
        }

        _leg = Leg.Out;
        WorldPlay.Log($"{_character.Name} leaves home after {(int)(Core.Now - _insideSince).TotalMinutes} minutes");
        return _entry.BeginOut(_character) ? SkillStatus.Running : Fail("could not get out");
    }

    // A friend on a visit to the owner stands at the door: the door opens for it. True while a
    // guest is inside. The door shuts once no guest waits at it.
    private bool LetGuestsIn()
    {
        var waiting = false;
        var inside = false;

        foreach (var mobile in _way.House.Map.GetMobilesInRange(_way.Front, HouseDoorway.FootprintTiles))
        {
            if (mobile is not SosariaCharacter guest || !VisitSkill.IsVisiting(guest, _character))
            {
                continue;
            }

            if (_way.House.IsInside(guest))
            {
                inside = true;
            }
            else if (NavMetric.Chebyshev(guest.Location, _way.Front) <= HomeLifeRules.GuestDoorTiles)
            {
                waiting = true;
            }
        }

        if (waiting)
        {
            _way.Open(_character);
        }
        else
        {
            _way.Shut(_character);
        }

        return inside;
    }
}
