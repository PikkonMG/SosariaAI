using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Navigation.Generation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A run into a catalog dungeon hall. A mage marks a rune at home before it sets out. The
/// crew musters with its leader; when the leader or any member near can cast Gate Travel
/// toward a rune marked at the door, the group goes through one gate together (see
/// <see cref="PartyGate"/>), and a lone mage opens its own. Else a person with a door rune
/// recalls to the door first, from any trip of <see cref="DungeonEntryRules.RecallMinTripTiles"/>
/// or more, and casts a fizzle again (<see cref="DungeonEntryRules.DoorRecallCasts"/>); a group
/// leader does so when its whole crew can recall after it. A lone traveller left with a long
/// walk turns to a nearer dungeon, and a blue's walk keeps clear of the reds' town gate. The
/// rest of the way in is three legs (see <see cref="DungeonEntryRules"/>): the surface walk to
/// the real door pad, where a mage with a blank rune marks the door, the step onto it (another
/// pad of the door when one will not take), and the walk inside to the hall, round the pads it
/// does not take. A walk inside that cannot reach the hall crawls the floor it reached, and a
/// pad inside that carries the person out sends it back in by the door. Inside, a camper
/// holds the hall and then the floor for a long run; a crawler fights room by room across
/// the floor, rolling each next room or the stair down
/// by its tier and toward the rooms with prey in reach (see <see cref="DungeonCrawlRules.NextStop"/>),
/// walks on from a room with no prey in sight, and takes the stair once rooms in a row give
/// nothing and no room of the floor shows prey; it walks off the landing below to a room. A
/// stair down counts only to a floor that fits the crawler's power with its pets and party
/// (<see cref="DungeonGround.FightingPower"/>); a bare floor whose stairs down all lead too
/// deep sends it up a stair that fits, or out. A floor past its reach, or one it lost on
/// (<see cref="DungeonCrawlRules.LosingWhy"/>: runs, a fallen pet or mate, bad wounds), is left
/// the same way, and a lost floor stays too hard for it a while. Each floor taken is logged with
/// the crawler's power against the floor's difficulty. The run timer, bad wounds, a
/// full pack or spent supplies bring the person back out by the stairs, and the log says
/// which; from the door the way home recalls when a home rune pays. A crawl that reached no
/// room fails the trip. A run that ends rolls the next one's dungeon again
/// (<see cref="SosariaCharacter.RollDungeonAgain"/>); a run another job breaks off writes its
/// end (<see cref="DungeonAbort"/>).
/// A group leader holds at the door and between rooms for a member left behind, calls the
/// stairs in party chat, and tells the group board each new floor.
/// </summary>
public sealed class DungeonTripSkill : Skill, IHuntingSkill
{
    /// <summary>A mage this close to its home spot marks its home rune before it leaves.</summary>
    public const int HomeMarkTiles = 12;

    /// <summary>A lone traveller holds its open gate this long before it steps in.</summary>
    public static readonly TimeSpan GateHold = TimeSpan.FromSeconds(3);

    private const string OwnGateEvent = "walked through its own gate";
    private const string HeadsForVerb = "heads for";
    private const string GoesDownVerb = "goes down to";
    private const string GoesUpVerb = "goes up to";
    private const string EnterEvent = "entered a dungeon";

    private static readonly ILogger logger = SosariaLog.For(typeof(DungeonTripSkill));

    private enum Phase
    {
        MarkHome,
        OpenGate,
        GroupGate,
        RecallToDoor,
        Enter,
        MarkDoor,
        Camp,
        Crawl,
        WalkHome
    }

    private readonly string _partyId;
    private readonly List<Point3D> _cleared = [];
    private readonly HashSet<int> _sweptFloors = [];
    private readonly DelveFailure _failure = new();
    private readonly List<Teleporter> _doorPads = [];
    private readonly List<(Point3D Pad, bool LandsInside)> _doorPadLinks = [];
    private Point3D _hall;
    private HuntSkill _hunt;
    private TravelSkill _walkIn;
    private TravelSkill _walkHome;
    private TravelSkill _descent;
    private DungeonLeg _leg;
    private Point3D _door;
    private int _padPick;
    private Teleporter _padItem;
    private Point3D _pad;
    private Point3D _landing;
    private DateTime _padStarted;
    private IReadOnlyList<Point3D> _keepClear;
    private int _doorRecalls;
    private DateTime _recallRetryEnds;
    private int _reentries;
    private bool _crawlFromLanding;
    private int _roomsReached;
    private string _leaveReason;
    private bool _crawlFailed;
    private bool _finished;
    private HuntSkill _roomHunt;
    private Skill _failedStop;
    private SosariaCharacter _character;
    private Phase _phase;
    private bool _waitingForParty;
    private DateTime _crawlStarted;
    private bool _suppliesLowAtCrawl;
    private DateTime _gateOpenedAt;
    private DateTime _holdStarted;
    private bool _holdDone;
    private bool _betweenRooms;
    private bool _walkHomeFromInside;
    private TimeSpan _runLength;
    private int _seed;
    private int _rooms;
    private int _roomFailures;
    private int _bareRooms;
    private int _floorId;
    private DungeonFloor? _watched;
    private int _runsAtFloor;
    private int _petsAtFloor;
    private int _matesDownAtFloor;
    private double _hitsAtFloor;

    public DungeonTripSkill(Point3D hall, HuntSkill hunt, string partyId)
    {
        _hall = hall;
        _hunt = hunt;
        _partyId = partyId;
    }

    public override string Name => SkillKinds.Dungeon;

    /// <summary>The dungeon of the hall this run goes to.</summary>
    public override JobTarget? AimedAt =>
        _character?.Map == null ? null : DungeonGround.RegionNames(_character.Map)(_hall) is { } dungeon ? new JobTarget(dungeon, Point3D.Zero) : null;

    /// <summary>The hall this run goes to.</summary>
    public Point3D Hall => _hall;

    /// <summary>True once the character stood inside a dungeon on this trip.</summary>
    public bool ReachedInside { get; private set; }

    /// <summary>The run is over and the person walks home.</summary>
    public bool HeadingHome => _phase == Phase.WalkHome;

    public bool IsHunting =>
        (_phase == Phase.Camp && _hunt.IsHunting) || (_phase == Phase.Crawl && _roomHunt?.IsHunting == true);

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _failure.Reset();
        _waitingForParty = false;
        _walkIn = null;
        _walkHome = null;
        _descent = null;
        _roomHunt = null;
        _failedStop = null;
        _cleared.Clear();
        _sweptFloors.Clear();
        _rooms = 0;
        _roomFailures = 0;
        _bareRooms = 0;
        _floorId = -1;
        _watched = null;
        _betweenRooms = false;
        _walkHomeFromInside = false;
        _doorRecalls = 0;
        _recallRetryEnds = default;
        _reentries = 0;
        _crawlFromLanding = false;
        _roomsReached = 0;
        _leaveReason = null;
        _crawlFailed = false;
        _finished = false;
        _crawlStarted = default;
        _keepClear = DungeonGround.KeepClear(character);
        NextWaypoint();
        _seed = Utility.Random(int.MaxValue);
        ReachedInside = InDungeon(character);

        if (!ReachedInside)
        {
            LogHall(HeadsForVerb);
        }

        if (!string.IsNullOrEmpty(_partyId) && Party.IsLeader(_partyId, character.CharacterId))
        {
            Party.StartWait(character);
            _waitingForParty = true;
        }

        if (!ReachedInside &&
            NavMetric.Chebyshev(character.Location, character.HomeSpot) <= HomeMarkTiles &&
            MarkRules.TryMarkHere(character))
        {
            _phase = Phase.MarkHome;
            return true;
        }

        return (ReachedInside ? StartCrawl() : StartWalkIn()) || Refused();
    }

    public override SkillStatus Tick()
    {
        if (_character.IsGhost)
        {
            return Ended(Checked(SkillStatus.Failed, DelveFailure.Died));
        }

        Party.TickCare(_character);

        if (_waitingForParty && _phase != Phase.MarkHome)
        {
            if (Party.TickWait(_character) == PartyWaitResult.Waiting)
            {
                return SkillStatus.Running;
            }

            _waitingForParty = false;
        }

        if (CarriedOut())
        {
            return Ended(Checked(Reenter(), FailureReason()));
        }

        var status = _phase switch
        {
            Phase.MarkHome => AfterCast(StartWalkIn),
            Phase.OpenGate => AdvanceGate(),
            Phase.GroupGate => AdvanceGroupGate(),
            Phase.RecallToDoor => AdvanceRecall(),
            Phase.Enter => AdvanceEnter(),
            Phase.MarkDoor => AfterCast(() => BeginLeg(DungeonLeg.StepOnPad)),
            Phase.Camp => AdvanceCamp(),
            Phase.Crawl => AdvanceCrawl(),
            _ => Finish(TickWalkHome())
        };

        return Ended(Checked(status, FailureReason()));
    }

    /// <summary>A run that ended, done or failed, sends the next one to a dungeon rolled again.</summary>
    private SkillStatus Ended(SkillStatus status)
    {
        if (status != SkillStatus.Running)
        {
            _finished = true;
            _character.RollDungeonAgain();
        }

        return status;
    }

    /// <summary>A trip refused at its start says why and rolls the next dungeon again too.</summary>
    private bool Refused()
    {
        _character.RollDungeonAgain();
        var why = FailureReason();
        _failure.Refuse(_character, why);
        return CannotStart(why);
    }

    /// <summary>Passes a tick status through; a failure says why once a trip and carries the reason to its end line.</summary>
    private SkillStatus Checked(SkillStatus status, string reason) =>
        _failure.Check(_character, status, reason) == SkillStatus.Failed ? Fail(reason) : status;

    /// <summary>
    /// Why the trip failed, read from the phase and leg it failed in. A trip fails on the way
    /// in, at its first hunt, or when its crawl reached no room: the walk home turns a failed
    /// walk into a done trip. A walk that failed on the way in adds its own reason.
    /// </summary>
    private string FailureReason() =>
        _phase switch
        {
            Phase.Camp => DelveFailure.HallEmpty,
            Phase.Crawl or Phase.WalkHome => DelveFailure.NoRoomReached,
            _ => TravelSkill.WithWalkWhy(DungeonEntryRules.FailureReason(_leg), _walkIn)
        };

    /// <summary>A run another job breaks off while it runs is noted, so the next job's start writes its end.</summary>
    public override void Abort()
    {
        if (!_finished)
        {
            DungeonAbort.Note(_character);
        }

        _walkIn?.Abort();
        _hunt.Abort();
        _roomHunt?.Abort();
        _descent?.Abort();
        _walkHome?.Abort();
        _character?.RestoreTownStance();
    }

    public override void Resume(TimeSpan held)
    {
        _walkIn?.Resume(held);
        _hunt.Resume(held);
        _roomHunt?.Resume(held);
        _descent?.Resume(held);
        _walkHome?.Resume(held);
        _crawlStarted = SkillClock.Shift(_crawlStarted, held);
        _gateOpenedAt = SkillClock.Shift(_gateOpenedAt, held);
        _padStarted = SkillClock.Shift(_padStarted, held);
        _holdStarted = SkillClock.Shift(_holdStarted, held);
    }

    /// <summary>A mark cast runs to its end, taken or not; the trip goes on either way.</summary>
    private SkillStatus AfterCast(Func<bool> next) =>
        TravelSpells.TakeOutcome(_character) == TravelCastOutcome.Casting
            ? SkillStatus.Running
            : next() ? SkillStatus.Running : SkillStatus.Failed;

    /// <summary>
    /// The way to the door, best first: a group leader's run takes one gate for the whole
    /// group, a lone mage opens its own; then a recall, then the walk.
    /// </summary>
    private bool StartWalkIn()
    {
        _gateOpenedAt = default;
        var leads = LfgBoard.LeadsRun(_character);

        if (leads
                ? PartyGate.TryOpen(_character, _hall, DungeonEntryRules.RecallMinTripTiles, DungeonName())
                : GateRules.TryOpenToward(_character, _hall, DungeonEntryRules.RecallMinTripTiles))
        {
            _phase = leads ? Phase.GroupGate : Phase.OpenGate;
            return true;
        }

        return RecallOrWalk();
    }

    private bool RecallOrWalk() => BeginDoorRecall() || WalkFromHere();

    /// <summary>The first cast of the recall to the door. False when no recall to it can begin.</summary>
    private bool BeginDoorRecall()
    {
        if (!TryRecallToDoor())
        {
            return false;
        }

        _doorRecalls = 1;
        _phase = Phase.RecallToDoor;
        return true;
    }

    /// <summary>
    /// The leader at the group's gate: it holds while the words are spoken, steps in first,
    /// and holds at the door until the caster came through last. A gate that never opened
    /// leaves the recall or the walk; one the group crossed leaves the walk from the door.
    /// </summary>
    private SkillStatus AdvanceGroupGate() =>
        PartyGate.Turn(_character) != GateTurn.None ? SkillStatus.Running
        : RecallOrWalk() ? SkillStatus.Running
        : SkillStatus.Failed;

    /// <summary>
    /// Begins a recall to the mark that lands at this dungeon's door. The recall pays from
    /// <see cref="DungeonEntryRules.RecallMinTripTiles"/> up; a door close by is walked. A
    /// person in a group recalls only as the leader of a group call whose whole crew can
    /// recall after it.
    /// </summary>
    private bool TryRecallToDoor()
    {
        var grouped = GameParty.InParty(_character) || !string.IsNullOrEmpty(_partyId);

        return DungeonEntryRules.RecallsToDoor(
                   InDungeon(_character),
                   grouped,
                   grouped && LfgBoard.CrewCanRecallTo(_character, _hall, DungeonEntryRules.RecallMinTripTiles)
               ) &&
               RecallRules.TryRecallToward(_character, _hall, DungeonEntryRules.RecallMinTripTiles);
    }

    /// <summary>
    /// A recall that took is written once. One that fizzled is cast again once the book and
    /// the caster are ready, a few times, as players recast; then the walk does the trip.
    /// </summary>
    private SkillStatus AdvanceRecall()
    {
        switch (TravelSpells.TakeOutcome(_character))
        {
            case TravelCastOutcome.Casting:
                return SkillStatus.Running;
            case TravelCastOutcome.Succeeded:
                if (SosariaSettings.LogActivity)
                {
                    logger.Information("{Line}", DungeonEntryRules.RecalledToDoorLine(_character.Name, DungeonName()));
                }

                return BeginWalkIn() ? SkillStatus.Running : SkillStatus.Failed;
            case TravelCastOutcome.Fizzled:
                _recallRetryEnds = Core.Now + DungeonEntryRules.DoorRecallRetryWait;
                break;
        }

        return RecallAgain() || WalkFromHere() ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>
    /// Casts the fizzled door recall again, or waits for it while a resting book or a cast
    /// still wearing off holds it back. False when no cast is left or the recall will not come.
    /// </summary>
    private bool RecallAgain()
    {
        if (!DungeonEntryRules.RecallsAgain(_doorRecalls, Core.Now, _recallRetryEnds))
        {
            return false;
        }

        if (TryRecallToDoor())
        {
            _doorRecalls++;
            return true;
        }

        return TravelSpells.Passes(RecallRules.WhyNoRecall(_character, _hall, DungeonEntryRules.RecallMinTripTiles));
    }

    /// <summary>
    /// No gate or recall took: the trip walks from here. A lone traveller facing a long walk
    /// turns to a nearer dungeon first, and recalls there when a rune it has not tried yet
    /// lands at that door. A long walk says why no recall carried it.
    /// </summary>
    private bool WalkFromHere()
    {
        if (!InDungeon(_character))
        {
            var walk = WalkToDoorTiles();

            if (DungeonEntryRules.WalkIsLong(walk) && TurnToNearer())
            {
                if (_doorRecalls == 0 && BeginDoorRecall())
                {
                    return true;
                }

                walk = WalkToDoorTiles();
            }

            LogWalk(walk);
        }

        return BeginWalkIn();
    }

    private bool Lone() => string.IsNullOrEmpty(_partyId) && !GameParty.InParty(_character);

    /// <summary>The walk to this run's door, the public moongates counted; zero when the door is unknown.</summary>
    private int WalkToDoorTiles()
    {
        var door = DungeonGround.DoorOf(_character.Map, DungeonGround.RegionNames(_character.Map)(_hall));

        return door == Point3D.Zero
            ? 0
            : NavMetric.ByMoongate(_character.Location, door, MoongateSeeds.LocationsFor(_character.HomeFacet));
    }

    /// <summary>A lone traveller takes a hall whose door is nearer on foot, and holds that hall's ground if it camps.</summary>
    private bool TurnToNearer()
    {
        if (!Lone() || DungeonGround.NearerOnFoot(_character, _hall, Utility.Random(int.MaxValue)) is not { } nearer)
        {
            return false;
        }

        var from = DungeonName();
        _hall = nearer.Arrival;
        _hunt = SkillFactory.HallHunt(_hall, _partyId);

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", DungeonEntryRules.NearerLine(_character.Name, from, DungeonName()));
        }

        LogHall(HeadsForVerb);
        return true;
    }

    /// <summary>Writes the floor of this run's hall and the power the crawler takes it on: "fits power N vs floor M".</summary>
    private void LogHall(string verb)
    {
        if (SosariaSettings.LogActivity && _character.Map != null &&
            DungeonAtlas.For(_character.Map.Name).FloorAt(_hall) is { } floor)
        {
            LogFloor(verb, floor, DungeonGround.FightingPower(_character));
        }
    }

    private void LogFloor(string verb, DungeonFloor floor, int power)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Line}",
                DungeonCrawlRules.FitLine(_character.Name, verb, floor.Dungeon, floor.Level, power, floor.Difficulty)
            );
        }
    }

    /// <summary>A walk a recall would have paid for says why no recall carried the person.</summary>
    private void LogWalk(int walk)
    {
        if (!SosariaSettings.LogActivity || walk < DungeonEntryRules.RecallMinTripTiles)
        {
            return;
        }

        var why = _doorRecalls > 0 ? DungeonEntryRules.FizzledWhy
            : !Lone() ? RecallRules.InPartyWhy
            : RecallRules.WhyNoRecall(_character, _hall, DungeonEntryRules.RecallMinTripTiles) ?? RecallRules.WordsFailedWhy;
        logger.Information("{Line}", DungeonEntryRules.WalkLine(_character.Name, DungeonName(), walk, why));
    }

    private string DungeonName() => DungeonGround.RegionNames(_character.Map)(_hall) ?? TalkWords.Place(_character);

    /// <summary>
    /// Inside this run's own dungeon. A pad on the way to Deceit carried walkers into Despise,
    /// and the walk from there to the Deceit hall had no room to reach.
    /// </summary>
    private bool InThisDungeon() =>
        InDungeon(_character) &&
        string.Equals(DungeonGround.RegionNames(_character.Map)(_character.Location), DungeonName(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The crawl begins, or the run turns for the surface with its reason written: 20 of 30
    /// crawls that reached no room stood inside with no word of why.
    /// </summary>
    private SkillStatus CrawlOrWalkOut() => StartCrawl() ? SkillStatus.Running : StartWalkHome();

    /// <summary>
    /// Starts the way in from where the person stands: inside already, it walks to the hall;
    /// outside, it walks to the real door pad when one by the catalog door lands inside.
    /// </summary>
    private bool BeginWalkIn()
    {
        var inside = InThisDungeon();
        return BeginLeg(DungeonEntryRules.FirstLeg(inside, !inside && FindDoorPad()));
    }

    private bool BeginLeg(DungeonLeg leg)
    {
        _phase = Phase.Enter;
        _leg = leg;

        // Back in after a pad carried it out, the person crawls from the landing: the walk to
        // the hall is the one that stepped on the pad.
        if (leg == DungeonLeg.WalkInside && _crawlFromLanding)
        {
            return StartCrawl();
        }

        if (leg == DungeonLeg.StepOnPad)
        {
            _walkIn = null;
            _padStarted = Core.Now;
            NextWaypoint();
            return true;
        }

        // Catalog heights name the floor: a hall's floor must not drop to the land under it.
        // Inside, the walk goes round the pads; on the surface it keeps clear of the reds' town.
        _walkIn = leg switch
        {
            DungeonLeg.WalkToDoor => new TravelSkill(
                _pad,
                DungeonEntryRules.PadApproachTiles,
                arrivalFloor: true,
                keepClear: _keepClear
            ),
            DungeonLeg.WalkInside => new TravelSkill(_hall, CharactersFile.DefaultGoToRange, arrivalFloor: true, avoidPads: true),
            _ => new TravelSkill(_hall, CharactersFile.DefaultGoToRange, arrivalFloor: true, avoidPads: true, keepClear: _keepClear)
        };
        return _walkIn.Begin(_character);
    }

    /// <summary>
    /// The real teleporters by the catalog door of the hall's dungeon that carry a person
    /// inside, and the nearest of them with where it lands. False when the catalog knows no
    /// door or no pad there lands inside.
    /// </summary>
    private bool FindDoorPad()
    {
        var map = _character.Map;
        var dungeonAt = DungeonGround.RegionNames(map);
        var dungeon = dungeonAt(_hall);
        _door = DungeonGround.DoorOf(map, dungeon);
        _doorPads.Clear();
        _doorPadLinks.Clear();

        if (_door == Point3D.Zero)
        {
            return false;
        }

        foreach (var item in map.GetItemsInRange<Teleporter>(_door, DungeonEntryRules.PadSearchTiles))
        {
            if (!GateTravel.IsUsablePad(item, map, _character))
            {
                continue;
            }

            _doorPads.Add(item);
            _doorPadLinks.Add((item.Location, string.Equals(dungeonAt(item.PointDest), dungeon, StringComparison.OrdinalIgnoreCase)));
        }

        return TakePad(DungeonEntryRules.PickPad(_door, _doorPadLinks));
    }

    /// <summary>Makes pad <paramref name="pick"/> of the door the one to step on. False for no pad.</summary>
    private bool TakePad(int pick)
    {
        if (pick == DungeonEntryRules.NoPad)
        {
            return false;
        }

        _padPick = pick;
        _padItem = _doorPads[pick];
        _pad = _padItem.Location;
        _landing = _padItem.PointDest;
        return true;
    }

    /// <summary>The pad would not take the person: the next pad of the door gets its own time. False when none is left.</summary>
    private bool NextDoorPad()
    {
        _doorPadLinks[_padPick] = (_doorPadLinks[_padPick].Pad, false);

        if (!TakePad(DungeonEntryRules.PickPad(_door, _doorPadLinks)))
        {
            return false;
        }

        _padStarted = Core.Now;
        return true;
    }

    private SkillStatus AdvanceGate()
    {
        if (_gateOpenedAt == default)
        {
            switch (TravelSpells.TakeOutcome(_character))
            {
                case TravelCastOutcome.Casting:
                    return SkillStatus.Running;
                case TravelCastOutcome.Succeeded:
                    _gateOpenedAt = Core.Now;
                    return SkillStatus.Running;
                default:
                    return RecallOrWalk() ? SkillStatus.Running : SkillStatus.Failed;
            }
        }

        if (Core.Now - _gateOpenedAt < GateHold)
        {
            return SkillStatus.Running;
        }

        // Through the gate the walk goes on from the door; a gate that would not take leaves the recall or the walk.
        return GateTravel.StepIntoSpellGate(_character, TravelSpells.GateBeside(_character), OwnGateEvent) switch
        {
            GateStep.Waiting => SkillStatus.Running,
            GateStep.Through => BeginWalkIn() ? SkillStatus.Running : SkillStatus.Failed,
            _ => RecallOrWalk() ? SkillStatus.Running : SkillStatus.Failed
        };
    }

    private SkillStatus AdvanceEnter()
    {
        var inside = InThisDungeon();
        ReachedInside |= inside;

        if (DungeonEntryRules.SkipsToInside(_leg, inside))
        {
            _walkIn?.Abort();
            return BeginLeg(DungeonLeg.WalkInside) ? SkillStatus.Running : SkillStatus.Failed;
        }

        var status = _leg == DungeonLeg.StepOnPad ? StepOnPad() : _walkIn.Tick();

        if (DungeonEntryRules.CrawlsWhereItStands(_leg, InThisDungeon(), status))
        {
            _walkIn = null;
            return CrawlOrWalkOut();
        }

        if (status != SkillStatus.Done)
        {
            return status;
        }

        if (DungeonEntryRules.After(_leg) is not { } next)
        {
            return CrawlOrWalkOut();
        }

        // At the door a mage with a blank rune marks it, so the next run here is a recall.
        // Nothing can be marked inside a Felucca dungeon.
        if (_leg == DungeonLeg.WalkToDoor && MarkRules.TryMarkHere(_character))
        {
            _phase = Phase.MarkDoor;
            return SkillStatus.Running;
        }

        return BeginLeg(next) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>
    /// One think of stepping onto the door pad. The engine's own pad carries the person in.
    /// A group leader first waits at the door so the group goes in together; a fight at the
    /// door holds the step's clock. A person the walk left out of the step's reach walks up
    /// to the pad first, and one whose straight step will not carry it paths onto the pad.
    /// A pad that will not take the person gives way to the next pad of the door.
    /// </summary>
    private SkillStatus StepOnPad()
    {
        var now = Core.Now;

        if (HoldForCrew())
        {
            _padStarted = now;
            return SkillStatus.Running;
        }

        if (_character.Combatant is { Deleted: false, Alive: true })
        {
            _padStarted = now;
        }

        if (DungeonEntryRules.PadStepExpired(now, _padStarted))
        {
            LogPadStuck();
            return NextDoorPad() ? SkillStatus.Running : SkillStatus.Failed;
        }

        if (DungeonEntryRules.NeedsApproach(_character.Location, _pad) ||
            DungeonEntryRules.PathsOntoPad(now, _padStarted) && NavMetric.Chebyshev(_character.Location, _pad) > 0)
        {
            GateTravel.WalkOntoPad(_character, _pad);
            return SkillStatus.Running;
        }

        return GateTravel.StepThroughTeleporter(_character, _landing, EnterEvent) switch
        {
            GateStep.Through => SkillStatus.Done,
            GateStep.Waiting => SkillStatus.Running,
            _ => SkillStatus.Failed
        };
    }

    /// <summary>Writes what held the step onto the door pad back (<see cref="DungeonEntryRules.PadStuckLine"/>).</summary>
    private void LogPadStuck()
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        var map = _character.Map;
        var at = _character.Location;
        var next = GatePad.EntryStep(Standable.Walker(map), at, _pad);
        var engineAllows = next is { } step && Standable.TryStep(map, at.X, at.Y, at.Z, step.X, step.Y, out _);
        var people = 0;

        if (next is { } tile)
        {
            foreach (var mobile in map.GetMobilesAt(tile))
            {
                if (mobile != _character)
                {
                    people++;
                }
            }
        }

        var why = DungeonEntryRules.PadStuckWhy(
            _character.Frozen,
            _character.Paralyzed,
            _character.Spell?.IsCasting == true,
            _character.Motor.CanMoveNow,
            _character.Motor.HeldAtGuardLine,
            next,
            engineAllows,
            people
        );
        logger.Information("{Line}", DungeonEntryRules.PadStuckLine(_character.Name, at, _pad, why));
    }

    /// <summary>
    /// Starts the run inside from where the person stands. A person back in after a pad
    /// carried it out keeps the run's clock.
    /// </summary>
    private bool StartCrawl()
    {
        var profile = _character.PersonProfile;
        var camps = DungeonCrawlRules.ShouldCamp(profile.Traits, _seed) && !_crawlFromLanding;

        if (_crawlStarted == default)
        {
            Talk.Maybe(_character, TalkCategory.DungeonEnter, TalkOdds.DungeonEnterPercent, DungeonSlots());
            _crawlStarted = Core.Now;
            _runLength = DungeonCrawlRules.RunLength(profile.Traits, _seed, camps);
            _suppliesLowAtCrawl = SupplyCheck.IsLow(_character);
            _hunt.JoinRun(_suppliesLowAtCrawl);
        }

        var noted = DungeonGate.NoteWhere(_character);
        LfgBoard.NoteFloor(_character, noted);

        // A hall off every mapped floor has no rooms to crawl: it is held like a camp.
        if (noted is not { } floor)
        {
            _phase = Phase.Camp;
            return _hunt.Begin(_character);
        }

        _phase = Phase.Crawl;
        _floorId = floor.Id;
        Watch(floor);
        var power = DungeonGround.FightingPower(_character);

        if (!Stays(floor, power))
        {
            return LeaveFloor(floor, power, DungeonCrawlRules.TooHardWhy, lost: false) || StartWalkHome() == SkillStatus.Running;
        }

        if (!DungeonCrawlRules.CampsAtHall(camps, HallOn(floor)))
        {
            return BeginFirstRoom(HallOn(floor) ? _hall : _character.Location);
        }

        _phase = Phase.Camp;

        if (_hunt.Begin(_character))
        {
            return true;
        }

        // The hall is too hot to hold now: the camper fights the floor room by room instead.
        _phase = Phase.Crawl;
        _cleared.Add(_hall);
        return NextRoom();
    }

    private bool HallOn(DungeonFloor floor) =>
        DungeonAtlas.For(_character.Map.Name).FloorAt(_hall) is { } hallFloor && hallFloor.Id == floor.Id;

    /// <summary>
    /// The hall's hunt ends on its own clock or when the hall is bare. The run does not: a
    /// camper whose hall still had prey holds it again, and one whose hall went bare fights
    /// on across the floor room by room, until the run itself is over.
    /// </summary>
    private SkillStatus AdvanceCamp()
    {
        if (LeaveIfLosing() is { } left)
        {
            return left;
        }

        var held = _hunt.Tick();

        if (held == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _roomsReached += held == SkillStatus.Failed ? 0 : 1;

        if (DungeonCrawlRules.CampsOn(_hunt.EndReason) && LeaveReasonNow() == null && _hunt.Begin(_character))
        {
            return SkillStatus.Running;
        }

        _phase = Phase.Crawl;
        _cleared.Add(_hall);
        _bareRooms = DungeonCrawlRules.CountBareRooms(_bareRooms, _hunt.EndReason, _hunt.Kills);
        return NextRoom() ? SkillStatus.Running : StartWalkHome();
    }

    private SkillStatus AdvanceCrawl()
    {
        if (LeaveIfLosing() is { } left)
        {
            return left;
        }

        if (_descent != null)
        {
            var walk = _descent.Tick();

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _failedStop = walk == SkillStatus.Failed ? _descent : _failedStop;
            _descent = null;
            _roomFailures = walk == SkillStatus.Failed ? _roomFailures + 1 : 0;
            EndStop();
        }
        else if (!_betweenRooms)
        {
            var status = _roomHunt.Tick();

            if (status == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _failedStop = status == SkillStatus.Failed ? _roomHunt : _failedStop;
            _roomFailures = status == SkillStatus.Failed ? _roomFailures + 1 : 0;
            _roomsReached += status == SkillStatus.Failed ? 0 : 1;
            _bareRooms = DungeonCrawlRules.CountBareRooms(_bareRooms, _roomHunt.EndReason, _roomHunt.Kills);
            EndStop();
        }

        // Between rooms a group leader lets the slow ones catch up before it moves on.
        if (HoldForCrew())
        {
            return SkillStatus.Running;
        }

        _betweenRooms = false;
        return NextRoom() ? SkillStatus.Running : StartWalkHome();
    }

    /// <summary>A room or a stair is behind the crawler: it counts, and the group gathers before the next.</summary>
    private void EndStop()
    {
        _rooms++;
        _betweenRooms = true;
        NextWaypoint();
    }

    /// <summary>A group leader holds here for a member left behind, once per door or room and never for long.</summary>
    private bool HoldForCrew()
    {
        if (_holdDone)
        {
            return false;
        }

        var now = Core.Now;

        if (_holdStarted == default)
        {
            _holdStarted = now;
        }

        if (LfgRules.HoldForCrew(LfgBoard.FarthestCrew(_character), now - _holdStarted))
        {
            _character.Motor.ClearMoveIntent();
            return true;
        }

        _holdDone = true;
        return false;
    }

    private void NextWaypoint()
    {
        _holdStarted = default;
        _holdDone = false;
    }

    /// <summary>
    /// The next room on this floor, or the landing of a stair down or up. False when the run is
    /// over or the floor offers nothing more; the caller turns for home. A floor the crawler
    /// may not stay on is left up a stair that fits, or out.
    /// </summary>
    private bool NextRoom()
    {
        _leaveReason = LeaveReasonNow();

        if (_leaveReason != null)
        {
            return false;
        }

        var floor = DungeonGate.NoteWhere(_character);
        LfgBoard.NoteFloor(_character, floor);

        if (floor is not { } here)
        {
            _leaveReason = DungeonCrawlRules.OffFloorWhy;
            return false;
        }

        if (here.Id != _floorId)
        {
            _floorId = here.Id;
            _cleared.Clear();
            _bareRooms = 0;
        }

        Watch(here);
        var power = DungeonGround.FightingPower(_character);

        if (!Stays(here, power))
        {
            return LeaveFloor(here, power, DungeonCrawlRules.TooHardWhy, lost: false);
        }

        var bare = DungeonCrawlRules.FloorIsBare(_bareRooms);

        if (bare)
        {
            _sweptFloors.Add(here.Id);
        }

        var map = DungeonAtlas.For(_character.Map.Name);
        var seed = unchecked(_seed + _rooms);
        var downs = map.StairsDown(here.Id);
        var fitDowns = Taken(map, downs, power);
        var fitUps = Taken(map, map.StairsUp(here.Id), power);
        var ways = new FloorWays(fitDowns.Count > 0, downs.Count > 0 && fitDowns.Count == 0, fitUps.Count > 0);
        var rooms = RoomPoints(Reachable(map.Rooms(here.Id)));
        var stop = DungeonCrawlRules.NextStop(
            _character.Location,
            rooms,
            _cleared,
            PreyNear(rooms),
            ways,
            bare,
            _character.PersonProfile.Tier,
            seed
        );

        // A crawler kept from the stair down by the floor below, not by its own sweep of it, says so.
        if ((stop is DungeonCrawlRules.NoStop or DungeonCrawlRules.Ascend) && ways.DownBarred &&
            map.Floor(downs[0].ToFloor) is { } deeper && !DungeonGround.Takes(_character, deeper, power, Core.Now))
        {
            LogKeepOff(deeper, power, DungeonCrawlRules.TooHardWhy);
        }

        switch (stop)
        {
            case DungeonCrawlRules.NoStop:
                _leaveReason = DungeonCrawlRules.FloorEmptyWhy;
                return false;
            case DungeonCrawlRules.Ascend:
                return TakeStair(map, fitUps, seed, power, GoesUpVerb);
            case DungeonCrawlRules.Descend:
                GameParty.Chat(_character, Talk.Line(TalkCategory.PartyDescend));
                return TakeStair(map, fitDowns, seed, power, GoesDownVerb);
            default:
                return BeginRoom(rooms[stop]);
        }
    }

    /// <summary>The crawler may stay on the floor: within its reach, and not one it lost on lately.</summary>
    private bool Stays(DungeonFloor floor, int power) =>
        DungeonCrawlRules.StaysOn(floor.Difficulty, power) && !DungeonGround.TooHard(_character, floor, Core.Now);

    /// <summary>
    /// The stairs that land on a floor this crawler takes (<see cref="DungeonGround.Takes"/>) and
    /// did not sweep bare on this run: a crawler sent up from a bare floor whose stairs down lead
    /// too deep does not come back down to it.
    /// </summary>
    private List<DungeonStair> Taken(DungeonMap map, IReadOnlyList<DungeonStair> stairs, int power)
    {
        var taken = new List<DungeonStair>(stairs.Count);
        var now = Core.Now;

        for (var i = 0; i < stairs.Count; i++)
        {
            if (!_sweptFloors.Contains(stairs[i].ToFloor) && Walks(stairs[i].Pad.Location) &&
                map.Floor(stairs[i].ToFloor) is { } floor && DungeonGround.Takes(_character, floor, power, now))
            {
                taken.Add(stairs[i]);
            }
        }

        return taken;
    }

    /// <summary>Takes one of <paramref name="stairs"/>, rolled by the seed, and writes the floor it leads to against the power.</summary>
    private bool TakeStair(DungeonMap map, List<DungeonStair> stairs, int seed, int power, string verb)
    {
        var stair = stairs[(int)(ChoiceSeed.Unit(seed, DungeonCrawlRules.RoomSalt) * stairs.Count)];

        if (map.Floor(stair.ToFloor) is { } to)
        {
            LogFloor(verb, to, power);
        }

        return BeginStair(stair.Landing.Location);
    }

    /// <summary>
    /// Leaves a floor the crawler may not stay on, or lost on (<paramref name="lost"/>: it stays
    /// too hard for it a while): up a stair to a floor that fits while the run and the wounds
    /// allow, else out. False when it heads out; the caller turns for home.
    /// </summary>
    private bool LeaveFloor(DungeonFloor here, int power, string why, bool lost)
    {
        var now = Core.Now;

        if (lost)
        {
            DungeonGround.MarkTooHard(_character, here, now);
        }

        LogKeepOff(here, power, why);
        var map = DungeonAtlas.For(_character.Map.Name);
        var ups = Taken(map, map.StairsUp(here.Id), power);
        _leaveReason = LeaveReasonNow();

        if (DungeonCrawlRules.GoesUp(ups.Count > 0, _leaveReason, Vitals.HitsFraction(_character)))
        {
            return TakeStair(map, ups, unchecked(_seed + _rooms), power, GoesUpVerb);
        }

        _leaveReason ??= why;
        return false;
    }

    private void LogKeepOff(DungeonFloor floor, int power, string why)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Line}",
                DungeonCrawlRules.KeepOffLine(_character.Name, floor.Dungeon, floor.Level, power, floor.Difficulty, why)
            );
        }
    }

    /// <summary>A new floor under the crawler: what it has lost so far is counted from here.</summary>
    private void Watch(DungeonFloor floor)
    {
        if (_watched?.Id == floor.Id)
        {
            return;
        }

        _watched = floor;
        _runsAtFloor = _character.Memory.Danger.RecentRuns(Core.Now);
        _petsAtFloor = PetKeeper.LivePets(_character);
        _matesDownAtFloor = MatesDown();
        _hitsAtFloor = Vitals.HitsFraction(_character);
    }

    /// <summary>
    /// Out of a fight, a crawler losing on its floor (<see cref="DungeonCrawlRules.LosingWhy"/>)
    /// stops what it does there, calls the floor too hard, and goes up or out. Null while it holds.
    /// </summary>
    private SkillStatus? LeaveIfLosing()
    {
        if (_watched is not { } floor || _character.Combatant is { Deleted: false, Alive: true })
        {
            return null;
        }

        var why = DungeonCrawlRules.LosingWhy(
            Math.Max(0, _character.Memory.Danger.RecentRuns(Core.Now) - _runsAtFloor),
            Math.Max(0, _petsAtFloor - PetKeeper.LivePets(_character)),
            Math.Max(0, MatesDown() - _matesDownAtFloor),
            _hitsAtFloor,
            Vitals.HitsFraction(_character)
        );

        if (why == null)
        {
            return null;
        }

        _watched = null;
        _hunt.Abort();
        _roomHunt?.Abort();
        _descent?.Abort();
        _roomHunt = null;
        _descent = null;
        _betweenRooms = false;
        _phase = Phase.Crawl;

        return LeaveFloor(floor, DungeonGround.FightingPower(_character), why, lost: true)
            ? SkillStatus.Running
            : StartWalkHome();
    }

    /// <summary>The members of the crawler's party that lie dead now.</summary>
    private int MatesDown()
    {
        var party = GameParty.Of(_character);
        var down = 0;

        for (var i = 0; i < (party?.Members.Count ?? 0); i++)
        {
            var member = party.Members[i].Mobile;
            down += member != null && member != _character && !member.Deleted && !member.Alive ? 1 : 0;
        }

        return down;
    }

    /// <summary>
    /// Takes a stair, down or up, to its landing, and rolls a room of the floor there from it.
    /// The landing is not fought round: it is often a pad back, and a hunter strolling round
    /// the Wrong level 2 landing was carried up again and lost its room.
    /// </summary>
    private bool BeginStair(Point3D landing)
    {
        _descent = new TravelSkill(landing, CharactersFile.DefaultGoToRange, arrivalFloor: true, avoidPads: true);

        if (_descent.Begin(_character))
        {
            return true;
        }

        var failed = _descent;
        _descent = null;
        return StopFailed(failed);
    }

    /// <summary>
    /// The first room is the spot the crawl starts from when prey stands in reach of it; else
    /// the spot counts as looked into and the crawl rolls a room of the floor. Both halls of
    /// Wrong are stair landings far from any juka.
    /// </summary>
    private bool BeginFirstRoom(Point3D start)
    {
        if (HasPreyNear(start))
        {
            return BeginRoom(start);
        }

        _cleared.Add(start);
        return NextRoom();
    }

    /// <summary>For each room, true when prey stands in reach of it (see <see cref="DungeonCrawlRules.RoomPreyRange"/>).</summary>
    private bool[] PreyNear(IReadOnlyList<Point3D> rooms)
    {
        var prey = new bool[rooms.Count];

        for (var i = 0; i < rooms.Count; i++)
        {
            prey[i] = HasPreyNear(rooms[i]);
        }

        return prey;
    }

    private bool HasPreyNear(Point3D at) =>
        HuntPrey.CountAt(_character, _character.Map, at, DungeonCrawlRules.RoomPreyRange) > 0;

    /// <summary>
    /// Why the run is over now, or null: its time, bad wounds, supplies spent since the crawl
    /// began, a full pack or a closed floor.
    /// </summary>
    private string LeaveReasonNow()
    {
        var packFull = CarryLoad.IsLoaded(_character, SosariaCombat.HuntPackFillFraction);

        return DungeonCrawlRules.LeaveReason(
            Core.Now - _crawlStarted,
            _runLength,
            Vitals.HitsFraction(_character),
            HuntEndDecision.RanLowOnRun(_suppliesLowAtCrawl, SupplyCheck.IsLow(_character)),
            packFull,
            _roomFailures
        );
    }

    /// <summary>
    /// Fights round one room, walked to round the floor's pads. A room that cannot be reached
    /// counts as a failure and the next is picked.
    /// </summary>
    private bool BeginRoom(Point3D target)
    {
        _cleared.Add(target);
        _roomHunt = new HuntSkill(
            DungeonCrawlRules.RoomArea(target),
            TimeSpan.FromMinutes(DungeonCrawlRules.RoomMinutes),
            DungeonCrawlRules.RoomEmptyLimit,
            SosariaCombat.DefaultStopBelowHitsFraction,
            new TravelSkill(target, CharactersFile.DefaultGoToRange, arrivalFloor: true, avoidPads: true),
            _partyId
        );
        _roomHunt.JoinRun(_suppliesLowAtCrawl);

        return _roomHunt.Begin(_character) || StopFailed(_roomHunt);
    }

    /// <summary>
    /// A room or a stair that cannot be reached counts as a failure, and the next stop is picked.
    /// The stop is kept, so a crawl that reached no room names the last stop's reason.
    /// </summary>
    private bool StopFailed(Skill stop)
    {
        _failedStop = stop;
        _roomFailures++;
        _rooms++;
        return NextRoom();
    }

    /// <summary>
    /// The rooms this crawler walks to from where it stands, past the spots its walks just
    /// found no way to. Rooms were rolled by weight alone: 30 crawls in 90 minutes reached none.
    /// </summary>
    private List<NavNode> Reachable(IReadOnlyList<NavNode> rooms)
    {
        var reachable = new List<NavNode>(rooms.Count);

        for (var i = 0; i < rooms.Count; i++)
        {
            if (Walks(rooms[i].Location))
            {
                reachable.Add(rooms[i]);
            }
        }

        return reachable;
    }

    /// <summary>A walk from here to the spot on this crawler's roads, not to a spot it just could not reach.</summary>
    private bool Walks(Point3D spot) =>
        !NavSearch.IsNearAny(spot, _character.Memory.Unreachable.Active(Core.Now)) &&
        RedGangReach.Walks(_character, _character.Location, spot);

    private static Point3D[] RoomPoints(IReadOnlyList<NavNode> rooms)
    {
        var points = new Point3D[rooms.Count];

        for (var i = 0; i < rooms.Count; i++)
        {
            points[i] = rooms[i].Location;
        }

        return points;
    }

    private SkillStatus StartWalkHome()
    {
        if (ReachedInside)
        {
            Talk.Maybe(_character, TalkCategory.DungeonLeave, TalkOdds.DungeonLeavePercent, DungeonSlots());
            LogLeave();
        }

        _crawlFailed = DungeonCrawlRules.CrawlFailed(_roomsReached, _leaveReason);
        _character.RestoreTownStance();
        _phase = Phase.WalkHome;
        _walkHomeFromInside = InDungeon(_character);
        return Finish(BeginWalkHome());
    }

    /// <summary>The run turns for the surface: the line names the dungeon, the minutes inside and why.</summary>
    private void LogLeave()
    {
        if (!SosariaSettings.LogActivity)
        {
            return;
        }

        var dungeon = DungeonGround.RegionNames(_character.Map)(_character.Location) ?? DungeonName();
        var why = _leaveReason == DungeonCrawlRules.RoomsFailedWhy
            ? TravelSkill.WithWalkWhy(_leaveReason, _failedStop)
            : _leaveReason ?? DungeonCrawlRules.FloorEmptyWhy;
        logger.Information("{Line}", DungeonCrawlRules.LeaveLine(_character.Name, dungeon, Core.Now - _crawlStarted, why));
    }

    /// <summary>The walk home; from inside it climbs the stairs round the pads it does not take.</summary>
    private SkillStatus BeginWalkHome()
    {
        _walkHome = new TravelSkill(_character.HomeSpot, CharactersFile.DefaultGoToRange, avoidPads: _walkHomeFromInside);
        return _walkHome.Begin(_character) ? SkillStatus.Running : SkillStatus.Failed;
    }

    /// <summary>
    /// The way home from inside climbs the real stairs; no rune works underground. Out
    /// through the door, the trip starts over from the surface, where a home rune recalls it.
    /// </summary>
    private SkillStatus TickWalkHome()
    {
        if (!_walkHomeFromInside || InDungeon(_character))
        {
            return _walkHome.Tick();
        }

        _walkHomeFromInside = false;
        _walkHome.Abort();
        return BeginWalkHome();
    }

    private SkillStatus Finish(SkillStatus homeWalk)
    {
        if (homeWalk != SkillStatus.Running && !string.IsNullOrEmpty(_partyId) &&
            Party.IsLeader(_partyId, _character.CharacterId))
        {
            Party.Disband(_partyId);
        }

        return HomeOutcome(homeWalk, _crawlFailed);
    }

    /// <summary>
    /// The hunt is over and the character is back outside. A home walk that fails is not
    /// a dungeon failure; the goal loop picks GoHome next. A crawl that reached no room
    /// failed the trip, however the walk home went.
    /// </summary>
    public static SkillStatus HomeOutcome(SkillStatus homeWalk, bool crawlFailed) =>
        homeWalk == SkillStatus.Running ? SkillStatus.Running
        : crawlFailed ? SkillStatus.Failed
        : SkillStatus.Done;

    /// <summary>
    /// A pad inside carried the person out while it walked in, held the hall or crawled:
    /// it goes back in by the door (<see cref="DungeonEntryRules.Reenters"/>).
    /// </summary>
    private bool CarriedOut() =>
        (_phase is Phase.Camp or Phase.Crawl || _phase == Phase.Enter && _leg == DungeonLeg.WalkInside) &&
        DungeonEntryRules.Reenters(ReachedInside, InDungeon(_character), _reentries);

    /// <summary>
    /// Goes back in by the door after a pad carried the person out, and this time crawls from
    /// where the door lands it. The run's clock and its rooms go on.
    /// </summary>
    private SkillStatus Reenter()
    {
        _reentries++;
        _crawlFromLanding = true;
        _walkIn?.Abort();
        _hunt.Abort();
        _roomHunt?.Abort();
        _descent?.Abort();
        _walkIn = null;
        _roomHunt = null;
        _descent = null;
        _betweenRooms = false;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Line}", DungeonEntryRules.ReenterLine(_character.Name, DungeonName()));
        }

        return BeginWalkIn() ? SkillStatus.Running : SkillStatus.Failed;
    }

    private TalkSlots DungeonSlots() => new() { Dungeon = TalkWords.Place(_character) };

    /// <summary>True while the person stands in a dungeon region.</summary>
    public static bool InDungeon(Mobile character) =>
        People.InWorld(character) &&
        character.Region?.IsPartOf<DungeonRegion>() == true;
}
