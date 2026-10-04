using System;
using Server;
using Server.Guilds;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// The leader's side of an Order or Chaos war band, or of a blue anti-PK sweep or Den raid (side
/// <see cref="GuildType.Regular"/>), see <see cref="PartyRoads"/>: ride to an open meeting spot
/// or PvP hot spot with the band behind, walk about it for a while, and set a course on any
/// enemy of the other side, or any red, seen out in the open, on the way or there. The rally
/// or the lawful draw in <see cref="WorldPlay"/> strikes once the two are close; the fight
/// ends the patrol and the band dissolves into it, and the scorer picks healing or home after.
/// A posse's patrol has a mark, the killer it rides after: it closes on the mark first when it
/// sees it, and for <see cref="FactionRules.TrackTime"/> it rides wherever the mark went.
/// </summary>
public sealed class FactionPatrolSkill : Skill
{
    public const string SkillName = "FactionPatrol";

    private SosariaCharacter _character;
    private TravelSkill _walk;
    private HomeRange _home;
    private DateTime _arrivedAt;
    private DateTime _nextScanAt;
    private Mobile _quarry;
    private readonly Mobile _mark;
    private Point3D _target;
    private DateTime _startedAt;

    public FactionPatrolSkill(FactionSpot spot, GuildType side, Mobile mark = null)
    {
        Spot = spot;
        Side = side;
        _mark = mark;
        _target = spot.Location;
    }

    public FactionSpot Spot { get; }

    public GuildType Side { get; }

    public override string Name => SkillName;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _home = HomeRange.Capture(character);
        _arrivedAt = default;
        _nextScanAt = default;
        _quarry = null;
        _startedAt = Core.Now;
        return StartWalk();
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !_character.Alive)
        {
            return Finish(SkillStatus.Failed);
        }

        var now = Core.Now;

        if (_arrivedAt != default && now - _arrivedAt >= FactionRules.PatrolTime)
        {
            return Finish(SkillStatus.Done);
        }

        if (now >= _nextScanAt)
        {
            _nextScanAt = now + FactionRules.PatrolScanGap;
            _quarry = Side == GuildType.Regular
                ? MarkInSight() ?? HotSpots.NearestRed(_character)
                : FactionWar.NearestQuarry(_character, Side);
        }

        if (_quarry is { Deleted: false, Alive: true } && _character.Motor.MoveTo(_quarry, FactionRules.EngageRange))
        {
            _walk?.Abort();
            _walk = null;
            return SkillStatus.Running;
        }

        _quarry = null;

        if (MarkMoved(now))
        {
            _walk?.Abort();
            _walk = null;
            _arrivedAt = default;
            _target = _mark.Location;
        }

        if (_arrivedAt == default)
        {
            return TickRide(now);
        }

        _character.Motor.LoiterInHome(IdleWanderSkill.WanderChanceToNotMove);
        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _home.Restore(_character);
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _arrivedAt = SkillClock.Shift(_arrivedAt, held);
        _startedAt = SkillClock.Shift(_startedAt, held);
    }

    // The ride to the spot; a chase that ended mid-ride takes up the ride again.
    private SkillStatus TickRide(DateTime now)
    {
        if (_walk == null && !StartWalk())
        {
            return Finish(SkillStatus.Failed);
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;

        if (status == SkillStatus.Failed)
        {
            return Finish(SkillStatus.Failed);
        }

        _arrivedAt = now;
        _character.Home = _target;
        _character.RangeHome = FactionRules.SpotRadius;
        return SkillStatus.Running;
    }

    private bool StartWalk()
    {
        _walk = new TravelSkill(_target, FactionRules.SpotRadius);

        if (_walk.Begin(_character))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    // The mark, alive and seen within intercept range out of the guards' reach.
    private Mobile MarkInSight() =>
        _mark is { Deleted: false, Alive: true } mark && mark.Map == _character.Map &&
        NavMetric.Chebyshev(_character.Location, mark.Location) <= FactionRules.InterceptRange &&
        _character.CanSee(mark) && !SosariaCharacter.UnderGuards(mark)
            ? mark
            : null;

    // While the posse tracks, a mark that moved well away from where it rides turns the ride.
    private bool MarkMoved(DateTime now) =>
        _mark is { Deleted: false, Alive: true } mark && mark.Map == _character.Map &&
        now - _startedAt < FactionRules.TrackTime &&
        NavMetric.Chebyshev(mark.Location, _target) > FactionRules.TrackTiles;

    private SkillStatus Finish(SkillStatus status)
    {
        _walk?.Abort();
        _walk = null;
        _home.Restore(_character);
        return status;
    }
}
