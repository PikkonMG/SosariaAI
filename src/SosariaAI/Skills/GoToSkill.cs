using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Walks a chain of legs to a target. ModernUO's pathfinder searches a box about 38 tiles
/// wide, so a long trip is split into legs short enough for it; each leg is one waypoint.
/// Goals are boxed once, because the PathFollower keeps its path only for the same object.
/// A stuck leg climbs <see cref="WalkRecoveryRules"/>, whose second spell walks the leg on
/// the walker's own steps. A final goal beside the walker on another floor is reached the
/// same way by <see cref="FloorSteps"/>; a goal on the roof over a walled tile no step
/// reaches is reached beside it, as close as a person stands. The motor walks each leg round
/// the armed traps near its way (<see cref="TrapDetour"/>). A tamer's walk stands while its
/// pets catch up (<see cref="PetKeeper.HoldsForPets"/>).
/// </summary>
public sealed class GoToSkill : Skill
{
    private readonly IPoint3D[] _legs;
    private readonly int _range;
    private readonly bool _floorAtEnd;
    private readonly ApproachProgress _progress = new();
    private SosariaCharacter _character;
    private int _leg;
    private DateTime _started;
    private Point3D _lastSeen;
    private int _frozenTicks;
    private IReadOnlyList<Direction> _ownSteps;
    private int _ownStep;
    private bool _steppingAround;
    private bool _steppedAround;

    public const int GiveUpMinutes = 5;
    public static readonly TimeSpan GiveUp = TimeSpan.FromMinutes(GiveUpMinutes);

    /// <summary>
    /// Clear <paramref name="floorAtEnd"/> when the last leg is not the trip's final goal:
    /// a graph node or a tile route to one. Its floor is then not checked.
    /// </summary>
    public GoToSkill(IPoint3D goal, int range, bool floorAtEnd = true) : this([goal], range, floorAtEnd)
    {
    }

    public GoToSkill(IReadOnlyList<IPoint3D> legs, int range, bool floorAtEnd = true)
    {
        _legs = new IPoint3D[legs.Count];

        for (var i = 0; i < legs.Count; i++)
        {
            _legs[i] = legs[i];
        }

        _range = range;
        _floorAtEnd = floorAtEnd;
    }

    public static GoToSkill FromPoints(IReadOnlyList<Point3D> points, int range, bool floorAtEnd = true)
    {
        var legs = new IPoint3D[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            legs[i] = points[i];
        }

        return new GoToSkill(legs, range, floorAtEnd);
    }

    public override string Name => SkillKinds.GoTo;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _leg = 0;
        StartLeg();
        return _legs.Length > 0 && _legs[0] != null && character is { Deleted: false, Map: not null } && character.Map != Map.Internal;
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !People.InWorld(_character))
        {
            return SkillStatus.Failed;
        }

        if (TimeUp(Core.Now, _started))
        {
            return SkillStatus.Failed;
        }

        // A tamer stands while its pets catch up: no step is due, and no stall is counted.
        if (PetKeeper.HoldsForPets(_character))
        {
            return SkillStatus.Running;
        }

        var goal = _legs[_leg];
        var isLast = _leg == _legs.Length - 1;
        var range = isLast ? _range : CharactersFile.DefaultGoToRange;
        var checkFloor = isLast && _floorAtEnd;

        if (WalkArrival.Arrived(_character.Location, goal, range, checkFloor))
        {
            if (isLast)
            {
                return SkillStatus.Done;
            }

            // Each leg gets its own clock. One skill holds a whole tile route, and a long
            // walk must not end because the first legs used up the time.
            _leg++;
            StartLeg();
            return SkillStatus.Running;
        }

        if (WalkArrival.NeedsFloorSteps(_character.Location, goal, range, checkFloor))
        {
            return TakeOwnSteps(goal, range, FloorSteps.Find, countSteps: false) ??
                   (ArrivalHeight.IsRoofOverBlockedTile(_character.Map, goal) ? SkillStatus.Done : SkillStatus.Failed);
        }

        if (!_steppingAround && Recover() == WalkRecovery.StepAround && !_steppedAround)
        {
            // Once a leg. The guard counts the steps left from here: a way through a
            // door can lead away from the leg before it turns back.
            _steppedAround = true;
            _steppingAround = true;
            _ownSteps = null;
            _progress.Reset();
        }

        if (_steppingAround)
        {
            if (TakeOwnSteps(goal, range, FloorSteps.Around, countSteps: true) is { } stepped)
            {
                return stepped;
            }

            // No step list reaches the leg: the engine ladder carries on to its end.
            _steppingAround = false;
        }

        _ownSteps = null;
        _character.Motor.MoveToPoint(goal, range);

        if (isLast && WalkArrival.Arrived(_character.Location, goal, range, checkFloor))
        {
            return SkillStatus.Done;
        }

        return _progress.Observe(_character.GetDistanceToSqrt(goal)) ? SkillStatus.Failed : SkillStatus.Running;
    }

    private void StartLeg()
    {
        _progress.Reset();
        _started = Core.Now;
        _lastSeen = _character.Location;
        _frozenTicks = 0;
        _ownSteps = null;
        _steppingAround = false;
        _steppedAround = false;
    }

    /// <summary>
    /// One rung of the stuck ladder, climbed from how long the walker stood and failed to
    /// close in. The step-around rung is the caller's to take.
    /// </summary>
    private WalkRecovery Recover()
    {
        var at = _character.Location;
        _frozenTicks = at == _lastSeen ? _frozenTicks + 1 : 0;
        _lastSeen = at;
        var motor = _character.Motor;
        var rung = WalkRecoveryRules.Next(_frozenTicks, _progress.TicksWithoutProgress);

        switch (rung)
        {
            case WalkRecovery.StepAside:
                motor.StepAside();
                break;
            case WalkRecovery.Replan:
                motor.RestartPath();
                break;
            case WalkRecovery.NudgeAndReplan:
                motor.StepAside();
                motor.RestartPath();
                break;
            case WalkRecovery.StepAround:
                motor.ClearMoveIntent();
                motor.RestartPath();
                break;
        }

        return rung;
    }

    /// <summary>
    /// One step of the walker's own step list to the leg: up or down a stair to the goal's
    /// floor, or round the shut door the engine path will not plan through. The list never
    /// steps onto a tile an armed trap hurts, through an open door's leaf or round a doorway
    /// corner (<see cref="Standable.TryStep"/>). The character's step opens a door ahead. A
    /// list that meets a door that stays shut is searched again. Null when
    /// <paramref name="find"/> finds no list. The stuck guard watches the steps left when
    /// <paramref name="countSteps"/> is set, else the distance.
    /// </summary>
    private SkillStatus? TakeOwnSteps(
        IPoint3D goal,
        int range,
        Func<Point3D, IPoint3D, int, TileStep, IReadOnlyList<Direction>> find,
        bool countSteps
    )
    {
        if (_ownSteps == null || _ownStep >= _ownSteps.Count)
        {
            _ownSteps = find(_character.Location, goal, range, Standable.Walker(_character.Map).SafeStep);
            _ownStep = 0;

            if (_ownSteps is not { Count: > 0 })
            {
                _ownSteps = null;
                return null;
            }
        }

        var motor = _character.Motor;

        if (motor.CanMoveNow)
        {
            var result = motor.DoMoveImpl(_ownSteps[_ownStep]);

            if (result == MoveResult.Success)
            {
                _ownStep++;
            }
            else if (result == MoveResult.Blocked)
            {
                _ownSteps = null;
            }
        }

        var left = !countSteps
            ? NavMetric.Distance(_character.Location, new Point3D(goal.X, goal.Y, goal.Z))
            : _ownSteps == null
                ? double.MaxValue
                : _ownSteps.Count - _ownStep;

        return _progress.Observe(left) ? SkillStatus.Failed : SkillStatus.Running;
    }

    /// <summary>
    /// The waypoint the pathfinder is working toward now — the stall log wants the
    /// leg, not the trip's final goal, to name the blocked tile.
    /// </summary>
    internal Point3D? CurrentLeg =>
        _character != null && _leg < _legs.Length
            ? new Point3D(_legs[_leg].X, _legs[_leg].Y, _legs[_leg].Z)
            : null;

    public override void Abort() => _character?.Motor.ClearMoveIntent();

    /// <summary>
    /// The walk goes on after a hold: a fight or a talk. A fight may have carried the walker
    /// off, so the stuck guard measures afresh from where it stands now, and the engine path
    /// plans again from here.
    /// </summary>
    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);

        if (_character == null)
        {
            return;
        }

        _progress.Reset();
        _frozenTicks = 0;
        _lastSeen = _character.Location;
        _character.Motor.RestartPath();
    }

    public static bool TimeUp(DateTime now, DateTime started) =>
        started != default && now - started >= GiveUp;
}
