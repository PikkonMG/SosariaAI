using System;
using Server;
using Server.Items;
using Server.SkillHandlers;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A pickpocket's work: pick a mark in the crowd, walk up (or hide and creep up), stand a moment
/// beside it, peek into the pack (a failed peek is tried again a few times, then it is a blind
/// grab or a walk-off), and lift with
/// the engine's Stealing skill. A clean lift is followed by a hide and a few quiet steps away,
/// then the take goes to the bank. A caught thief's mark shouts or calls the guards a moment
/// later, when the thief is still in their reach; the thief runs at once, hides once it has a
/// gap, and lays low until the flag lapses before it banks.
/// </summary>
public sealed class StealSkill : Skill
{
    // Eight compass ways: half of them is the way back.
    private const int DirectionHalfTurn = 4;
    private const int DirectionMask = 0x7;
    private const string GoldWord = "gold";

    private enum Phase
    {
        Approach,
        Case,
        Slip,
        Escape,
        LayLow,
        Bank
    }

    private readonly Mobile _chosenMark;

    private SosariaCharacter _thief;
    private Mobile _mark;
    private Item _loot;
    private Phase _phase;
    private DateTime _phaseStarted;
    private TimeSpan _caseFor;
    private Point3D _slipTo;
    private bool _sneaked;
    private bool _lifted;
    private int _peeks;
    private DateTime _nextPeekAt;
    private bool _looked;
    private bool _blind;
    private Skill _bank;

    /// <summary>A routine step: the thief picks its own mark when the step begins.</summary>
    public StealSkill()
    {
    }

    /// <summary>A thief that already picked out its mark in the crowd goes for that one.</summary>
    public StealSkill(Mobile mark) => _chosenMark = mark;

    public override string Name => SkillKinds.Steal;

    public override bool Begin(SosariaCharacter character)
    {
        _thief = character;
        _lifted = false;
        _bank = null;
        _peeks = 0;
        _nextPeekAt = default;
        _looked = false;
        _blind = false;

        if (!StealRules.MayBegin(character))
        {
            return CannotStart(StealRules.FlaggedReason);
        }

        _mark = ChosenMarkStillGood(character) ? _chosenMark : StealMarks.Find(character, out _loot);

        if (_mark == null)
        {
            return CannotStart(StealRules.NoMarkReason);
        }

        StealMarks.FreeHands(character);

        if (CanCreep(character) && !character.Hidden)
        {
            Server.Skills.UseSkill(character, SkillName.Hiding);
        }

        _sneaked = character.Hidden;
        Enter(Phase.Approach);
        return true;
    }

    public override SkillStatus Tick()
    {
        if (_thief is { Deleted: false, Alive: false })
        {
            return Fail(StealRules.DiedReason);
        }

        if (_thief == null || _thief.Deleted || !People.InWorld(_thief))
        {
            return SkillStatus.Failed;
        }

        return _phase switch
        {
            Phase.Approach => Approach(),
            Phase.Case => Case(),
            Phase.Slip => Slip(),
            Phase.Escape => Escape(),
            Phase.LayLow => LayLow(),
            _ => Bank()
        };
    }

    public override void Abort()
    {
        _bank?.Abort();
        _bank = null;
        _mark = null;
        _loot = null;
        _thief = null;
    }

    public override void Resume(TimeSpan held)
    {
        _bank?.Resume(held);
        _phaseStarted = SkillClock.Shift(_phaseStarted, held);
        _nextPeekAt = SkillClock.Shift(_nextPeekAt, held);
    }

    private bool ChosenMarkStillGood(SosariaCharacter thief)
    {
        if (_chosenMark == null || !StealMarks.IsMark(thief, _chosenMark) ||
            !thief.InRange(_chosenMark, StealRules.MarkRange))
        {
            return false;
        }

        _loot = StealRules.PickLoot(_chosenMark.Backpack, Stealing.MaxWeightToSteal);
        return _loot != null;
    }

    /// <summary>Only a thief the engine grants quiet steps creeps up hidden; any other would hide and stand revealed at its first step.</summary>
    private static bool CanCreep(SosariaCharacter thief) =>
        StealRules.ShouldCreep(
            thief.Skills.Hiding.Base,
            thief.Skills.Stealth.Value,
            Stealth.HidingRequirement,
            Stealth.GetArmorRating(thief)
        );

    private bool MarkGone() =>
        _mark is not { Deleted: false, Alive: true, Hidden: false } || _mark.Map != _thief.Map ||
        !_thief.InRange(_mark, StealRules.MarkRange * 2);

    private SkillStatus Approach()
    {
        if (MarkGone())
        {
            return GiveUp(StealRules.MarkGoneReason);
        }

        if (Core.Now - _phaseStarted >= StealRules.ApproachLimit)
        {
            return GiveUp(StealRules.ApproachTooLongReason);
        }

        if (StealRules.InReach(_thief.Location, _mark.Location))
        {
            _caseFor = RedGangRules.Between(StealRules.CaseMin, StealRules.CaseMax, Utility.RandomDouble());
            _thief.Motor.ClearMoveIntent();
            _thief.Direction = _thief.GetDirectionTo(_mark);
            Enter(Phase.Case);
            return SkillStatus.Running;
        }

        if (_thief.Hidden && _thief.AllowedStealthSteps <= 0)
        {
            Server.Skills.UseSkill(_thief, SkillName.Stealth);
        }

        _thief.Motor.MoveTo(_mark, StealRules.ReachTiles);
        return SkillStatus.Running;
    }

    // Beside the mark: a moment's pause, the peek, then the lift. A mark that walked off is followed.
    private SkillStatus Case()
    {
        if (MarkGone())
        {
            return GiveUp(StealRules.MarkGoneReason);
        }

        if (!StealRules.InReach(_thief.Location, _mark.Location))
        {
            Enter(Phase.Approach);
            return SkillStatus.Running;
        }

        var standing = Core.Now - _phaseStarted;

        if (standing < _caseFor)
        {
            return SkillStatus.Running;
        }

        // The skill's own delay can outlast the mark's patience: a lift that never comes is given up.
        if (standing >= StealRules.ApproachLimit)
        {
            return GiveUp(StealRules.NoLiftReason);
        }

        if (!_looked)
        {
            return Peek();
        }

        // A fight on the way may have put the blade back in hand; the skill waits out its own delay.
        StealMarks.FreeHands(_thief);

        if (!Server.Skills.UseSkill(_thief, SkillName.Stealing) || _thief.Target == null)
        {
            return SkillStatus.Running;
        }

        // No look: the engine takes a random thing from the pack.
        return Lift(_blind ? _mark : _loot);
    }

    /// <summary>
    /// One peek into the mark's pack, the engine's own: a skill check, a notice for anyone
    /// watching. A failed peek is tried again after a moment while peeks are left; then the
    /// thief grabs blind or walks off.
    /// </summary>
    private SkillStatus Peek()
    {
        if (Core.Now < _nextPeekAt)
        {
            return SkillStatus.Running;
        }

        Snooping.Container_Snoop(_mark.Backpack, _thief);
        _peeks++;

        if (StealRules.GotALook(_thief.Skills.Snooping.Value, Utility.Random(PercentRoll.Scale)))
        {
            _loot = StealRules.PickLoot(_mark.Backpack, Stealing.MaxWeightToSteal);
            _looked = true;
            return _loot == null ? GiveUp(StealRules.NothingWorthReason) : SkillStatus.Running;
        }

        if (StealRules.PeeksAgain(_peeks))
        {
            _nextPeekAt = Core.Now + StealRules.PeekGap;
            return SkillStatus.Running;
        }

        if (!StealRules.BlindGrab(Utility.Random(PercentRoll.Scale)))
        {
            return GiveUp(StealRules.NoLookReason);
        }

        _looked = true;
        _blind = true;
        return SkillStatus.Running;
    }

    private SkillStatus Lift(object target)
    {
        var wasCriminal = _thief.Criminal;
        var before = StealMarks.Tally(_mark.Backpack);
        _thief.Target.Invoke(_thief, target);
        var taken = StealMarks.Lifted(before, _mark, out var amount);
        var caught = !wasCriminal && _thief.Criminal;
        _lifted = taken != null;
        StealMarks.NoteTried(_thief, _mark, Core.Now);
        WorldPlay.Log(
            _lifted
                ? $"{_thief.Name} lifted {Describe(taken, amount)} from {_mark.Name} at {PlaceNames.Of(_thief)} ({CaughtWord(caught)})"
                : $"{_thief.Name} fumbled a lift on {_mark.Name} at {PlaceNames.Of(_thief)} ({CaughtWord(caught)})"
        );

        if (_lifted || caught)
        {
            ShardNews.Theft(_thief, _mark, caught);
        }

        if (caught)
        {
            VictimReacts();
            Enter(Phase.Escape);
            return SkillStatus.Running;
        }

        if (!_lifted)
        {
            return Fail(StealRules.MissedReason);
        }

        SlipAway();
        return SkillStatus.Running;
    }

    // The mark of a caught thief takes a moment to shout, the way a player types it.
    private void VictimReacts()
    {
        if (_mark is not SosariaCharacter { Deleted: false, Alive: true } victim ||
            !StealRules.VictimCalls(Utility.Random(PercentRoll.Scale)))
        {
            return;
        }

        var thief = _thief;
        Timer.StartTimer(
            RedGangRules.Between(StealRules.VictimCallMin, StealRules.VictimCallMax, Utility.RandomDouble()),
            () => VictimShouts(victim, thief)
        );
    }

    /// <summary>
    /// The guards for a thief still in their reach under them; the street for one that got clear
    /// of the guards or out of their reach.
    /// </summary>
    private static void VictimShouts(SosariaCharacter victim, SosariaCharacter thief)
    {
        if (victim is not { Deleted: false, Alive: true } || thief is not { Deleted: false, Alive: true } ||
            thief.Map != victim.Map)
        {
            return;
        }

        if (SosariaCharacter.UnderGuards(thief) &&
            StealRules.VictimReaches(NavMetric.Chebyshev(victim.Location, thief.Location)))
        {
            GuardCall.TryCall(victim, thief);
            return;
        }

        Talk.Say(victim, TalkCategory.ThiefVictim, new TalkSlots { Name = thief.Name });
    }

    // A clean lift reveals the thief: it hides again when it can and slips a few tiles off.
    private void SlipAway()
    {
        if (StealRules.HidesAfter(_sneaked, Utility.Random(PercentRoll.Scale)) && CanCreep(_thief))
        {
            Server.Skills.UseSkill(_thief, SkillName.Hiding);
        }

        var away = (Direction)(((int)_thief.GetDirectionTo(_mark) + DirectionHalfTurn) & DirectionMask);
        var x = _thief.X;
        var y = _thief.Y;
        Server.Movement.Movement.Offset(away, ref x, ref y, StealRules.SlipTiles);
        _slipTo = new Point3D(x, y, _thief.Map.GetAverageZ(x, y));
        Enter(Phase.Slip);
    }

    private SkillStatus Slip()
    {
        if (Core.Now - _phaseStarted >= StealRules.SlipLimit || _thief.InRange(_slipTo, StealRules.ReachTiles))
        {
            Enter(Phase.Bank);
            return SkillStatus.Running;
        }

        if (_thief.Hidden && _thief.AllowedStealthSteps <= 0)
        {
            Server.Skills.UseSkill(_thief, SkillName.Stealth);
        }

        _thief.Motor.MoveToPoint(_slipTo);
        return SkillStatus.Running;
    }

    private SkillStatus Escape()
    {
        var threat = Chaser() ?? (_mark is { Deleted: false, Alive: true } && _mark.Map == _thief.Map ? _mark : null);
        var gap = threat == null || NavMetric.Chebyshev(_thief.Location, threat.Location) >= StealRules.GapTiles;

        if (gap || Core.Now - _phaseStarted >= StealRules.EscapeLimit)
        {
            Enter(Phase.LayLow);
            return SkillStatus.Running;
        }

        _thief.SetRunPace();
        _thief.Motor.StepAwayFrom(threat);
        return SkillStatus.Running;
    }

    private SkillStatus LayLow()
    {
        if (!_thief.Criminal || Core.Now - _phaseStarted >= StealRules.LayLowLimit)
        {
            if (!_lifted)
            {
                return SkillStatus.Done;
            }

            Enter(Phase.Bank);
            return SkillStatus.Running;
        }

        if (!_thief.Hidden)
        {
            Server.Skills.UseSkill(_thief, SkillName.Hiding);
        }

        _thief.Motor.ClearMoveIntent();
        return SkillStatus.Running;
    }

    private SkillStatus Bank()
    {
        if (_bank == null)
        {
            _bank = new BankDepositSkill(_thief.HomeSpot);

            if (!_bank.Begin(_thief))
            {
                return SkillStatus.Done;
            }
        }

        return _bank.Tick();
    }

    // A mark that got away, hid, died, or held nothing worth it: the thief leaves it be a while.
    private SkillStatus GiveUp(string reason)
    {
        StealMarks.NoteTried(_thief, _mark, Core.Now);
        return Fail(reason);
    }

    /// <summary>Someone who has turned on the thief and is still after it.</summary>
    private Mobile Chaser()
    {
        foreach (var info in _thief.Aggressors)
        {
            if (info.Attacker is { Deleted: false, Alive: true } attacker && attacker.Combatant == _thief &&
                attacker.Map == _thief.Map)
            {
                return attacker;
            }
        }

        return null;
    }

    private static string Describe(Item item, int amount)
    {
        var name = item is Gold ? GoldWord : StealRules.ItemWord(item.Name ?? item.ItemData.Name ?? item.GetType().Name, amount);
        return amount > StealRules.SingleItem ? $"{amount} {name}" : name;
    }

    private static string CaughtWord(bool caught) => caught ? "caught" : "clean";

    private void Enter(Phase phase)
    {
        _phase = phase;
        _phaseStarted = Core.Now;
    }
}
