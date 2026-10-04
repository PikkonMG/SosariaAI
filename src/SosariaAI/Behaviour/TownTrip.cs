using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Skill = SosariaAI.Skills.Skill;

namespace SosariaAI.Behaviour;

/// <summary>
/// The walk of a travel job: roads and public moongates to a bank in another town. It
/// carries its own step name, so the job plan moves on when the trip ends. The trip shuns
/// danger: a road past a place the walker ran from ends it, and a trip ended so, or stopped
/// by a threat on the way (<see cref="GiveUpForThreat"/>), rests its bank a while
/// (<see cref="TownTripRules.GivenUpRest"/>).
/// </summary>
public sealed class TownTrip : Skill
{
    private readonly TravelSkill _walk;
    private SosariaCharacter _character;

    public TownTrip(Point3D bankArrival) =>
        _walk = new TravelSkill(bankArrival, NavLimits.BankArrivalRange, shunsDanger: true);

    public override string Name => SkillKinds.Travel;

    /// <summary>The bank the trip walks to.</summary>
    public Point3D Goal => _walk.Goal;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk.ClearFailReason();
        return _walk.Begin(character) || CannotStart(_walk.FailReason);
    }

    public override SkillStatus Tick()
    {
        var status = _walk.Tick();

        if (status != SkillStatus.Failed)
        {
            return status;
        }

        if (_walk.FailReason == TravelSkill.DangerousRoadWhy)
        {
            RestBank(_character, Goal, Core.Now);
        }

        return Fail(_walk.FailReason);
    }

    public override void Abort() => _walk.Abort();

    public override void Resume(TimeSpan held) => _walk.Resume(held);

    /// <summary>
    /// A threat in sight stops what <paramref name="who"/> is doing. A town trip it stops rests
    /// its bank, so the next pick does not send the walker down the same road into the threat.
    /// </summary>
    public static void GiveUpForThreat(SosariaCharacter who, DateTime now)
    {
        if (who?.Routine?.CurrentSkill is TownTrip trip)
        {
            RestBank(who, trip.Goal, now);
        }

        who?.Routine?.AbortActive();
    }

    private static void RestBank(SosariaCharacter who, Point3D bank, DateTime now) =>
        who?.StartClock(RuleClock.TownTripGivenUp(bank), now);
}
