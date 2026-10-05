using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using SosariaAI.Skills;
using Moves = Server.Movement.Movement;

namespace SosariaAI.Mobiles;

/// <summary>
/// Walks a player character the way a client does: one step per step delay, the Running
/// bit when the pace is a run, closed doors opened on the way, and round the armed floor
/// traps a player knows (<see cref="TrapDetour"/>). A living red on open ground never steps
/// across the guard line (<see cref="EntersGuards"/>). A character standing still in a
/// doorway steps out of it (<see cref="StepOutOfDoorway"/>). A PlayerMobile has no engine
/// AI, so this class holds the path, the step clock and the live move intent.
/// </summary>
public sealed class CharacterMotor
{
    public const int MinStepMs = 50;
    public const int SidestepTurns = 2;
    public const int RandomDirectionCount = 8;

    /// <summary>A back-off that has not got this far from where it stood is running in place.</summary>
    public const int AwayProgressTiles = 2;

    /// <summary>Running in place this long while backing off is being pinned: fight, heal or freeze instead.</summary>
    public const int AwayStallMs = 1000;

    /// <summary>A back-off asked for again after this long is a new one, measured from where it starts.</summary>
    public const int AwayRestartMs = 1500;

    /// <summary>A character that stepped within this many step delays is on its way, not standing.</summary>
    public const int WalkingStepDelays = 2;

    private readonly SosariaCharacter _character;
    private PathFollower _path;

    /// <summary>The goal the engine's path stalled on: the walk there follows the person's own tile route (<see cref="StepAlongRoute"/>).</summary>
    private Point3D? _ownRouteGoal;
    private long _nextMoveAt;
    private Mobile _intentTarget;
    private IPoint3D _intentPoint;
    private int _intentRange;
    private long _intentExpiresAt;
    private Point3D _awayFrom;
    private int _awaySteps;
    private readonly ApproachProgress _approach = new();
    private Mobile _approachGoal;
    private Point3D _approachGoalAt;
    private bool _approachGaveUp;
    private Point3D _approachGaveUpAt;
    private LoiterPace _loiter;
    private Point3D _awayAnchor;
    private long _awayAnchorAt;
    private long _lastAwayAt;
    private Map _stepMap;
    private TileStep _step;
    private readonly TrapDetour _detour = new();

    public CharacterMotor(SosariaCharacter character) => _character = character;

    /// <summary>The way this character came, for a runner that backs out the way it walked in.</summary>
    public StepTrail Trail { get; } = new();

    private CharacterAction _action = CharacterAction.Wander;

    /// <summary>The body's state. A change runs the driver's action hook once.</summary>
    public CharacterAction Action
    {
        get => _action;
        set
        {
            if (_action == value)
            {
                return;
            }

            _action = value;
            RoutineDriver.OnActionChanged(_character);
        }
    }

    /// <summary>True while the character runs. Travel and fights run; town idling walks.</summary>
    public bool Running { get; set; }

    /// <summary>A runner whose run the engine refused walks until this tick (<see cref="RunRules.TiredRunRestMs"/>).</summary>
    private long _walksUntil;

    /// <summary>True when this step is a run: the pace is a run and the mount is not resting (<see cref="RunRules.RunsNow"/>).</summary>
    private bool RunsNow => RunRules.RunsNow(Running, Core.TickCount, _walksUntil);

    public long NextMoveAt => _nextMoveAt;

    public int StepDelayMs => Math.Max(MinStepMs, RunRules.StepDelay(_character.Mounted, RunsNow));

    public bool CanMoveNow => Core.TickCount - _nextMoveAt >= 0;

    /// <summary>True while a walk goal is live and wants the next step on the move clock.</summary>
    public bool HasMoveIntent =>
        (_intentTarget != null || _intentPoint != null || _awaySteps > 0) && Core.TickCount - _intentExpiresAt < 0;

    /// <summary>
    /// A direct step, with no path behind it. True only when that step was taken. A step
    /// from a safe tile onto one an armed trap hurts is never taken.
    /// </summary>
    public bool DoMove(Direction direction) => !EntersTrap(direction) && IsSuccess(DoMoveImpl(direction));

    /// <summary>
    /// A direct step that slides to one side when blocked, for a walker with no path that
    /// only needs to get clear: backing off, fleeing, creeping round a ring.
    /// </summary>
    public bool DoMoveOrSlide(Direction direction) => DoMove(direction) || StepAside();

    /// <summary>
    /// The exact step the PathFollower asks for. A blocked step reports Blocked, so the
    /// follower plans again; a sidestep here once hid every block and walkers paced.
    /// </summary>
    public MoveResult DoMoveImpl(Direction direction, bool badStateOk = true)
    {
        if (InBadState() || !CanMoveNow)
        {
            return MoveResult.BadState;
        }

        if (EntersGuards(direction))
        {
            HeldAtGuardLine = true;
            return MoveResult.Blocked;
        }

        // Mobile.Move only turns a mobile that faces elsewhere. Face first, then step.
        var wanted = (direction & Direction.Mask) | (RunsNow ? Direction.Running : 0);
        _character.Direction = wanted;
        var at = _character.Location;

        if (Stepped(wanted, at) || TryOpenDoorAhead(wanted) && Stepped(wanted, at) || WalkedTiredRun(wanted, at))
        {
            ConsumeStep();
            HeldAtGuardLine = false;
            Trail.Note(_character.Map, _character.Location);
            return MoveResult.Success;
        }

        return MoveResult.Blocked;
    }

    /// <summary>
    /// True when the last step this walk asked for was refused at the guard line (see
    /// <see cref="EntersGuards"/>): the way it walks leads into a town. A step taken, a stop and
    /// a fresh path clear it, so a fight at the line does not speak for the walk after it.
    /// </summary>
    public bool HeldAtGuardLine { get; private set; }

    /// <summary>Drops the engine path, so the next step plans again from where the character stands.</summary>
    public void RestartPath()
    {
        _path = null;
        HeldAtGuardLine = false;
    }

    /// <summary>
    /// Walks toward a fixed point until it is inside <paramref name="range"/>. False on
    /// arrival or when no step is possible. A range of zero walks onto the point itself:
    /// with the default reach of one, a walk onto a pad stood beside it for good.
    /// </summary>
    public bool MoveToPoint(IPoint3D goal, int range = CharactersFile.DefaultGoToRange)
    {
        if (InBadState(ignoreClock: true) || goal == null)
        {
            ClearMoveIntent();
            return false;
        }

        if (_path?.Goal != goal)
        {
            _path = NewPath(goal);
            _ownRouteGoal = null;
        }

        RenewIntent(null, goal, range);

        if (DetourStep(goal, range) is { } detoured)
        {
            if (!detoured)
            {
                ClearMoveIntent();
            }

            return detoured;
        }

        var couldMove = CanMoveNow && !InBadState();
        var before = _character.Location;
        var at = new Point3D(goal.X, goal.Y, goal.Z);

        if (couldMove && _ownRouteGoal == at && !_character.InRange(at, range) && StepAlongRoute(at, range, StepWalker()))
        {
            return true;
        }

        if (_path.Follow(range))
        {
            _path = null;
            _ownRouteGoal = null;
            ClearMoveIntent();
            return false;
        }

        if (couldMove && _character.Location == before && StepAlongRoute(at, range, StepWalker()))
        {
            _ownRouteGoal = at;
            return true;
        }

        var progressed = _character.Location != before || !couldMove;

        if (!progressed)
        {
            ClearMoveIntent();
        }

        return progressed;
    }

    /// <summary>
    /// Walks until the target is inside <paramref name="range"/>. True while on the way or there.
    /// A living red out of the guards does not walk after a target that stands under them
    /// (<see cref="KeepsOffGuards"/>): the target is out of its reach.
    /// </summary>
    public bool MoveTo(Mobile target, int range)
    {
        if (InBadState(ignoreClock: true) || target?.Deleted != false)
        {
            ClearMoveIntent();
            return false;
        }

        if (_character.InRange(target, range))
        {
            ResetApproach();
            ClearMoveIntent();
            return true;
        }

        if (KeepsOffGuards && SosariaCharacter.UnderGuards(target))
        {
            ResetApproach();
            ClearMoveIntent();
            return false;
        }

        if (_approachGaveUp && _approachGoal == target)
        {
            if (target.Location == _approachGaveUpAt)
            {
                ClearMoveIntent();
                return false;
            }

            ResetApproach();
        }

        RenewIntent(target, null, range);

        if (DetourStep(target, range) is { } detoured)
        {
            if (detoured)
            {
                return true;
            }

            TrackApproach(target, couldMove: true);
            return !_approachGaveUp;
        }

        if (_path == null && _character.InLOS(target))
        {
            var before = _character.GetDistanceToSqrt(target);
            var result = DoMoveImpl(_character.GetDirectionTo(target));

            if (result == MoveResult.BadState)
            {
                return true;
            }

            if (result == MoveResult.Success && _character.GetDistanceToSqrt(target) < before)
            {
                ResetApproach();
                return true;
            }
        }

        if (_path == null || _path.Goal != target)
        {
            _path = NewPath(target);
        }

        var couldMove = CanMoveNow && !InBadState();
        var at = _character.Location;

        if (_path.Follow(range))
        {
            ResetApproach();
            return true;
        }

        if (couldMove && _character.Location == at)
        {
            StepAlongRoute(target.Location, range, StepWalker());
        }

        TrackApproach(target, couldMove);
        return !_approachGaveUp && (_character.Location != at || !couldMove);
    }

    /// <summary>
    /// One step back from a threat, the way a player looks where it goes: never nearer the
    /// threat, along a wall or a coast rather than into it, and toward open ground rather than
    /// a corner or a dead end (<see cref="EscapeRules.ChooseStep"/>). False when no step is
    /// possible now or the way back is shut.
    /// </summary>
    public bool StepAwayFrom(IPoint3D threat)
    {
        if (threat == null || InBadState() || !CanMoveNow)
        {
            return false;
        }

        var direction = EscapeRules.ChooseStep(
            EscapeStep,
            _character.Location,
            threat.X,
            threat.Y,
            EscapeRules.KiteLookahead
        );

        return direction != EscapeRules.NoChoice && DoMove((Direction)direction);
    }

    /// <summary>
    /// Backs away from a threat for <paramref name="steps"/> steps: the first now when the move
    /// clock allows, the rest on the move clock at the body's own pace, never two in one instant.
    /// False when pinned: the step was due and every way back was shut, or the back-off has run
    /// in place for <see cref="AwayStallMs"/> (a wall, a corner, a dead end).
    /// </summary>
    public bool BackAway(IPoint3D threat, int steps)
    {
        if (threat == null || steps <= 0 || InBadState(ignoreClock: true))
        {
            return false;
        }

        _path = null;
        RenewIntent(null, null, 0);
        _awayFrom = new Point3D(threat.X, threat.Y, threat.Z);
        _awaySteps = steps;

        if (AwayStalled())
        {
            _awaySteps = 0;
            return false;
        }

        return !CanMoveNow || InBadState() || TakeAwayStep();
    }

    /// <summary>
    /// True when this back-off has not got <see cref="AwayProgressTiles"/> from where it stood
    /// for <see cref="AwayStallMs"/>. A back-off asked for after a pause measures afresh.
    /// </summary>
    private bool AwayStalled()
    {
        var now = Core.TickCount;
        var here = _character.Location;

        if (now - _lastAwayAt > AwayRestartMs || NavMetric.Chebyshev(here, _awayAnchor) >= AwayProgressTiles)
        {
            _awayAnchor = here;
            _awayAnchorAt = now;
        }

        _lastAwayAt = now;
        return now - _awayAnchorAt >= AwayStallMs;
    }

    private bool TakeAwayStep()
    {
        if (StepAwayFrom(_awayFrom))
        {
            _awaySteps--;
            return true;
        }

        _awaySteps = 0;
        return false;
    }

    /// <summary>The engine's step check on the character's map, kept while it stays on that map.</summary>
    public TileStep GroundStep
    {
        get
        {
            var map = _character.Map;

            if (map != _stepMap || _step == null)
            {
                _stepMap = map;
                _step = (int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
                    Standable.TryStep(map, fromX, fromY, fromZ, toX, toY, out toZ);
            }

            return _step;
        }
    }

    /// <summary>True when this character's runs keep off guarded ground (<see cref="EscapeRules.KeepsOffGuards"/>).</summary>
    public bool KeepsOffGuards =>
        EscapeRules.KeepsOffGuards(PkRules.IsRed(_character.Kills), _character.IsGhost, SosariaCharacter.UnderGuards(_character));

    /// <summary>The step a run takes: <see cref="GroundStep"/>, never onto guarded ground while <see cref="KeepsOffGuards"/>.</summary>
    public TileStep EscapeStep
    {
        get
        {
            var step = GroundStep;

            if (!KeepsOffGuards)
            {
                return step;
            }

            var map = _character.Map;
            return (int fromX, int fromY, int fromZ, int toX, int toY, out int toZ) =>
                step(fromX, fromY, fromZ, toX, toY, out toZ) && !GuardCall.IsGuardedPlace(new Point3D(toX, toY, toZ), map);
        }
    }

    /// <summary>
    /// Stands about inside the Home ring: stands still for a dwell set by
    /// <paramref name="stayWeight"/>, turns to someone near, strolls to one free tile, and
    /// stands again. Outside the ring it walks back in first.
    /// </summary>
    public void LoiterInHome(int stayWeight)
    {
        if (InBadState(ignoreClock: true))
        {
            return;
        }

        (_loiter ??= new LoiterPace(_character)).Tick(stayWeight);
    }

    /// <summary>Takes the next step of a live walk goal. Called on the move clock between thinks.</summary>
    public void ContinueMove()
    {
        if (!HasMoveIntent || !CanMoveNow)
        {
            return;
        }

        if (_awaySteps > 0)
        {
            TakeAwayStep();
        }
        else if (_intentTarget != null)
        {
            MoveTo(_intentTarget, _intentRange);
        }
        else
        {
            MoveToPoint(_intentPoint, _intentRange);
        }
    }

    public void ClearMoveIntent()
    {
        _intentTarget = null;
        _intentPoint = null;
        _awaySteps = 0;
    }

    public void Stop()
    {
        ClearMoveIntent();
        ResetApproach();
        HeldAtGuardLine = false;
    }

    private bool Stepped(Direction direction, Point3D from) =>
        _character.Move(direction) && _character.Location != from;

    /// <summary>
    /// A refused run tried again at a walk. When the walk goes through, the run was refused by
    /// the mount's fatigue, not the ground: the rider walks for <see cref="RunRules.TiredRunRestMs"/>
    /// and lets the mount rest. True when the walk step was taken.
    /// </summary>
    private bool WalkedTiredRun(Direction run, Point3D from)
    {
        if ((run & Direction.Running) == 0)
        {
            return false;
        }

        var walk = run & Direction.Mask;
        _character.Direction = walk;

        if (!Stepped(walk, from))
        {
            return false;
        }

        _walksUntil = Core.TickCount + RunRules.TiredRunRestMs;
        return true;
    }

    private PathFollower NewPath(IPoint3D goal) => new(_character, goal) { Mover = DoMoveImpl };

    /// <summary>
    /// One step toward <paramref name="goal"/> along the first straight leg of the person's own
    /// tile route, judged by the engine's own step (<see cref="TileRoute"/>). The engine's path
    /// checks the items on a tile it steps onto but not the corners of a diagonal past them, and
    /// when its step is refused it walks straight at the goal: walkers set down north of the
    /// posts round the Orc Cave pads walked into them until their step ran out. Keeps off armed
    /// traps and, for a red, the guards (<see cref="DoMove"/>). False when no route or step serves.
    /// </summary>
    public bool StepAlongRoute(Point3D goal, int range, TileWalker walker)
    {
        if (InBadState() || !CanMoveNow || walker == null)
        {
            return false;
        }

        var at = _character.Location;
        var route = TileRoute.Find(at, goal, walker, isIndoor: null, range);

        if (route.Count == 0 || NavMetric.Chebyshev(at, route[0]) is not (var legTiles and > 0))
        {
            return false;
        }

        var next = new Point3D(
            WalkLine.Lerp(at.X, route[0].X, WalkLine.Step, legTiles),
            WalkLine.Lerp(at.Y, route[0].Y, WalkLine.Step, legTiles),
            at.Z
        );

        return DoMove(_character.GetDirectionTo(next));
    }

    private TileWalker StepWalker() => Standable.Walker(_character.Map);

    /// <summary>Steps to one side of the facing, then the other. False when both sides are blocked.</summary>
    public bool StepAside()
    {
        if (InBadState() || !CanMoveNow)
        {
            return false;
        }

        var facing = (int)(_character.Direction & Direction.Mask);
        var side = Utility.RandomBool() ? 1 : -1;

        for (var i = 0; i < SidestepTurns; i++)
        {
            var turned = (Direction)((facing + side + RandomDirectionCount) % RandomDirectionCount);
            var step = turned | (RunsNow ? Direction.Running : 0);

            if (EntersTrap(turned) || EntersGuards(turned))
            {
                side = -side;
                continue;
            }

            _character.Direction = step;

            if (Stepped(step, _character.Location))
            {
                ConsumeStep();
                HeldAtGuardLine = false;
                return true;
            }

            side = -side;
        }

        return false;
    }

    /// <summary>
    /// The client's auto-open-doors option opens a closed door in the walk direction.
    /// A locked door stays shut, and a house door keeps its own access check.
    /// </summary>
    private bool TryOpenDoorAhead(Direction direction)
    {
        var map = _character.Map;

        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var x = _character.X;
        var y = _character.Y;
        Moves.Offset(direction & Direction.Mask, ref x, ref y);

        if (DoorTiles.ClosedDoorAt(map, x, y, _character.Z) is not { } door)
        {
            return false;
        }

        door.Use(_character);
        return door.Open;
    }

    private void ConsumeStep() => _nextMoveAt = Core.TickCount + StepDelayMs;

    /// <summary>
    /// One step of the way round the armed traps near the way to <paramref name="goal"/>.
    /// Null when the engine path takes this step: no trap near, no safe way, the goal in
    /// range, or no step due now. The engine path is dropped after a step round, so it
    /// plans again from where the character stands.
    /// </summary>
    private bool? DetourStep(IPoint3D goal, int range)
    {
        if (!CanMoveNow || InBadState())
        {
            return null;
        }

        var direction = _detour.Next(_character.Map, _character.Location, new Point3D(goal.X, goal.Y, goal.Z), range);

        if (direction == null)
        {
            return null;
        }

        _path = null;

        if (DoMoveImpl(direction.Value) == MoveResult.Success)
        {
            _detour.Stepped();
            return true;
        }

        _detour.Blocked();
        return false;
    }

    /// <summary>True when a step that way leaves a safe tile for one an armed trap hurts.</summary>
    private bool EntersTrap(Direction direction)
    {
        var traps = TrapTiles.For(_character.Map);

        if (traps.Count == 0)
        {
            return false;
        }

        var at = _character.Location;
        var x = at.X;
        var y = at.Y;
        Moves.Offset(direction & Direction.Mask, ref x, ref y);
        return !TrapRules.MayEnter(traps.Harms(at.X, at.Y, at.Z), traps.Harms(x, y, at.Z));
    }

    /// <summary>
    /// True when a step that way takes a living red from open ground onto ground the guards
    /// cover (<see cref="KeepsOffGuards"/>). The engine sends a guard the moment a red steps in
    /// near a townsperson, so no walk of a red crosses the line: not a chase after a victim
    /// that ran into town, not an errand whose path cuts a town's corner.
    /// </summary>
    private bool EntersGuards(Direction direction)
    {
        if (!KeepsOffGuards)
        {
            return false;
        }

        var at = _character.Location;
        var x = at.X;
        var y = at.Y;
        Moves.Offset(direction & Direction.Mask, ref x, ref y);
        return GuardCall.IsGuardedPlace(new Point3D(x, y, at.Z), _character.Map);
    }

    /// <summary>
    /// Steps off an armed trap, or away from beside one when idle, the way a player caught
    /// standing there does (<see cref="TrapRules.ShouldStepOff"/>). A character on its way
    /// keeps walking. True when it stepped.
    /// </summary>
    public bool StepOffTrap()
    {
        if (!_character.Alive || InBadState() || !CanMoveNow)
        {
            return false;
        }

        var map = _character.Map;
        var traps = TrapTiles.For(map);

        if (traps.Count == 0)
        {
            return false;
        }

        var at = _character.Location;

        if (!TrapRules.ShouldStepOff(
                IsWalking,
                _action == CharacterAction.Combat,
                traps.Harms(at.X, at.Y, at.Z),
                traps.BesideTrap(at.X, at.Y, at.Z)
            ))
        {
            return false;
        }

        Span<TrapStepOption> options = stackalloc TrapStepOption[RandomDirectionCount];

        for (var d = 0; d < RandomDirectionCount; d++)
        {
            var x = at.X;
            var y = at.Y;
            Moves.Offset((Direction)d, ref x, ref y);
            var canStep = Standable.TryStep(map, at.X, at.Y, at.Z, x, y, out var z);
            options[d] = new TrapStepOption(canStep, canStep && traps.Harms(x, y, z), canStep && traps.BesideTrap(x, y, z));
        }

        var choice = TrapRules.StepOff(options, (int)(_character.Direction & Direction.Mask));
        return choice != TrapRules.NoStep && DoMove((Direction)choice);
    }

    /// <summary>
    /// Steps out of a doorway when it stands still there, the way a player moves on out of a
    /// door: the engine never shuts a door on a person, and the open leaf blocks the tile
    /// beside the doorway for everyone. Bank and shop errands ended in the Minoc bank door
    /// 486 times in one evening, and the walkers beside the leaf stalled there. The step goes
    /// to a free tile off the doorway, the way the character faces first. A character on its
    /// way or in a fight is left to it. True when it stepped.
    /// </summary>
    public bool StepOutOfDoorway()
    {
        if (!_character.Alive || InBadState() || !CanMoveNow || IsWalking ||
            _action is CharacterAction.Combat or CharacterAction.Flee)
        {
            return false;
        }

        var map = _character.Map;
        var at = _character.Location;

        if (!DoorTiles.IsDoorway(map, at.X, at.Y))
        {
            return false;
        }

        Span<bool> clear = stackalloc bool[RandomDirectionCount];

        for (var d = 0; d < RandomDirectionCount; d++)
        {
            var x = at.X;
            var y = at.Y;
            Moves.Offset((Direction)d, ref x, ref y);
            clear[d] = Standable.TryStep(map, at.X, at.Y, at.Z, x, y, out var z) &&
                       !DoorTiles.IsDoorway(map, x, y) &&
                       !StepBodies.Occupied(map, _character, x, y, z);
        }

        var choice = DoorTiles.StepOff(clear, (int)(_character.Direction & Direction.Mask));
        return choice != DoorTiles.NoStep && DoMove((Direction)choice);
    }

    /// <summary>True while a walk goal is live or the character stepped within the last few step delays.</summary>
    private bool IsWalking => HasMoveIntent || Core.TickCount - _nextMoveAt < (long)StepDelayMs * WalkingStepDelays;

    private void RenewIntent(Mobile target, IPoint3D point, int range)
    {
        _intentTarget = target;
        _intentPoint = point;
        _intentRange = range;
        _awaySteps = 0;
        _intentExpiresAt = Core.TickCount + StepDelayMs * 2 + (long)(_character.ThinkDelay.TotalMilliseconds * 2);
    }

    /// <summary>
    /// The stuck guard for a chase: the same one a walk leg keeps, started again whenever
    /// the target moves. Only a tick that could step counts.
    /// </summary>
    private void TrackApproach(Mobile target, bool couldMove)
    {
        if (!couldMove)
        {
            return;
        }

        if (_approachGoal != target || target.Location != _approachGoalAt)
        {
            _approachGoal = target;
            _approachGoalAt = target.Location;
            _approachGaveUp = false;
            _approach.Reset();
        }

        if (_approach.Observe(_character.GetDistanceToSqrt(target)))
        {
            _approachGaveUp = true;
            _approachGaveUpAt = target.Location;
            _path = null;
            ClearMoveIntent();
        }
    }

    private void ResetApproach()
    {
        _path = null;
        _approachGoal = null;
        _approachGoalAt = Point3D.Zero;
        _approachGaveUp = false;
        _approach.Reset();
    }

    private bool InBadState(bool ignoreClock = false) =>
        _character.Deleted || _character.Frozen || _character.Paralyzed ||
        !People.InWorld(_character) ||
        !ignoreClock && _character.Spell?.IsCasting == true;

    private static bool IsSuccess(MoveResult result) =>
        result is MoveResult.Success or MoveResult.SuccessAutoTurn;
}
