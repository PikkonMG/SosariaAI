using System;
using System.Collections.Generic;
using Server;
using Server.Guilds;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A red's run to the PvP hot spot its gang works this slot (see <see cref="HotSpots"/>): the
/// open ground round a moongate, a busy dungeon door or the Britain graveyard, one it
/// can walk to or recall to. A gang red in the Den first meets its gang at the Den's bank for a
/// short while, and the gang rides out one way: all on foot through the Den's moongate, or each
/// by recall to a rune by the camp; the first at the camp wait a short while for the rest (see
/// <see cref="GangRunPhase"/>). There it patrols the roads round the spot, and now and then the
/// gang lies in wait at the spot's camp, hidden when it can, shuffling when it cannot (see
/// <see cref="RedGangRules"/>). A red that drifts from its pack walks back to it. When the
/// pack is heavy, the fights go badly, the supplies run short or the camp slot is up, the run
/// ends and the gang on the spot rides back to the Den together (see
/// <see cref="RedGangRunRules"/>). A lawful fighter walks toward a reported PK attack. A PK
/// hunter with no report to answer rides out to hunt reds (see <see cref="PkHunterRules"/>): a
/// patrol of a hot spot the reds camp, now and then of Buccaneer's Den itself, chasing any red
/// it sees there (<see cref="FactionPatrolSkill"/>). Combat remains in WorldPlay, so this skill
/// never invents or teleports a target.
/// </summary>
public sealed class ConflictSkill : Skill
{
    public static readonly TimeSpan WatchTime = TimeSpan.FromMinutes(3);

    /// <summary>
    /// A camp is logged once when it starts: by the first of its gang to arrive, and again
    /// only when the gang left the spot for a whole camp slot. Each member and each lurk
    /// logged it before, 593 lines in 78 minutes.
    /// </summary>
    private static readonly LogGate<(int Crew, string Spot)> CampStarts = new(HotSpotRules.CampSlot);

    /// <summary>An ambush is logged once per gang and spot while it lasts.</summary>
    private static readonly LogGate<(int Crew, string Spot)> AmbushStarts = new(RedGangRules.LurkMax);

    /// <summary>A gang's ride home from a spot is logged once, by the red whose run ended first.</summary>
    private static readonly LogGate<(int Crew, string Spot)> RidesHome = new(HotSpotRules.CampSlot);

    // The eight directions of the engine, for a lurking red's shuffle.
    private const int Directions = 8;

    public const string ReportGoal = "the murder report";
    public const string TrafficGoal = "the busy ground";
    public const string NoReportWhy = "no murder report to answer";
    public const string NoGroundWhy = "no busy ground the red can reach";
    public const string NoConflictWhy = "no player conflict to seek";
    public const string NoStandingWhy = "nowhere to stand near the place";
    public const string BeyondLeashWhy = "the place lies beyond the leash";
    public const string GuardsInWayWhy = "the red cannot get there past the guards";
    public const string DiedWhy = "died on the outing";
    public const string NoHuntGroundWhy = "no red ground to hunt";

    /// <summary>The hunters who set out for the Den; <see cref="HuntersInDen"/> keeps those still on the run.</summary>
    private static readonly HashSet<Serial> DenHunters = [];

    public static string CampGoal(string spot) => $"the camp at {spot}";

    public static string NoWalkWhy(string goal) => $"no walk to {goal}";

    public static string WalkFailedWhy(string goal) => $"the walk to {goal} failed";

    private SosariaCharacter _character;
    private Skill _walk;
    private string _goal;
    private Point3D _reportSpot;
    private DateTime _arrivedAt;
    private HotSpot _spot;
    private Point3D _camp;
    private List<Point3D> _legs;
    private int _leg;
    private bool _toCamp;
    private bool _lurking;
    private DateTime _patrolEnds;
    private DateTime _nextAmbushRoll;
    private DateTime _lurkEnds;
    private DateTime _nextShuffle;
    private DateTime _nextCohesion;
    private DateTime _nextRunCheck;
    private int _lowHitsCount;
    private int _runsOff;
    private DateTime _lastRanFromAt;
    private bool _wasBelow;
    private bool _calledHome;
    private GangRunPhase _phase;
    private Point3D _musterPoint;
    private DateTime _phaseStarted;
    private bool _recalling;
    private FactionPatrolSkill _hunt;

    public override string Name => SkillKinds.Conflict;

    /// <summary>True for a PK hunter's run into Buccaneer's Den (see <see cref="PartyRoads.RidesAgainstDen"/>).</summary>
    public bool HuntsDen { get; private set; }

    /// <summary>What this outing is for, in plain words for chat (<see cref="ConflictRules.DoingPhrase"/>).</summary>
    public string Doing =>
        ConflictRules.DoingPhrase(_spot?.Name, _phase, _lurking, _hunt != null, HuntsDen, _reportSpot != Point3D.Zero);

    /// <summary>True while the red waits at the Den's muster point for its gang.</summary>
    public bool Mustering => _phase == GangRunPhase.Muster;

    /// <summary>True for a run to the hot spot named <paramref name="spot"/> that stands in <paramref name="phase"/>.</summary>
    public bool IsOn(string spot, GangRunPhase phase) =>
        _spot != null && _phase == phase && string.Equals(_spot.Name, spot, StringComparison.Ordinal);

    /// <summary>
    /// Why this red would not set out on a hot-spot run now (<see cref="RedGangRunRules.WhyNotSetOut"/>),
    /// or <see cref="HuntEndReason.None"/>: its pack against its carry limit, and its supplies.
    /// </summary>
    public static HuntEndReason WhyNotSetOut(SosariaCharacter red) =>
        RedGangRunRules.WhyNotSetOut(
            CarryLoad.IsLoaded(red, SosariaCombat.HuntPackFillFraction),
            SupplyCheck.IsLow(red)
        );

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _arrivedAt = default;
        _phase = GangRunPhase.Ride;
        _phaseStarted = Core.Now;
        _musterPoint = Point3D.Zero;
        _recalling = false;
        _spot = null;
        _camp = Point3D.Zero;
        _legs = null;
        _leg = RedGangRules.NoLeg;
        _toCamp = false;
        _lurking = false;
        _lowHitsCount = 0;
        _runsOff = 0;
        _lastRanFromAt = default;
        _wasBelow = false;
        _calledHome = false;
        _goal = null;
        _reportSpot = Point3D.Zero;
        _hunt = null;
        HuntsDen = false;

        if (character.Disposition == DispositionKind.Outlaw)
        {
            var stay = WhyNotSetOut(character);

            if (stay != HuntEndReason.None)
            {
                return CannotStart(
                    stay == HuntEndReason.SuppliesLow
                        ? $"{RedGangRunRules.Because(stay)} ({SupplyRules.FightStoppers(SupplyCheck.LowNeeds(character))})"
                        : RedGangRunRules.Because(stay)
                );
            }

            RedGang.JoinNearestGang(character);

            if (HotSpots.CampFor(character, Core.Now) is { } camp)
            {
                _spot = camp.Spot;
                _camp = camp.Camp;
                _musterPoint = MusterPoint(character);

                if (_musterPoint != Point3D.Zero && StartMuster())
                {
                    return true;
                }

                _musterPoint = Point3D.Zero;
                return StartRide();
            }
        }

        if (character.IsPkHunter && character.PkReportToAnswer() == null)
        {
            return StartHunt(character);
        }

        var lawful = character.Disposition == DispositionKind.Lawful;
        var report = lawful ? character.PkReportToAnswer() : null;
        var picked = lawful ? ConflictRules.SpotOf(report) : ResolveTarget(character);

        if (picked == Point3D.Zero)
        {
            return CannotStart(
                lawful ? NoReportWhy
                : character.Disposition == DispositionKind.Outlaw ? NoGroundWhy
                : NoConflictWhy
            );
        }

        var target = Standing(character.Map, picked);

        if (lawful)
        {
            _reportSpot = picked;
        }

        if (target == Point3D.Zero)
        {
            return RefuseReport(NoStandingWhy);
        }

        // A red with a camp walks to it however far it lies; any other seeker stays on its
        // leash, and a red with no camp in reach goes only where it can get past the guards.
        if (HomeLeash.BeyondLeash(target, character.Location, HomeLeash.ConfiguredRadius()))
        {
            return RefuseReport(BeyondLeashWhy);
        }

        if (character.Disposition == DispositionKind.Outlaw && !RedGangReach.CanReach(character, target))
        {
            return CannotStart(GuardsInWayWhy);
        }

        if (!StartWalk(target, lawful ? ReportGoal : TrafficGoal))
        {
            return false;
        }

        if (report != null)
        {
            report.Answerers++;
        }

        return true;
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted || !_character.Alive)
        {
            return Fail(DiedWhy);
        }

        if (_calledHome)
        {
            Abort();
            return SkillStatus.Done;
        }

        if (_hunt != null)
        {
            return _hunt.Tick();
        }

        switch (_phase)
        {
            case GangRunPhase.Muster:
                return TickMuster();
            case GangRunPhase.Ride:
                return TickRide();
            case GangRunPhase.Gather:
                return TickGather();
        }

        if (_spot == null)
        {
            return Core.Now - _arrivedAt >= WatchTime ? SkillStatus.Done : SkillStatus.Running;
        }

        if (RunIsOver())
        {
            return SkillStatus.Done;
        }

        if (_lurking)
        {
            return Lurk();
        }

        if (!_toCamp)
        {
            KeepWithPack();
            RollForAmbush();
        }

        return Patrol();
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _hunt?.Abort();
    }

    public override void Resume(TimeSpan held)
    {
        _walk?.Resume(held);
        _hunt?.Resume(held);
        _arrivedAt = SkillClock.Shift(_arrivedAt, held);
        _phaseStarted = SkillClock.Shift(_phaseStarted, held);
        _patrolEnds = SkillClock.Shift(_patrolEnds, held);
        _nextAmbushRoll = SkillClock.Shift(_nextAmbushRoll, held);
        _lurkEnds = SkillClock.Shift(_lurkEnds, held);
        _nextShuffle = SkillClock.Shift(_nextShuffle, held);
        _nextCohesion = SkillClock.Shift(_nextCohesion, held);
        _nextRunCheck = SkillClock.Shift(_nextRunCheck, held);
    }

    /// <summary>
    /// A PK hunter's run: into Buccaneer's Den now and then while few hunters ride there
    /// (<see cref="PkHunterRules.RidesToDen"/>), else the hot spot a blue sweep would take. The
    /// Den lies past every blue's leash, and the hunter rides there all the same.
    /// </summary>
    private bool StartHunt(SosariaCharacter hunter)
    {
        var den = PkHunterRules.RidesToDen(Utility.Random(int.MaxValue), HuntersInDen())
            ? HotSpots.DenSpot(hunter.Map)
            : null;

        if ((den ?? HotSpots.SweepTarget(hunter, Core.Now)) is not { } ground)
        {
            return CannotStart(NoHuntGroundWhy);
        }

        var hunt = new FactionPatrolSkill(ground, GuildType.Regular);

        if (!hunt.Begin(hunter))
        {
            return CannotStart(NoWalkWhy(ground.Name));
        }

        _hunt = hunt;
        HuntsDen = den != null;

        if (HuntsDen)
        {
            DenHunters.Add(hunter.Serial);
        }

        WorldPlay.Log($"pk hunter: {hunter.Name} rides out to {ground.Name}");
        return true;
    }

    /// <summary>The PK hunters on a run into the Den now; one whose run ended is let go.</summary>
    private static int HuntersInDen()
    {
        DenHunters.RemoveWhere(serial =>
            World.FindMobile(serial) is not SosariaCharacter { Routine.CurrentSkill: ConflictSkill { HuntsDen: true } });
        return DenHunters.Count;
    }

    /// <summary>A gang mate started an ambush: a patrolling red goes to lie in wait with it.</summary>
    public void JoinAmbush()
    {
        if (_spot != null && _arrivedAt != default && !_lurking && !_toCamp)
        {
            StartAmbush();
        }
    }

    /// <summary>
    /// A gang mate's run at <paramref name="spot"/> is over: a red on its way to the same spot,
    /// or on it, rides home with the gang. A run to another spot goes on.
    /// </summary>
    public void CallHome(string spot)
    {
        if (_spot != null && string.Equals(_spot.Name, spot, StringComparison.Ordinal))
        {
            _calledHome = true;
        }
    }

    // Now and then the red looks at its pack, its wounds and its supplies; when the run is
    // over it calls the gang on the spot home with it.
    private bool RunIsOver()
    {
        var now = Core.Now;

        if (now < _nextRunCheck)
        {
            return false;
        }

        _nextRunCheck = now + RedGangRunRules.RunCheck;
        var hits = Vitals.HitsFraction(_character);
        var below = hits < RedGangRunRules.HeadHomeBelowHitsFraction;
        _lowHitsCount = HuntEndDecision.CountLowHits(_lowHitsCount, _wasBelow, below);
        _wasBelow = below;

        if (_character.LastRanFromAt != _lastRanFromAt)
        {
            _runsOff++;
            _lastRanFromAt = _character.LastRanFromAt;
        }

        var reason = RedGangRunRules.WhyHeadHome(
            now,
            _patrolEnds,
            CarryLoad.IsLoaded(_character, SosariaCombat.HuntPackFillFraction),
            hits,
            _lowHitsCount,
            SupplyCheck.IsLow(_character),
            _runsOff
        );

        if (reason == HuntEndReason.None)
        {
            return false;
        }

        Abort();
        RedGang.CallHome(_character, _spot.Name);

        if (RidesHome.Opens((HotSpots.CrewKey(_character), _spot.Name), now))
        {
            WorldPlay.Log(
                $"{_character.Name} rides back to the Den from {_spot.Name} with a pack of {RedGang.PackSize(_character)}: {RedGangRunRules.Because(reason)}"
            );
        }

        return true;
    }

    /// <summary>Sets out for the outing's goal: a camp, the busy ground or a murder report.</summary>
    private bool StartWalk(Point3D target, string goal, bool onFoot = false)
    {
        _goal = goal;
        _walk = new TravelSkill(target, CharactersFile.DefaultGoToRange, onFoot: onFoot);
        return _walk.Begin(_character) || RefuseReport(NoWalkWhy(goal));
    }

    /// <summary>
    /// The red walks to the Den's muster point, or waits there when it stands on it already.
    /// False when no walk leads there: the red sets out from where it stands.
    /// </summary>
    private bool StartMuster()
    {
        _phase = GangRunPhase.Muster;
        _phaseStarted = Core.Now;

        if (NavMetric.Chebyshev(_character.Location, _musterPoint) <= RedGangRules.MusterTiles)
        {
            return true;
        }

        _walk = new TravelSkill(_musterPoint, RedGangRules.MusterTiles);

        if (_walk.Begin(_character))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    /// <summary>
    /// Sets out for the camp. A red that mustered in the Den takes its gang's way (see
    /// <see cref="RedGang.RideWay"/>): by rune, it recalls to the camp and walks the rest;
    /// on foot, the road leads all of them through the Den's moongate.
    /// </summary>
    private bool StartRide()
    {
        _phase = GangRunPhase.Ride;
        _phaseStarted = Core.Now;

        var way = _musterPoint != Point3D.Zero ? RedGang.RideWay(_character, _spot.Name, _camp, _musterPoint) : (GangRideWay?)null;

        return way == GangRideWay.ByRune && StartRecall() ||
               StartWalk(_camp, CampGoal(_spot.Name), onFoot: way == GangRideWay.OnFoot);
    }

    private bool StartRecall()
    {
        _walk = new RecallSkill(_camp, RecallRules.NoRoadMinTripTiles);
        _recalling = _walk.Begin(_character);

        if (!_recalling)
        {
            _walk = null;
        }

        return _recalling;
    }

    /// <summary>
    /// At the Den's muster point: the red waits for the gang mates the call brought along, at
    /// least <see cref="RedGangRules.MusterMin"/> and at most <see cref="RedGangRules.MusterMax"/>,
    /// and a red late to a gang that rode out already follows it at once.
    /// </summary>
    private SkillStatus TickMuster()
    {
        var now = Core.Now;
        var rollCall = RedGang.WaitForRunMates(
            _character,
            _spot.Name,
            GangRunPhase.Muster,
            _musterPoint,
            RedGangRules.MusterTiles,
            _phaseStarted,
            RedGangRules.MusterMax,
            now
        );
        var rodeOut = RedGang.RodeOut(_character, _spot.Name, now);

        if (_walk != null)
        {
            if (_walk.Tick() == SkillStatus.Running)
            {
                if (!rodeOut && rollCall != PartyWaitResult.TimedOut)
                {
                    return SkillStatus.Running;
                }

                _walk.Abort();
            }

            _walk = null;
        }

        if (!rodeOut && !RedGangRules.MusterOver(rollCall, _phaseStarted, now))
        {
            _character.Motor.ClearMoveIntent();
            return SkillStatus.Running;
        }

        return StartRide() ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>
    /// On the way to the goal. A recall to the camp's rune walks the rest from where it
    /// landed, or the whole way when it would not take. At a camp the red waits for its gang.
    /// </summary>
    private SkillStatus TickRide()
    {
        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return status;
        }

        _walk = null;

        if (_recalling)
        {
            _recalling = false;
            return StartWalk(_camp, CampGoal(_spot.Name)) ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (status == SkillStatus.Failed)
        {
            NoteReportUnreachable();
            return Fail(WalkFailedWhy(_goal));
        }

        if (_spot == null)
        {
            Arrive();
        }
        else
        {
            _phase = GangRunPhase.Gather;
            _phaseStarted = Core.Now;
        }

        return SkillStatus.Running;
    }

    /// <summary>
    /// At the camp: the first there wait for the gang mates still riding to it, at most
    /// <see cref="RedGangRules.GatherMax"/>, before they work the spot together.
    /// </summary>
    private SkillStatus TickGather()
    {
        if (RedGang.WaitForRunMates(
                _character,
                _spot.Name,
                GangRunPhase.Ride,
                _camp,
                RedGangRules.GatherTiles,
                _phaseStarted,
                RedGangRules.GatherMax,
                Core.Now
            ) == PartyWaitResult.Waiting)
        {
            _character.Motor.ClearMoveIntent();
            return SkillStatus.Running;
        }

        Arrive();
        return SkillStatus.Running;
    }

    /// <summary>
    /// Where a gang red in the Den meets its gang before a run: the Den's bank, where the reds
    /// hang about between runs. Zero for a red with no gang or out of the Den.
    /// </summary>
    private static Point3D MusterPoint(SosariaCharacter red)
    {
        var bank = BankPlaza.BankFor(NavWorld.DestinationsFor(red.Map?.Name), PkRules.BucsDenHaven);

        return RedGangRules.Musters(
            red.OutlawGang != PkGangRules.NoGang,
            PkRules.InBuccaneersDen(red.X, red.Y),
            PkRules.InBuccaneersDen(bank.X, bank.Y)
        )
            ? bank
            : Point3D.Zero;
    }

    /// <summary>
    /// A murder report the blue could not start toward is kept off its list a while (see
    /// <see cref="ConflictRules.BlueRides"/>), so the next choice is another report or other work.
    /// </summary>
    private bool RefuseReport(string why)
    {
        NoteReportUnreachable();
        return CannotStart(why);
    }

    private void NoteReportUnreachable()
    {
        if (_reportSpot != Point3D.Zero)
        {
            _character.Memory.Unreachable.Note(_reportSpot, Core.Now);
        }
    }

    private void Arrive()
    {
        var now = Core.Now;
        _arrivedAt = now;
        _phase = GangRunPhase.Work;

        if (_spot == null)
        {
            return;
        }

        _patrolEnds = now + HotSpotRules.CampSlot;
        _lastRanFromAt = _character.LastRanFromAt;
        _nextAmbushRoll = now + Roll(RedGangRules.AmbushRollMin, RedGangRules.AmbushRollMax);
        _legs = PatrolLegs(_character.Map, _spot);

        if (CampStarts.Opens((HotSpots.CrewKey(_character), _spot.Name), now))
        {
            WorldPlay.Log($"{_character.Name} patrols {_spot.Name} with a pack of {RedGang.PackSize(_character)}");
        }
    }

    // Walking the roads round the spot, one road node after another. WorldPlay strikes.
    private SkillStatus Patrol()
    {
        if (_walk != null)
        {
            var status = _walk.Tick();

            if (status == SkillStatus.Running)
            {
                return status;
            }

            _walk = null;

            if (_toCamp)
            {
                _toCamp = false;

                if (status == SkillStatus.Done)
                {
                    BeginLurk();
                }

                return SkillStatus.Running;
            }
        }

        _leg = RedGangRules.NextLeg(_legs.Count, _leg, Utility.Random(int.MaxValue));

        // No road round the spot: the gang waits at its camp instead.
        if (_leg == RedGangRules.NoLeg)
        {
            BeginLurk();
            return SkillStatus.Running;
        }

        _walk = new TravelSkill(_legs[_leg], CharactersFile.DefaultGoToRange);

        if (!_walk.Begin(_character))
        {
            _walk = null;
            _legs.RemoveAt(_leg);
            _leg = RedGangRules.NoLeg;
        }

        return SkillStatus.Running;
    }

    // Every few minutes the gang rolls to lie in wait at the camp.
    private void RollForAmbush()
    {
        if (Core.Now < _nextAmbushRoll)
        {
            return;
        }

        _nextAmbushRoll = Core.Now + Roll(RedGangRules.AmbushRollMin, RedGangRules.AmbushRollMax);

        if (!RedGangRules.ShouldAmbush(Utility.Random(OutlawRules.PercentScale)))
        {
            return;
        }

        StartAmbush();

        if (_toCamp)
        {
            RedGang.CallToAmbush(_character);
        }
    }

    private void StartAmbush()
    {
        _walk?.Abort();
        _walk = new TravelSkill(_camp, CharactersFile.DefaultGoToRange);
        _toCamp = _walk.Begin(_character);

        if (!_toCamp)
        {
            _walk = null;
        }
    }

    private void BeginLurk()
    {
        var now = Core.Now;
        _lurking = true;
        _lurkEnds = now + Roll(RedGangRules.LurkMin, RedGangRules.LurkMax);
        _nextShuffle = now;

        if (AmbushStarts.Opens((HotSpots.CrewKey(_character), _spot.Name), now))
        {
            WorldPlay.Log($"{_character.Name} lies in wait at {_spot.Name} with a pack of {RedGang.PackSize(_character)}");
        }
    }

    // Waiting at the camp: hidden if the red can hide, shuffling about otherwise. WorldPlay strikes.
    private SkillStatus Lurk()
    {
        var now = Core.Now;

        if (now >= _lurkEnds)
        {
            _lurking = false;
            return SkillStatus.Running;
        }

        if (!_character.Hidden && _character.Skills.Hiding.Value >= PkGangRules.LurkHiding)
        {
            Server.Skills.UseSkill(_character, SkillName.Hiding);
        }

        if (_character.Hidden)
        {
            _character.Motor.ClearMoveIntent();
            return SkillStatus.Running;
        }

        if (now >= _nextShuffle)
        {
            _nextShuffle = now + Roll(RedGangRules.ShuffleMin, RedGangRules.ShuffleMax);

            if (RedGangRules.ShuffleStepsBack(NavMetric.Chebyshev(_character.Location, _camp)))
            {
                _character.Motor.MoveToPoint(_camp);
            }
            else
            {
                _character.Motor.DoMove((Direction)Utility.Random(Directions));
            }
        }

        return SkillStatus.Running;
    }

    // A red that drifted from its pack walks back to its nearest mate.
    private void KeepWithPack()
    {
        if (Core.Now < _nextCohesion)
        {
            return;
        }

        _nextCohesion = Core.Now + RedGangRules.CohesionCheck;

        if (RedGang.NearestPackmate(_character) is not { } mate ||
            !RedGangRules.StraysFromPack(NavMetric.Chebyshev(_character.Location, mate.Location)))
        {
            return;
        }

        _walk?.Abort();
        _walk = new TravelSkill(mate.Location, RedGangRules.RegroupTiles);

        if (!_walk.Begin(_character))
        {
            _walk = null;
        }
    }

    private static TimeSpan Roll(TimeSpan min, TimeSpan max) => RedGangRules.Between(min, max, Utility.RandomDouble());

    /// <summary>The road nodes round a hot spot a gang patrols: outdoors, near the spot and out of the guards' reach.</summary>
    private static List<Point3D> PatrolLegs(Map map, HotSpot spot)
    {
        var legs = new List<Point3D>();
        var graph = NavWorld.GraphFor(map?.Name);

        if (graph == null)
        {
            return legs;
        }

        foreach (var node in graph.FindNearest(spot.Center, RedGangRules.PatrolNodeScan))
        {
            if (!node.Indoor && NavMetric.Chebyshev(node.Location, spot.Center) <= RedGangRules.PatrolRadiusTiles &&
                !GuardCall.IsGuardedPlace(node.Location, map))
            {
                legs.Add(node.Location);
            }
        }

        return legs;
    }

    /// <summary>
    /// The tile a walker can stand on at, or nearest to, a target: a PK report can name the
    /// spot a body fell from a roof or into the water, and a walk to a tile nobody can stand
    /// on stalls. Zero when none stands within <see cref="TileRoute.SnapRadius"/>.
    /// </summary>
    private static Point3D Standing(Map map, Point3D spot)
    {
        if (spot == Point3D.Zero || map == null || map == Map.Internal)
        {
            return Point3D.Zero;
        }

        var walker = Standable.Walker(map);

        if (walker.FloorNear(spot.X, spot.Y, spot.Z) is { } floor)
        {
            return new Point3D(spot.X, spot.Y, floor);
        }

        return TileRoute.TryNearestStanding(spot, walker, TileRoute.SnapRadius, out var standing) ? standing : Point3D.Zero;
    }

    private static Point3D ResolveTarget(SosariaCharacter character)
    {
        if (character.Disposition != DispositionKind.Outlaw)
        {
            return Point3D.Zero;
        }

        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        return ConflictRules.PickTraffic(catalog, character.Location, unchecked((int)character.Serial.Value));
    }
}
