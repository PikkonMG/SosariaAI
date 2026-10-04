using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Spawning;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Plans a node path, walks each short leg with <see cref="GoToSkill"/>, and steps through
/// gate edges the way a player does, so the character does not path across the void. A pad
/// that carries the character mid-walk counts as the hop the plan wanted. A leg that fails
/// marks its edge in <see cref="EdgeHealth"/> and the trip plans again from where the
/// character stands, a few times per trip. A long trip mostly starts with a recall, or a gate
/// for a grandmaster who leads a party or has people standing by, and the rest are walked so
/// the roads keep their traffic; a trip with no road to its goal recalls over the water; a
/// mage that walked a long way marks a rune where it arrived. A walker on a long road stops
/// now and then to look round. A murderer never plans over a guarded public moongate, and
/// through a guarded town only when no road keeps clear of the guards; a leg on which its walk
/// meets the guards all the same is barred to reds and the trip plans again. No living walker
/// plans over a one-way pad that drops it onto a dungeon floor above its reach
/// (<see cref="PathSearchBars.ForWalker"/>). A plan never walks
/// across a teleporter pad it does not take (<see cref="TravelPlan.WithoutPassThroughPads"/>).
/// A fight, a flight or a pad that carries the walker off its leg starts the trip again from
/// where it stands (<see cref="WalkRecoveryRules.CarriedOffLeg"/>). A trip that shuns danger,
/// or that a fight or a flight carried off its road, reads the road found for the places the
/// walker ran from that still bar it (<see cref="WalkRecoveryRules.DangerBarsRoad"/>): it
/// recalls over such a road, or, carried off, waits for the place to go quiet, and gives up
/// when neither carries it past (<see cref="DangerousRoadWhy"/>).
/// </summary>
public sealed class TravelSkill : Skill
{
    private const string TookTeleporterEvent = "took a teleporter";

    public const string NoRouteWhy = "no route to the goal";
    public const string GuardsWhy = "will not walk into the guards";
    public const string GateRefusedWhy = "a gate or pad on the way did not carry it";
    public const string NoWayOutWhy = "could not walk out of the building";
    public const string OffRoadStallWhy = "the walk off the roads stalled";
    public const string RoadStallWhy = "the walk stalled on the road";
    public const string DangerousRoadWhy = "the road there passes a place it ran from";

    private const string LegBarredNote = "; the leg is barred to reds from now on";

    /// <summary>
    /// Thinks spent stepping onto one teleporter pad before the hop counts as refused: the
    /// pad is a few tiles off at most, so this covers the walk and a step off and back on.
    /// </summary>
    public const int PadStepTries = 12;

    private const int NoGate = -1;
    private const int NoStep = -1;
    private static readonly ILogger logger = SosariaLog.For(typeof(TravelSkill));

    /// <summary>A red barred from one guarded goal is told once, not on every think that tries it again.</summary>
    private static readonly TimeSpan GuardRefusalQuiet = TimeSpan.FromMinutes(10);

    private static readonly LogGate<(Serial Who, Point3D Goal)> GuardRefusals = new(GuardRefusalQuiet);

    private readonly string _destination;
    private readonly Point3D _target;
    private readonly int _range;

    private SosariaCharacter _character;
    private IReadOnlyList<TravelStep> _steps;
    private Point3D? _arrival;
    private int _index;
    private int _walkTargetIndex;
    private bool _tileRouteMode;
    private bool _walkingArrival;
    private bool _leavingToStreet;
    private int _replans;
    private int _tileHopIndex = NoStep;
    private readonly bool _arrivalFloor;
    private readonly bool _exactTarget;
    private readonly bool _avoidPads;
    private readonly IReadOnlyList<Point3D> _keepClear;
    private Point3D _resolvedTarget;
    private GoToSkill _walk;
    private NavGraph _graph;
    private int _planEpoch;
    private bool _waitingForPlan;
    private Func<bool> _deferredPlan;
    private int _gateIndex = NoGate;
    private int _gateTries;
    private TravelCastSkill _cast;
    private bool _markingArrival;
    private Point3D _startedAt;
    private Point3D _lastSeen;
    private Map _lastSeenMap;
    private Point3D _tripGoal;
    private bool _recalledOverNoRoad;
    private DateTime _recallWaitEnds;
    private string _waitDestName;
    private string _waitWhyNone;
    private DateTime _nextRoadPause;
    private DateTime _roadPauseEnds;
    private TimeSpan _roadPauseLength;

    /// <summary>True while the road walked is a red's road planned from open ground to keep clear of the guards.</summary>
    private bool _roadClearOfGuards;

    /// <summary>True for a trip that never starts by magic (see the point-trip constructor).</summary>
    private readonly bool _onFoot;

    /// <summary>True for a trip that gives up rather than walk past danger (see the point-trip constructor).</summary>
    private readonly bool _shunsDanger;

    /// <summary>True once a fight or a flight carried this trip's walker off its road (<see cref="SetOutAgain"/>).</summary>
    private bool _carriedOff;

    /// <summary>When the wait for a ran-from place to go quiet gives up (<see cref="WaitOutDanger"/>); default when not waiting.</summary>
    private DateTime _dangerWaitEnds;

    /// <summary>The road the walker waits to walk: read again each think of the wait.</summary>
    private IReadOnlyList<TravelStep> _dangerRoad;

    /// <summary>Waits for danger this trip made (<see cref="WalkRecoveryRules.MaxDangerWaits"/>).</summary>
    private int _dangerWaits;

    /// <summary>The last start refused a murderer's walk into the guards.</summary>
    private bool RedBarredNow { get; set; }

    public TravelSkill(string destination, int range)
    {
        _destination = destination;
        _range = range;
    }

    /// <summary>
    /// A point trip. Set <paramref name="arrivalFloor"/> when the target z already
    /// names the floor the approach uses — a destination's bound node, resolved by
    /// <see cref="Destination.ApproachPoint"/>. Otherwise a target above the land
    /// is treated as a roof marker and dropped to the ground. Set
    /// <paramref name="exactTarget"/> when the target is the walker's own spot already,
    /// so an authored work spot or bank is not moved to the copy's own (<see cref="WorkSites.WalkTarget(Point3D, string, Point3D)"/>).
    /// Set <paramref name="avoidPads"/> for a walk inside a dungeon: its tile routes go round
    /// the teleporter pads it does not mean to take (<see cref="Standable.PadShyWalker"/>);
    /// the proofs of a route's first hops still walk the plain floor.
    /// <paramref name="keepClear"/> names places the road search keeps away from as it keeps
    /// from remembered danger: a blue's dungeon walk from the reds' own town gate.
    /// Set <paramref name="onFoot"/> for a trip that must not start by magic: a red gang that
    /// rides out on foot together, where one mate's own recall left it alone at the camp.
    /// Set <paramref name="shunsDanger"/> for a trip the walker may give up: its road is read once
    /// the search finds it, and a road past a place in the road search's keep-away list recalls
    /// over it or ends the trip (<see cref="DangerousRoadWhy"/>).
    /// </summary>
    public TravelSkill(
        Point3D target,
        int range,
        bool arrivalFloor = false,
        bool exactTarget = false,
        bool avoidPads = false,
        IReadOnlyList<Point3D> keepClear = null,
        bool onFoot = false,
        bool shunsDanger = false
    )
    {
        _target = target;
        _range = range;
        _arrivalFloor = arrivalFloor;
        _exactTarget = exactTarget;
        _avoidPads = avoidPads;
        _keepClear = keepClear;
        _onFoot = onFoot;
        _shunsDanger = shunsDanger;
    }

    public override string Name => SkillKinds.GoTo;

    /// <summary>A walk aims at its goal: the named place, or the point it was given.</summary>
    public override JobTarget? AimedAt =>
        new JobTarget(
            string.IsNullOrWhiteSpace(_destination) ? _target.ToString() : _destination,
            _tripGoal != Point3D.Zero ? _tripGoal : _target
        );

    public override bool Begin(SosariaCharacter character) =>
        BeginTrip(character) || CannotStart(FailReason ?? (RedBarredNow ? GuardsWhy : NoRouteWhy));

    private bool BeginTrip(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _steps = null;
        _arrival = null;
        _index = 0;
        _walkTargetIndex = 0;
        _tileRouteMode = false;
        _walkingArrival = false;
        _leavingToStreet = false;
        _replans = 0;
        _tileHopIndex = NoStep;
        _graph = null;
        _planEpoch++;
        _waitingForPlan = false;
        _deferredPlan = null;
        _gateIndex = NoGate;
        _gateTries = 0;
        _cast = null;
        _markingArrival = false;
        _startedAt = character.Location;
        _lastSeen = character.Location;
        _lastSeenMap = character.Map;
        _recalledOverNoRoad = false;
        _recallWaitEnds = default;
        _nextRoadPause = default;
        _roadPauseEnds = default;
        _roadClearOfGuards = false;
        _carriedOff = false;
        _dangerWaitEnds = default;
        _dangerRoad = null;
        _dangerWaits = 0;
        _resolvedTarget = _exactTarget ? _target : CopyTarget(character);

        RedBarredNow = RedBarred();

        if (RedBarredNow)
        {
            return false;
        }

        _tripGoal = ResolveTileGoal() ?? Point3D.Zero;

        return Planned(() => BeginMagic() || SetOut());
    }

    /// <summary>
    /// The copy's own spot for the trip (<see cref="WorkSites.WalkTarget(Point3D, string, Point3D)"/>).
    /// A walk to work aims at the floor of the work patch nearest its hashed tile: thirteen
    /// Cove mine walks aimed at rock and ended with no tile to stand on near the goal.
    /// </summary>
    private Point3D CopyTarget(SosariaCharacter character)
    {
        var target = WorkSites.WalkTarget(_target, character.CharacterId, character.HomeSpot);

        return WorkSites.TryWalkPatch(_target, character.CharacterId, out var patch) &&
               HarvestScan.TryNearestFloor(character.Map, target, patch, out var floor)
            ? floor
            : target;
    }

    /// <summary>
    /// Runs route work when the shard's planning budget allows it, and charges its time.
    /// Over the budget the traveler stands this heartbeat out and plans on a later one.
    /// </summary>
    private bool Planned(Func<bool> plan)
    {
        if (!PlanBudget.TryEnter())
        {
            _deferredPlan = plan;
            return true;
        }

        var started = PlanBudget.Start();

        try
        {
            return plan();
        }
        finally
        {
            PlanBudget.Charge(started);
        }
    }

    /// <summary>
    /// A murderer who walks into a guarded town or onto a guarded moongate pad dies to the
    /// guards on arrival. A red refuses the trip, and the planner stops offering it. A
    /// ghost is no guard candidate, so a red's ghost walks through a town to an ankh.
    /// </summary>
    private bool RedBarred()
    {
        if (ResolveTileGoal() is not { } goal || MayWalkInto(_character, goal))
        {
            return false;
        }

        _character.Memory.Unreachable.Note(goal, Core.Now);

        if (SosariaSettings.LogActivity && GuardRefusals.Opens((_character.Serial, goal), Core.Now))
        {
            logger.Information("{Name} will not walk into the guards at {Goal}", _character.Name, goal);
        }

        return true;
    }

    /// <summary>
    /// True when <paramref name="character"/> may walk to <paramref name="goal"/>: anyone but a
    /// living murderer, and a murderer only to a place the guards do not cover.
    /// </summary>
    public static bool MayWalkInto(SosariaCharacter character, Point3D goal) =>
        character.IsGhost || PkRules.MayVisit(PkRules.IsRed(character.Kills), GuardCall.IsGuardedPlace(goal, character.Map));

    // A route planned from inside a building aims at the nearest street through the wall.
    // Walk out along the floor first, then plan from open ground. A ghost keeps to the
    // floor too: only doors and people let it through.
    private bool SetOut() => LeaveBuilding() || PlanFromHere();

    /// <summary>
    /// A long trip with a rune that serves the goal starts by magic, as players did: a
    /// grandmaster leading a party, or with people standing by, opens a gate; anyone else
    /// with the means mostly recalls (<see cref="RecallRules.TakesMagic"/>) and otherwise
    /// walks. A party member stays with its party on foot, and so does a trip set on foot.
    /// </summary>
    private bool BeginMagic()
    {
        if (_onFoot || _character.IsGhost || ResolveTileGoal() is not { } goal)
        {
            return false;
        }

        var party = GameParty.Of(_character);
        var inParty = GameParty.InParty(_character);
        var leads = inParty && party.Leader == _character;

        if (GateRules.PrefersGate(leads, PetKeeper.RecallStrandsPets(_character), GateRules.Bystanders(_character)) &&
            StartCast(new GateSkill(goal, leads ? GateRules.PartyHold : TimeSpan.Zero)))
        {
            return true;
        }

        return !inParty &&
               RecallRules.TakesMagic(NavMetric.Chebyshev(_character.Location, goal), Utility.Random(PercentRoll.Scale)) &&
               StartCast(new RecallSkill(goal));
    }

    /// <summary>
    /// No road reaches the goal from here: water, a gateless island, a sealed pocket, or a
    /// moongate a murderer may not take. A person with a mark near the goal recalls over it
    /// whatever the trip's length, as players crossed water. Once a trip, so a recall that
    /// will not take ends the trip rather than casting again after every search.
    /// </summary>
    private bool RecallOverNoRoad(bool noRoad)
    {
        if (!noRoad || _recalledOverNoRoad || _character.IsGhost || GameParty.InParty(_character) ||
            ResolveTileGoal() is not { } goal ||
            !StartCast(new RecallSkill(goal, RecallRules.NoRoadMinTripTiles)))
        {
            return false;
        }

        _recalledOverNoRoad = true;
        return true;
    }

    private bool StartCast(TravelCastSkill cast)
    {
        if (!cast.Begin(_character))
        {
            return false;
        }

        _cast = cast;
        return true;
    }

    /// <summary>
    /// A recall or gate landed or would not take: the rest of the trip is walked from here.
    /// A mark at the arrival ends the trip either way.
    /// </summary>
    private SkillStatus TickCast()
    {
        var status = _cast.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _cast = null;

        if (_markingArrival)
        {
            return SkillStatus.Done;
        }

        NoteSeen();
        DungeonGate.NoteWhere(_character);
        return Planned(SetOut) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>
    /// The trip is done. A mage that came a long way with a blank rune marks this place, so
    /// the next trip here is a recall; the words hold the trip open until they end.
    /// </summary>
    private SkillStatus Arrived()
    {
        if (!MarkRules.TripWorthARune(NavMetric.Chebyshev(_startedAt, _character.Location)) ||
            !StartCast(new MarkSkill()))
        {
            return SkillStatus.Done;
        }

        _markingArrival = true;
        return SkillStatus.Running;
    }

    public override SkillStatus Tick()
    {
        var status = TickWalk();
        return status == SkillStatus.Failed && FailReason == null ? Fail(StoppedWhy()) : status;
    }

    private SkillStatus TickWalk()
    {
        if (_character == null)
        {
            return SkillStatus.Failed;
        }

        if (_cast != null)
        {
            return TickCast();
        }

        var status = TickTrip();
        return status == SkillStatus.Done ? Arrived() : status;
    }

    /// <summary>Why a trip stopped, read from where it stood in the trip: every failure names its cause.</summary>
    private string StoppedWhy() =>
        _character == null ? LeftWorldReason
        : _gateIndex != NoGate ? GateRefusedWhy
        : _leavingToStreet ? NoWayOutWhy
        : _tileRouteMode ? OffRoadStallWhy
        : _walk == null ? NoRouteWhy
        : RoadStallWhy;

    private SkillStatus TickTrip()
    {
        if (_deferredPlan != null)
        {
            if (!PlanBudget.TryEnter())
            {
                return SkillStatus.Running;
            }

            var plan = _deferredPlan;
            _deferredPlan = null;
            return Planned(plan) ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (_waitingForPlan)
        {
            return SkillStatus.Running;
        }

        if (_recallWaitEnds != default)
        {
            return TickRecallWait();
        }

        if (_dangerWaitEnds != default)
        {
            return TickDangerWait();
        }

        if (_gateIndex != NoGate)
        {
            return StepThroughGate(_gateIndex);
        }

        if (CarriedOnPlan() is { } carried)
        {
            return carried;
        }

        if (_tileRouteMode)
        {
            var tileStatus = _walk?.Tick() ?? SkillStatus.Failed;

            if (tileStatus != SkillStatus.Failed)
            {
                return tileStatus;
            }

            LogStall();
            return RetryTileWalk() ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (_walk == null)
        {
            return SkillStatus.Failed;
        }

        if (StrayedIntoGuards() is { } strayed)
        {
            return strayed;
        }

        if (!_leavingToStreet && !_walkingArrival && PausedOnRoad())
        {
            return SkillStatus.Running;
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        if (_leavingToStreet)
        {
            if (status == SkillStatus.Failed)
            {
                LogStall();
                return SkillStatus.Failed;
            }

            _leavingToStreet = false;
            return Planned(PlanFromHere) ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (status == SkillStatus.Failed)
        {
            LogStall();
            NoteFailedEdge();
            return RetryFromStreet() ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (_walkingArrival)
        {
            return SkillStatus.Done;
        }

        return ContinueAfterWalk();
    }

    /// <summary>
    /// A walker on a long road stops for a few seconds about once a minute, looks round or
    /// at someone near, then walks on; the leg's clock skips the stop. A runner, a fighter
    /// and a ghost never stop. True while the walker stands.
    /// </summary>
    private bool PausedOnRoad()
    {
        var now = Core.Now;

        if (_roadPauseEnds != default)
        {
            if (now < _roadPauseEnds)
            {
                return true;
            }

            _roadPauseEnds = default;
            _walk.Resume(_roadPauseLength);
            _nextRoadPause = now + RoadPauseRules.WalkBetween(Utility.Random(int.MaxValue));
            return false;
        }

        var fighting = _character.Combatant is { Deleted: false, Alive: true };
        var roadLeft = _tripGoal == Point3D.Zero ? 0 : NavMetric.Chebyshev(_character.Location, _tripGoal);

        if (!RoadPauseRules.MayPause(_character.Motor.Running, fighting, _character.IsGhost, roadLeft))
        {
            return false;
        }

        if (_nextRoadPause == default)
        {
            _nextRoadPause = now + RoadPauseRules.WalkBetween(Utility.Random(int.MaxValue));
            return false;
        }

        if (now < _nextRoadPause)
        {
            return false;
        }

        _roadPauseLength = RoadPauseRules.PauseLength(Utility.Random(int.MaxValue));
        _roadPauseEnds = now + _roadPauseLength;
        _character.Motor.Stop();
        _character.Direction = (Direction)Utility.Random(CharacterMotor.RandomDirectionCount);
        LoiterPace.FaceNearest(_character);
        return true;
    }

    public override void Abort()
    {
        _planEpoch++;
        _waitingForPlan = false;
        _cast?.Abort();
        _cast = null;
        _walk?.Abort();
        _walk = null;
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _cast?.Resume(held);
        _recallWaitEnds = SkillClock.Shift(_recallWaitEnds, held);
        _dangerWaitEnds = SkillClock.Shift(_dangerWaitEnds, held);
    }

    /// <summary>
    /// Hands the graph search to a path worker. False with the reason when no search was
    /// queued: no node near the goal, a search from here failed moments ago, or the
    /// workers did not take the job. With <paramref name="openPads"/> the road takes the
    /// one-way pads onto floors above the walker's reach (<see cref="PathSearchBars.OpeningPads"/>),
    /// and with <paramref name="crossGuards"/> a murderer's road crosses the guards by the least
    /// guarded way instead of keeping off them.
    /// </summary>
    private bool QueueGraphPlan(
        NavGraph graph,
        DestinationCatalog catalog,
        out string destName,
        out string whyNot,
        bool openPads = false,
        bool crossGuards = false
    )
    {
        destName = DestinationNode(graph, catalog);

        if (string.IsNullOrWhiteSpace(destName))
        {
            whyNot = Traveler.WhyNoGoal(graph, TileGoal());
            return false;
        }

        if (Traveler.InFailedCooldown(graph, _character.Location, destName))
        {
            whyNot = Traveler.WhyRecentFailure;
            return false;
        }

        AimArrival(graph, catalog, destName);
        var epoch = ++_planEpoch;
        var from = _character.Location;
        var avoid = Avoid();
        var keep = Traveler.IndoorKeepFor(graph, from, destName, MapIndoor);
        var planned = destName;
        var bars = PathSearchBars.ForWalker(_character, graph, _character.Map, from);

        if (openPads && bars is { MayOpenPads: true })
        {
            bars = bars.OpeningPads();
        }

        if (crossGuards)
        {
            bars = bars?.CrossingGuards();
        }

        var queued = PathSearch.TryEnqueue(
            new PathSearch.Job
            {
                Graph = graph,
                Source = destName,
                GateCost = NavSearch.DefaultGateCost,
                Avoid = avoid,
                IndoorKeep = keep,
                Bars = bars,
                Epoch = epoch,
                OnComplete = (prev, _, jobEpoch) => OnGraphPlan(graph, catalog, planned, prev, avoid, bars, jobEpoch)
            }
        );

        if (!queued)
        {
            whyNot = Traveler.WhySearchRefused;
            return false;
        }

        // Drop the leave-building walk. Ticking it after a missed plan used a
        // null node list and crashed the shard from TavernSkill.
        _walk = null;
        _waitingForPlan = true;
        whyNot = null;
        return true;
    }

    /// <summary>
    /// The graph node the trip plans to: the named destination's bound node, or the node
    /// that serves a point goal. Null when no node lies near the goal.
    /// </summary>
    private string DestinationNode(NavGraph graph, DestinationCatalog catalog)
    {
        if (!string.IsNullOrWhiteSpace(_destination))
        {
            var dest = ResolveDestination(catalog);
            return !string.IsNullOrWhiteSpace(dest?.Node) ? dest.Node : _destination;
        }

        return TileGoal() == Point3D.Zero
            ? null
            : Traveler.PreferredGoal(graph, _character.Location, TileGoal())?.Name;
    }

    /// <summary>
    /// The graph gets close to the goal; the real point is the final leg. Missing this made
    /// a gate node count as home and ended point trips at the node near the goal.
    /// </summary>
    private void AimArrival(NavGraph graph, DestinationCatalog catalog, string destName)
    {
        if (!string.IsNullOrWhiteSpace(_destination))
        {
            var approach = ResolveDestination(catalog)?.ApproachPoint(graph) ?? Point3D.Zero;
            _arrival = approach != Point3D.Zero ? approach : _arrival;
            return;
        }

        if (graph.TryGetNode(destName, out var node) && node.Location != TileGoal())
        {
            _arrival = TileGoal();
        }
    }

    private void OnGraphPlan(
        NavGraph graph,
        DestinationCatalog catalog,
        string destName,
        Dictionary<string, string> prev,
        IReadOnlyList<Point3D> avoid,
        PathSearchBars bars,
        int epoch
    )
    {
        if (epoch != _planEpoch || _character == null || _character.Deleted)
        {
            return;
        }

        _waitingForPlan = false;
        Planned(() => FinishGraphPlan(graph, catalog, destName, prev, avoid, bars));
    }

    /// <summary>
    /// The world thread's half of a graph plan: the walker's own first hop, then, with no
    /// road from here, the way past the guards, the walk back onto the road, a recall or the
    /// wait for one. It is paid from the planning budget like any other plan. True when the
    /// walker set out, cast, or waits for a search or a recall; false when the trip is over.
    /// </summary>
    private bool FinishGraphPlan(
        NavGraph graph,
        DestinationCatalog catalog,
        string destName,
        Dictionary<string, string> prev,
        IReadOnlyList<Point3D> avoid,
        PathSearchBars bars
    )
    {
        NoteSeen();
        _roadClearOfGuards = bars is { KeepsOffGuards: true, OpenAroundStart: false, CrossesGuards: false };
        var names = Traveler.FinishPlan(
            graph,
            _character.Location,
            destName,
            prev,
            MapWalker(),
            MapIndoor,
            avoid,
            out var whyNone
        );

        if (ReadsRoad && TravelPlan.From(graph, names) is var road && RoadPassesDanger(road, BarringDanger()))
        {
            return RecallOverNoRoad(noRoad: true) || WaitOutDanger(road) || GiveUpPastDanger();
        }

        if (names.Count > 0 && ApplyNames(graph, names) ||
            names.Count == 0 && (WayOverTheDrops(graph, catalog, bars, whyNone) || WayPastTheGuards(graph, catalog, bars, whyNone)) ||
            ReturnToRoad(graph, destName, prev))
        {
            return true;
        }

        // A plan through a moongate the walker may not take is no road for it either.
        var noRoad = names.Count > 0 || Traveler.IsNoRoad(whyNone);

        if (RecallOverNoRoad(noRoad))
        {
            return true;
        }

        _walk = null;
        _steps = null;

        if (noRoad && WaitForRecall(destName, whyNone))
        {
            return true;
        }

        RememberFailedRoute(graph, destName, ResolveTileGoal() ?? Point3D.Zero, whyNone, noRoad ? WhyNoRecall() : null);
        return false;
    }

    /// <summary>
    /// The road search already pays dearly for every node near a remembered place (<see cref="Avoid"/>),
    /// so a road it still leads past one of <paramref name="barring"/> has no way round: a
    /// moongate, a bridge, the one road out. A trip that shuns danger reads the walked road on
    /// the world thread: Ursel Walsh and Kenric the Just set out from the Magincia bank, met the
    /// red camp on the road to the moongate, ran home, and set out on the same road again every
    /// two minutes, for the trip's goal and midpoint lay far from the camp. A tile walk is read
    /// the same way (<see cref="TryTileWalk"/>).
    /// </summary>
    private bool RoadPassesDanger(IReadOnlyList<TravelStep> road, IReadOnlyList<Point3D> barring) =>
        TravelPlan.WalksNear(_character.Location, road, barring);

    /// <summary>True when this trip reads each road it finds for danger (<see cref="WalkRecoveryRules.ReadsRoadForDanger"/>).</summary>
    private bool ReadsRoad => WalkRecoveryRules.ReadsRoadForDanger(_shunsDanger, _carriedOff, GameParty.InParty(_character));

    /// <summary>The trip ends before its first step on a road past danger. False, for the plan to return.</summary>
    private bool GiveUpPastDanger()
    {
        _walk = null;
        _steps = null;
        NoteFailReason(DangerousRoadWhy);
        return false;
    }

    /// <summary>
    /// The road from here still passes a place the walker ran from, and no recall carries it
    /// over: the walker stands where it got clear and waits for the place to go quiet, as a
    /// player waited out the reds, then plans again (<see cref="WalkRecoveryRules.MayWaitOutDanger"/>).
    /// Giving up at once ended 1,317 walks home at the first road read after a flight, and the
    /// walker idled where it got clear, in reach of the same threat. False, for the trip to give up.
    /// </summary>
    private bool WaitOutDanger(IReadOnlyList<TravelStep> road)
    {
        if (!WalkRecoveryRules.MayWaitOutDanger(_shunsDanger, _dangerWaits))
        {
            return false;
        }

        DropRoad();
        _dangerWaits++;
        _dangerRoad = road;
        _dangerWaitEnds = Core.Now + WalkRecoveryRules.MaxDangerWait;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", DangerWaitLine(_character.Name, _character.Location));
        }

        return true;
    }

    /// <summary>
    /// One think of the wait for danger: the walker stands while its road still passes a place
    /// that bars it, and plans again from here once the place went quiet or the wait is over.
    /// A road still barred then recalls, waits once more, or ends the trip (<see cref="FinishGraphPlan"/>).
    /// </summary>
    private SkillStatus TickDangerWait()
    {
        if (Core.Now < _dangerWaitEnds && RoadPassesDanger(_dangerRoad, BarringDanger()))
        {
            return SkillStatus.Running;
        }

        _dangerWaitEnds = default;
        _dangerRoad = null;
        NoteSeen();
        return Planned(SetOut) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>The line a walker waiting out a place it ran from writes, easy to count.</summary>
    public static string DangerWaitLine(string name, Point3D at) =>
        $"{name} waits at {at} for the place it ran from to go quiet";

    /// <summary>
    /// A murderer with no road clear of the guards recalls over them when it can. Only a red
    /// already under the guards plans once more across them, by the least guarded way out
    /// (<see cref="PathSearchBars.CrossingGuards"/>): every step there is a risk anyway. A red
    /// on open ground never walks into a town: the engine sends a guard the moment it steps in
    /// near a townsperson, and 11 of 57 reds that crossed died on the way in one run. The goal
    /// itself is never under the guards (<see cref="RedBarred"/>), and a guarded moongate pad
    /// stays barred. False when the search was not a murderer's barred one, found a road, or no
    /// recall or second search could start.
    /// </summary>
    private bool WayPastTheGuards(NavGraph graph, DestinationCatalog catalog, PathSearchBars bars, string whyNone)
    {
        if (bars is not { KeepsOffGuards: true, CrossesGuards: false } || whyNone != Traveler.WhyNoPath)
        {
            return false;
        }

        if (RecallOverNoRoad(noRoad: true))
        {
            return true;
        }

        if (!bars.MayCrossGuards)
        {
            return false;
        }

        if (!QueueGraphPlan(graph, catalog, out _, out _, openPads: true, crossGuards: true))
        {
            return false;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", CrossGuardsLine(_character.Name, _character.Location, ResolveTileGoal() ?? TileGoal()));
        }

        return true;
    }

    /// <summary>
    /// A walker with no road that keeps off the one-way pads onto floors above its reach recalls
    /// when it can, and plans once more over them when it cannot (<see cref="PathSearchBars.OpeningPads"/>):
    /// a walker that stands where only such a pad leads out must still get out. 341 walkers in
    /// Deceit and the deep floors had no road home at all. Every no-road verdict counts
    /// (<see cref="Traveler.IsNoRoad"/>): the power the bars read rises and falls with the
    /// walker's hits and its party, so a crawler that picked Deceit's second level at full
    /// hits came through the door hurt, found its own stairs down barred, and its plan from the
    /// door ended with the walk to the first node blocked. A murderer's road there still keeps
    /// off the guards, and crosses them only when that search finds none too
    /// (<see cref="WayPastTheGuards"/>). False when the search barred no pad, found a road, or
    /// no recall or second search could start.
    /// </summary>
    private bool WayOverTheDrops(NavGraph graph, DestinationCatalog catalog, PathSearchBars bars, string whyNone)
    {
        if (bars is not { MayOpenPads: true } || !Traveler.IsNoRoad(whyNone))
        {
            return false;
        }

        if (RecallOverNoRoad(noRoad: true))
        {
            return true;
        }

        if (!QueueGraphPlan(graph, catalog, out _, out _, openPads: true))
        {
            return false;
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", OverTheDropsLine(_character.Name, _character.Location, ResolveTileGoal() ?? TileGoal()));
        }

        return true;
    }

    /// <summary>The line a plan over the pads onto floors above the walker's reach writes, easy to count.</summary>
    public static string OverTheDropsLine(string name, Point3D from, Point3D goal) =>
        $"{name} has no road from {from} to {goal} that keeps off the floors above its reach and takes the pads onto them";

    /// <summary>The line a red's plan across the guards writes, easy to count.</summary>
    public static string CrossGuardsLine(string name, Point3D from, Point3D goal) =>
        $"{name} has no road clear of the guards from {from} to {goal} and crosses them by the least guarded way";

    /// <summary>
    /// No road, and the recall over it is refused only for a while: in the heat of battle,
    /// mid-cast, short of mana, with the runebook resting, or under a criminal flag (see
    /// <see cref="RecallRules.NoRoadWait"/>). The trip stands and casts once the refusal passes.
    /// A red in the Den just after a fight gave up its run instead, and reds flagged for looting
    /// at the Shame door ended 78 walks home with "no recall: a criminal" and went back to the fight.
    /// </summary>
    private bool WaitForRecall(string destName, string whyNone)
    {
        if (RecallRules.NoRoadWait(WhyNoRecall()) is not { } wait)
        {
            return false;
        }

        _recallWaitEnds = Core.Now + wait;
        _waitDestName = destName;
        _waitWhyNone = whyNone;
        return true;
    }

    /// <summary>One think of the wait for a no-road recall: cast when it takes, give the trip up when the wait is over.</summary>
    private SkillStatus TickRecallWait()
    {
        if (RecallOverNoRoad(noRoad: true))
        {
            _recallWaitEnds = default;
            return SkillStatus.Running;
        }

        if (Core.Now < _recallWaitEnds)
        {
            return SkillStatus.Running;
        }

        _recallWaitEnds = default;
        RememberFailedRoute(
            NavWorld.GraphFor(_character.HomeFacet),
            _waitDestName,
            ResolveTileGoal() ?? Point3D.Zero,
            _waitWhyNone,
            WhyNoRecall()
        );
        return SkillStatus.Failed;
    }

    /// <summary>
    /// With no road, why no recall carried the walker over it, for the log line: 38 reds on
    /// Buccaneer's Den wrote "no route" and nothing on why the recall over the water failed.
    /// </summary>
    private string WhyNoRecall()
    {
        if (_character.IsGhost || ResolveTileGoal() is not { } goal)
        {
            return null;
        }

        if (_recalledOverNoRoad)
        {
            return RecallRules.RecalledOnceWhy;
        }

        return GameParty.InParty(_character)
            ? RecallRules.InPartyWhy
            : RecallRules.WhyNoRecall(_character, goal, RecallRules.NoRoadMinTripTiles) ?? RecallRules.WordsFailedWhy;
    }

    private bool ApplyNames(NavGraph graph, IReadOnlyList<string> names)
    {
        if (names == null || names.Count == 0)
        {
            return false;
        }

        var steps = TravelPlan.WithoutPassThroughPads(graph, TravelPlan.From(graph, names));

        // A plan found from an indoor start searches without the murderer's bars.
        if (UsesMoongate(steps) && !GateTravel.MayTakeMoongates(_character, steps))
        {
            return false;
        }

        _tileRouteMode = false;
        _graph = graph;
        _steps = steps;
        _index = 0;
        _walkTargetIndex = 0;
        _walkingArrival = false;
        _tileHopIndex = NoStep;
        return StartWalkTo(0);
    }

    private bool StartWalkTo(int targetIndex)
    {
        _walkTargetIndex = targetIndex;
        var step = _steps[targetIndex];
        var isLastNode = targetIndex == _steps.Count - 1;
        var isFinal = isLastNode && _arrival == null;
        var range = isFinal ? _range : CharactersFile.DefaultGoToRange;
        _walk = new GoToSkill(step.Location, range, floorAtEnd: isFinal);
        return _walk.Begin(_character);
    }

    internal SkillStatus ContinueAfterWalk()
    {
        if (_steps is not { Count: > 0 })
        {
            return SkillStatus.Failed;
        }

        _index = _walkTargetIndex;
        return AdvanceFromCurrent();
    }

    private SkillStatus AdvanceFromCurrent()
    {
        if (_character == null || _steps is not { Count: > 0 })
        {
            return SkillStatus.Failed;
        }

        if (_index >= _steps.Count - 1)
        {
            return FinishOrArrival();
        }

        var nextIndex = _index + 1;
        var next = _steps[nextIndex];

        if (next.ArrivalGate != NavGateKind.None)
        {
            _gateTries = 0;
            return StepThroughGate(nextIndex);
        }

        return StartWalkTo(nextIndex) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>
    /// One think at the gate into step <paramref name="gateIndex"/>. A fight on the pad or a
    /// moon rest waits; a gate that will not take the character is <see cref="GateRefused"/>.
    /// </summary>
    private SkillStatus StepThroughGate(int gateIndex)
    {
        var gate = _steps[gateIndex];
        var fighting = _character.Combatant is { Deleted: false, Alive: true };
        var step = DefendRules.MayTakeGate(fighting)
            ? GateTravel.Apply(
                _character,
                gate.ArrivalGate,
                gate.Location,
                _character.Map,
                TookTeleporterEvent,
                TravelPlan.ExitToward(_steps, gateIndex, _arrival)
            )
            : GateStep.Waiting;

        if (step == GateStep.Waiting &&
            (gate.ArrivalGate != NavGateKind.Teleporter || ++_gateTries < PadStepTries))
        {
            _gateIndex = gateIndex;
            return SkillStatus.Running;
        }

        _gateIndex = NoGate;
        NoteSeen();
        return step == GateStep.Through ? ContinueFrom(gateIndex) : GateRefused(gateIndex);
    }

    /// <summary>
    /// A gate that would not take the walker. One it was carried away from while it waited, by
    /// a fight or a flight, is no fault of the gate: the trip sets out again from where the
    /// walker stands. Harlan waited out a fight at the Trinsic moongate, fled far south, and
    /// asked the bare ground for the gate 52,236 times in four hours. At the pad itself the trip
    /// ends with the reason, and later plans pay to use the hop (<see cref="EdgeHealth"/>).
    /// </summary>
    private SkillStatus GateRefused(int gateIndex)
    {
        var pad = _steps[gateIndex - 1];
        var gate = _steps[gateIndex];

        if (GateHopRules.LeftThePad(_character.Location, pad.Location))
        {
            return SetOutAgain(pad.Location);
        }

        EdgeHealth.NoteFailure(_graph, pad.Node, gate.Node);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", GateRefusedLine(_character.Name, gate.ArrivalGate, pad.Location));
        }

        return SkillStatus.Failed;
    }

    /// <summary>The line a trip that a gate would not take writes at the pad, easy to count.</summary>
    public static string GateRefusedLine(string name, NavGateKind kind, Point3D pad) =>
        $"{name} found no {kind.ToString().ToLowerInvariant()} to take at {pad}; the trip ends and later plans pay for the hop";

    /// <summary>
    /// A pad on the way carried the character to a landing the plan goes through: that is
    /// the hop it wanted, so the walk goes on from the landing. Anything that carried it
    /// well off its leg otherwise, a pad, a fight or a flight, makes the leg stale, and the
    /// trip sets out again from where it stands. Null when the character only walked or
    /// still stands near its leg.
    /// </summary>
    private SkillStatus? CarriedOnPlan()
    {
        var before = _lastSeen;
        var sameMap = _lastSeenMap == _character.Map;
        var carried = GateHopRules.Carried(before, _character.Location, sameMap);
        NoteSeen();

        if (!carried)
        {
            return null;
        }

        for (var i = _walkTargetIndex; i < (_steps?.Count ?? 0); i++)
        {
            if (_steps[i].ArrivalGate == NavGateKind.Teleporter &&
                GateHopRules.Landed(_character.Location, _steps[i].Location))
            {
                _walk?.Abort();
                DungeonGate.NoteWhere(_character);
                return ContinueFrom(i);
            }
        }

        return _walk?.CurrentLeg is { } leg &&
               WalkRecoveryRules.CarriedOffLeg(
                   sameMap,
                   NavMetric.Chebyshev(before, leg),
                   NavMetric.Chebyshev(_character.Location, leg)
               )
            ? SetOutAgain(before)
            : null;
    }

    /// <summary>
    /// The walker was carried well off its leg: it looks where it stands and sets out again,
    /// as a player does after a fight. No stall is logged, no edge is marked and no replan is
    /// spent: nothing on the road failed. From now on the trip reads each road it finds for the
    /// places the walker ran from (<see cref="WalkRecoveryRules.ReadsRoadForDanger"/>).
    /// </summary>
    private SkillStatus SetOutAgain(Point3D carriedFrom)
    {
        DropRoad();
        _carriedOff = true;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", CarriedOffLine(_character.Name, carriedFrom, _character.Location));
        }

        return Planned(SetOut) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>Drops the road being walked, so the trip sets out afresh from where the walker stands.</summary>
    private void DropRoad()
    {
        _walk?.Abort();
        _walk = null;
        _steps = null;
        _tileRouteMode = false;
        _walkingArrival = false;
        _leavingToStreet = false;
        _tileHopIndex = NoStep;
    }

    /// <summary>
    /// A step's failure <paramref name="reason"/> with the reason its walk gave, so the end line
    /// names the cause: 294 walks home ended "every way home failed" and nothing said why.
    /// </summary>
    public static string WithWalkWhy(string reason, Skill walk) =>
        string.IsNullOrEmpty(walk?.FailReason) ? reason : $"{reason}: {walk.FailReason}";

    /// <summary>The line a walker carried off its road writes, easy to count.</summary>
    public static string CarriedOffLine(string name, Point3D from, Point3D at) =>
        $"{name} was carried off its road from {from} to {at} and sets out again";

    private void NoteSeen()
    {
        _lastSeen = _character.Location;
        _lastSeenMap = _character.Map;
    }

    private SkillStatus ContinueFrom(int reachedIndex)
    {
        _index = reachedIndex;
        _walk = null;

        if (_index >= _steps.Count - 1)
        {
            return FinishOrArrival();
        }

        return StartWalkTo(_index + 1) ? SkillStatus.Running : SkillStatus.Failed;
    }

    private SkillStatus FinishOrArrival()
    {
        if (_arrival is not { } arrival)
        {
            return SkillStatus.Done;
        }

        _walkingArrival = true;
        _arrival = null;

        if (!NeedsTileLeg(_character.Location, arrival) && MapWalker().FloorNear(arrival.X, arrival.Y, arrival.Z) != null)
        {
            _walk = new GoToSkill(arrival, _range);
            return _walk.Begin(_character) ? SkillStatus.Running : SkillStatus.Failed;
        }

        // The arrival point can sit well past the pathfinder box from the last node, as
        // a bank counter does from the street node in front of it, or where nobody stands,
        // as the Spirituality shrine's does inside the shrine. Route it tile by tile: the
        // route ends on the nearest tile that stands.
        var tiles = TileRoute.Find(_character.Location, arrival, RouteWalker(), MapIndoor, _range, out var reason);

        if (tiles.Count == 0)
        {
            LogNoRoute(_character.Location, arrival, reason);
            return SkillStatus.Failed;
        }

        _walk = GoToSkill.FromPoints(tiles, WalkArrival.TileRouteEndRange(tiles[^1], arrival, _range));
        return _walk.Begin(_character) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>True when a plan steps through a public moongate anywhere on the way.</summary>
    public static bool UsesMoongate(IReadOnlyList<TravelStep> steps)
    {
        for (var i = 0; i < (steps?.Count ?? 0); i++)
        {
            if (steps[i].ArrivalGate == NavGateKind.Moongate)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMurderer => PkRules.IsRed(_character.Kills);

    /// <summary>The named destination nearest the traveler; a blue's never in the reds' town while another exists.</summary>
    private Destination ResolveDestination(DestinationCatalog catalog) =>
        catalog?.Resolve(_destination, _character.Location, avoidRedTown: !IsMurderer);

    /// <summary>
    /// One pathfinder leg covers a short hop. A longer arrival walk is planned over tiles,
    /// or the game pathfinder gives up on it.
    /// </summary>
    public static bool NeedsTileLeg(Point3D from, Point3D arrival) =>
        NavMetric.Chebyshev(from, arrival) > TileRoute.WaypointSpacing;

    private bool PlanFromHere()
    {
        var goal = ResolveTileGoal() ?? Point3D.Zero;
        var graph = NavWorld.GraphFor(_character.HomeFacet);

        // Ground that found no road back moments ago rests every trip, tile walks too: each
        // search from it only failed again.
        if (Traveler.RestsOnGround(graph, _character.Location))
        {
            return false;
        }

        if (goal != Point3D.Zero && TryTileWalk(goal))
        {
            return true;
        }

        var catalog = NavWorld.DestinationsFor(_character.HomeFacet);

        if (graph == null)
        {
            RememberFailedRoute(null, _destination, goal, Traveler.WhyNoGraph);
            return false;
        }

        if (QueueGraphPlan(graph, catalog, out var destName, out var whyNot))
        {
            return true;
        }

        RememberFailedRoute(graph, destName, goal, whyNot);
        return false;
    }

    /// <summary>
    /// No route starts where the character stands: walls close it in, or no node lies within a
    /// leg. Walk the tiles to the nearest node near it that the plan's own search routes
    /// (<see cref="Traveler.RoadBack"/>), then plan from there. Three characters stood in a
    /// walled yard in Britain for an hour without this. One tile search of a bounded size serves
    /// the whole trip. It spends one of the trip's replans, so a walk that cannot be made still ends.
    /// </summary>
    private bool ReturnToRoad(NavGraph graph, string destName, Dictionary<string, string> prev)
    {
        if (graph == null || !WalkRecoveryRules.MayReplanTrip(_replans))
        {
            return false;
        }

        _replans++;
        var tiles = Traveler.RoadBack(graph, _character.Location, destName, prev, MapWalker(), MapIndoor, RouteWalker());

        if (tiles.Count > 0)
        {
            _leavingToStreet = true;
            _walk = GoToSkill.FromPoints(tiles, CharactersFile.DefaultGoToRange, floorAtEnd: false);

            if (_walk.Begin(_character))
            {
                return true;
            }
        }

        _leavingToStreet = false;
        _walk = null;
        return false;
    }

    /// <summary>The place this trip is for, before any copy adjustment.</summary>
    public Point3D Goal => _target;

    private Point3D TileGoal()
    {
        var goal = _resolvedTarget != Point3D.Zero ? _resolvedTarget : _target;
        return _arrivalFloor ? goal : ArrivalHeight.PreferGround(_character?.Map, goal);
    }

    private Point3D? ResolveTileGoal()
    {
        if (TileGoal() != Point3D.Zero)
        {
            return TileGoal();
        }

        if (string.IsNullOrWhiteSpace(_destination))
        {
            return null;
        }

        var catalog = NavWorld.DestinationsFor(_character.HomeFacet);
        var dest = ResolveDestination(catalog);
        return dest?.ApproachPoint(NavWorld.GraphFor(_character.HomeFacet));
    }

    /// <summary>
    /// Walks to the goal on tiles when a tile route reaches it. A trip that reads its road for
    /// danger leaves a tile route that leads back toward a place it ran from to the road search,
    /// which pays to go round it: Anselm of Yew ran from Roland Bramble five times on one walk
    /// home, each time setting out on tiles straight back past him.
    /// </summary>
    private bool TryTileWalk(Point3D goal)
    {
        var tiles = TileRoute.Find(_character.Location, goal, RouteWalker(), MapIndoor, _range);

        if (tiles.Count == 0 || ReadsRoad && RoadPassesDanger(TravelPlan.OnFoot(tiles), BarringDanger()))
        {
            return false;
        }

        _tileRouteMode = true;
        _walk = GoToSkill.FromPoints(tiles, WalkArrival.TileRouteEndRange(tiles[^1], goal, _range));
        return _walk.Begin(_character);
    }

    /// <summary>
    /// Says once per cooldown why the trip has no route, and cools the trip down under its
    /// goal node or, with none, its goal tile, so nobody searches it again every think.
    /// </summary>
    /// <param name="noRecallWhy">With no road, why no recall carried the walker over it; the log line tells it.</param>
    private void RememberFailedRoute(NavGraph graph, string destName, Point3D tile, string why, string noRecallWhy = null)
    {
        var from = _character.Location;
        var target = Traveler.FailTarget(destName, tile);

        if (Traveler.InFailedCooldown(graph, from, target))
        {
            return;
        }

        if (tile != Point3D.Zero)
        {
            LogNoRoute(from, tile, RecallRules.NoRoadLine(why, noRecallWhy));
        }

        // The goal is at fault only when the start bound and searched cleanly:
        // no node sits near the goal, its nodes lie in a piece apart from the
        // walker's, or a bound start found no path to it. A
        // blocked first hop is the character's own ground — the marooned rescue
        // answers that, not a goal block.
        if (tile != Point3D.Zero &&
            graph != null &&
            (why is Traveler.WhyNoPath or Traveler.WhyNoGoalNode or Traveler.WhyGoalApart || !Traveler.GoalHasNode(graph, tile)))
        {
            _character.Memory.Unreachable.Note(tile, Core.Now);
        }

        if (Traveler.IsGroundFailure(why))
        {
            RestOnGround(graph, from);
        }

        Traveler.NoteFailedRoute(graph, from, target);
    }

    /// <summary>
    /// No plan starts from this ground and no walk back onto the road was found: every trip
    /// from here rests a while (<see cref="Traveler.GroundRestMs"/>), whatever its goal.
    /// </summary>
    private void RestOnGround(NavGraph graph, Point3D from)
    {
        Traveler.NoteGroundRest(graph, from);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", GroundRestLine(_character.Name, from));
        }
    }

    /// <summary>The line a walker with no road back onto the graph writes, easy to count.</summary>
    public static string GroundRestLine(string name, Point3D at) =>
        $"{name} found no road back from {at} and rests {TimeSpan.FromMilliseconds(Traveler.GroundRestMs).TotalSeconds} s before it plans again";

    /// <summary>
    /// A walk leg dies in silence otherwise: a three-minute stall in Britain wrote
    /// no line at all and the trip ended with nothing to diagnose. The line names the
    /// trip's goal and the leg the walker could not take; the graph node it was bound
    /// for once stood in the goal's place and read as a walker stalled on its own goal.
    /// </summary>
    private void LogStall()
    {
        ActivityPulse.NoteNoRoute();
        _character?.NoteTravelStall();
        var leg = _walk?.CurrentLeg;

        if (_character != null && leg is { } stuckOn)
        {
            StallTally.Note(_character.Location, stuckOn);
        }

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} stalled walking toward {Target} at {From} on leg {Leg}",
                _character?.Name,
                ResolveTileGoal() ?? TileGoal(),
                _character?.Location,
                leg
            );
        }
    }

    private void LogNoRoute(Point3D from, Point3D to, string why)
    {
        ActivityPulse.NoteNoRoute();

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} has no route from {From} to {To} ({Why})",
                _character.Name,
                from,
                to,
                why
            );
        }
    }

    /// <summary>
    /// A tile route that failed part way is planned again from where the character now
    /// stands. If it stands inside a building, it walks out first. It spends one of the
    /// trip's replans, so a walk that truly cannot be made still ends and the scorer moves on.
    /// </summary>
    private bool RetryTileWalk()
    {
        if (_character == null || !WalkRecoveryRules.MayReplanTrip(_replans))
        {
            return false;
        }

        _replans++;
        _tileRouteMode = false;
        return LeaveBuilding() || ResolveTileGoal() is { } goal && TryTileWalk(goal);
    }

    /// <summary>
    /// A failed graph leg plans again from where the character stands: out of a building,
    /// then the same hop on tiles, then a new route that pays for the marked edge.
    /// </summary>
    private bool RetryFromStreet()
    {
        if (_tileRouteMode || _character == null || !WalkRecoveryRules.MayReplanTrip(_replans))
        {
            return false;
        }

        _replans++;
        return Planned(() => LeaveBuilding() || HopByTiles() || PlanFromHere());
    }

    /// <summary>A graph hop between two nodes failed: later searches pay to use it.</summary>
    private void NoteFailedEdge()
    {
        if (OnWalkedLeg(out var from, out var to))
        {
            EdgeHealth.NoteFailure(_graph, from, to);
        }
    }

    /// <summary>
    /// The graph leg the walker is on: the two nodes of a walked hop of the plan. False on the
    /// arrival walk, at a gate, or with no plan.
    /// </summary>
    private bool OnWalkedLeg(out string from, out string to)
    {
        from = null;
        to = null;

        if (_graph == null || _walkingArrival || _steps is not { Count: > 0 } ||
            _walkTargetIndex <= 0 || _walkTargetIndex >= _steps.Count ||
            _steps[_walkTargetIndex].ArrivalGate != NavGateKind.None)
        {
            return false;
        }

        from = _steps[_walkTargetIndex - 1].Node;
        to = _steps[_walkTargetIndex].Node;
        return true;
    }

    /// <summary>
    /// A red on a road planned clear of the guards that meets them mid-leg, under them or held at
    /// their line by its own feet (<see cref="CharacterMotor.HeldAtGuardLine"/>), has shown that
    /// the leg crosses them (<see cref="GuardedLegs.Shows"/>): the walked path cuts into a town
    /// the straight leg missed. The leg is barred to reds from now on (<see cref="GuardedLegs.Learn"/>),
    /// and the trip plans again from where the red stands. Null while the walk keeps clear.
    /// </summary>
    private SkillStatus? StrayedIntoGuards()
    {
        if (!_roadClearOfGuards || _leavingToStreet ||
            !GuardedLegs.Shows(
                IsMurderer,
                _character.IsGhost,
                _roadClearOfGuards,
                _character.Motor.HeldAtGuardLine || SosariaCharacter.UnderGuards(_character)
            ) ||
            !OnWalkedLeg(out var from, out var to))
        {
            return null;
        }

        _roadClearOfGuards = false;
        var learned = GuardedLegs.Learn(_graph, _character.Map, from, to);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", StrayedLine(_character.Name, from, to, _character.Location, learned));
        }

        DropRoad();
        return Planned(SetOut) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>The line a red that met the guards on a leg writes, easy to count.</summary>
    public static string StrayedLine(string name, string from, string to, Point3D at, bool learned) =>
        $"{name} met the guards at {at} on the leg {from} to {to}{(learned ? LegBarredNote : string.Empty)} and plans again from here";

    /// <summary>
    /// A stalled hop usually sits at the mouth of a pocket the straight walk
    /// cannot leave, while the tile walker threads the gap. Finish the same hop
    /// on tiles once before the whole route is planned again into the same wall.
    /// </summary>
    private bool HopByTiles()
    {
        if (_walkingArrival || _steps is not { Count: > 0 } || _walkTargetIndex >= _steps.Count ||
            _tileHopIndex == _walkTargetIndex)
        {
            return false;
        }

        var step = _steps[_walkTargetIndex];

        if (step.ArrivalGate != NavGateKind.None)
        {
            return false;
        }

        var tiles = TileRoute.Find(_character.Location, step.Location, RouteWalker(), MapIndoor);

        if (tiles.Count == 0)
        {
            return false;
        }

        _tileHopIndex = _walkTargetIndex;
        _walk = GoToSkill.FromPoints(tiles, CharactersFile.DefaultGoToRange, floorAtEnd: false);
        return _walk.Begin(_character);
    }

    private bool LeaveBuilding()
    {
        var legs = BuildingLeave.PathOut(
            _character.Map,
            _character.Location,
            NavWorld.GraphFor(_character.HomeFacet)
        );

        if (legs.Count == 0)
        {
            return false;
        }

        _leavingToStreet = true;
        _walk = GoToSkill.FromPoints(legs, CharactersFile.DefaultGoToRange, floorAtEnd: false);
        return _walk.Begin(_character);
    }

    /// <summary>The walker's steps as the engine moves the character, floor by floor; the route proofs walk with it.</summary>
    private TileWalker MapWalker() => Standable.Walker(_character?.Map);

    /// <summary>The walker a tile route is searched with: a walk inside a dungeon goes round the pads it does not take.</summary>
    private TileWalker RouteWalker() => _avoidPads ? Standable.PadShyWalker(_character?.Map) : MapWalker();

    /// <summary>The places the road search keeps away from: remembered danger, and the trip's own <see cref="_keepClear"/>.</summary>
    private IReadOnlyList<Point3D> Avoid() => WithKeepClear(_character.Memory.Danger.Active(Core.Now));

    /// <summary>
    /// The places a road read refuses to walk past: each remembered place that still bars the
    /// road (<see cref="WalkRecoveryRules.DangerBarsRoad"/>), and the trip's own <see cref="_keepClear"/>.
    /// The road search still steers round every remembered place (<see cref="Avoid"/>).
    /// </summary>
    private IReadOnlyList<Point3D> BarringDanger()
    {
        var now = Core.Now;
        var sightings = _character.Memory.Danger.Sightings(now);
        var barring = new List<Point3D>(sightings.Count);

        for (var i = 0; i < sightings.Count; i++)
        {
            var (spot, notedAt) = sightings[i];

            if (WalkRecoveryRules.DangerBarsRoad(now - notedAt, () => ThreatHolds(spot)))
            {
                barring.Add(spot);
            }
        }

        return WithKeepClear(barring);
    }

    /// <summary>
    /// True when a threat this walker must run from stands at <paramref name="spot"/> now, read as
    /// the walker reads its own sight (<see cref="HuntSkill.ThreatAt"/>, <see cref="SosariaCharacter.MustRunFrom"/>):
    /// the reds still camped at the Minoc moongate bar Terrin of Jhelom's road however long ago he ran.
    /// </summary>
    private bool ThreatHolds(Point3D spot) => _character.MustRunFrom(HuntSkill.ThreatAt(_character, spot));

    /// <summary><paramref name="places"/> with the trip's own <see cref="_keepClear"/> added.</summary>
    private IReadOnlyList<Point3D> WithKeepClear(IReadOnlyList<Point3D> places)
    {
        if (_keepClear is not { Count: > 0 })
        {
            return places;
        }

        var all = new List<Point3D>(places.Count + _keepClear.Count);
        all.AddRange(places);
        all.AddRange(_keepClear);
        return all;
    }

    private bool MapIndoor(int x, int y, int z) => IndoorTiles.IsBuilding(_character?.Map, x, y, z);
}
