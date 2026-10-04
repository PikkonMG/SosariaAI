using System;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// Holds a place in the standing crowd at the person's bank for a long, visible stretch:
/// claims a role, walks to its seat, and does the role with real actions until the hold
/// ends. A member shoved off the bank floor walks back.
/// </summary>
public sealed class BankCrowdSkill : Skill
{
    private static readonly ILogger logger = SosariaLog.For(typeof(BankCrowdSkill));

    private SosariaCharacter _member;
    private BankCrowdSeat _seat;
    private Skill _walk;
    private BankCrowdAct _act;
    private DateTime _until;

    public override string Name => SkillKinds.BankCrowd;

    public override bool Begin(SosariaCharacter character)
    {
        Leave();
        _member = character;

        if (!Present(character))
        {
            return CannotStart(Skill.NotInWorldReason);
        }

        if (!BankCrowd.TryClaim(character, out _seat))
        {
            return CannotStart("no place in the bank crowd fits");
        }

        return WalkTo(_seat.Spot) || CannotStart("no walk to the bank");
    }

    public override SkillStatus Tick()
    {
        if (_seat == null || !Present(_member))
        {
            Leave();
            return Fail("left the bank crowd");
        }

        if (_walk != null)
        {
            if (_walk.Tick() == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;

            if (!BankCrowdRules.AtBank(_member.Location, _seat.Bank))
            {
                Leave();
                return Fail("the walk to the bank failed");
            }

            if (_act == null)
            {
                return Arrive();
            }
        }

        if (!BankCrowdRules.AtBank(_member.Location, _seat.Bank))
        {
            return WalkTo(_seat.Spot) ? SkillStatus.Running : Fail("pushed off the bank floor with no way back");
        }

        if (Core.Now >= _until || !_act.Tick(Core.Now))
        {
            return Finish(SkillStatus.Done);
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        Leave();
    }

    public override void Resume(TimeSpan held)
    {
        _until = SkillClock.Shift(_until, held);
        _walk?.Resume(held);
    }

    private bool WalkTo(Point3D spot)
    {
        _walk = new TravelSkill(spot, BankCrowdRules.SeatArrivalRange);

        if (_walk.Begin(_member))
        {
            return true;
        }

        _walk = null;
        Leave();
        return false;
    }

    private SkillStatus Arrive()
    {
        _seat.MarkArrived();
        _member.Home = _seat.Spot;
        _member.RangeHome = BankCrowdRules.SeatArrivalRange;
        _until = Core.Now + BankCrowdRules.HoldLength(Roll(), _member.PersonProfile.PhaseLengthMultiplier);
        _act = BankCrowdAct.For(_seat.Role);

        if (!_act.Start(_member, _seat))
        {
            var role = _seat.Role;
            _act = null;
            Leave();
            return Fail($"could not take up the {role} role");
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} joins the bank crowd at {Bank} as {Role} until {Until}",
                _member.Name,
                _seat.BankKey,
                _seat.Role,
                _until
            );
        }

        return SkillStatus.Running;
    }

    private SkillStatus Finish(SkillStatus status)
    {
        if (SosariaSettings.LogActivity && _seat != null)
        {
            logger.Information("{Name} leaves the bank crowd at {Bank} ({Role})", _member.Name, _seat.BankKey, _seat.Role);
        }

        Leave();
        return status;
    }

    private void Leave()
    {
        _act?.End();
        _act = null;
        _walk = null;

        if (_member != null)
        {
            BankCrowd.Release(_member);
        }

        _seat = null;
    }

    private static int Roll() => Utility.Random(int.MaxValue);

    private static bool Present(SosariaCharacter member) =>
        member is { Deleted: false, Alive: true } && People.InWorld(member);
}
