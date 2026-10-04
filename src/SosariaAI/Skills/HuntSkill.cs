using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;
using SosariaAI.Economy;
using SosariaAI.Social;

namespace SosariaAI.Skills;

public sealed class HuntSkill : Skill, IHuntingSkill
{
    /// <summary>The blow a threat read reckons a person with (<see cref="HostileRead"/>); a creature counts its own.</summary>
    public const int HuntThreatAverageDamage = 8;

    // A fight is rated on everything that can join it, not just the one target in view.
    // Standing next to one troll with three more behind it is the whole problem.
    public const int ThreatScanRange = SosariaCombat.HuntRangePerception;
    public const int MaxRatedHostiles = 8;

    /// <summary>A hunter walks this close to prey it spotted, well inside its sight, and lets the fight start.</summary>
    public const int SeekRange = SosariaCombat.HuntRangePerception / 2;

    /// <summary>A hunter goes after prey this far past the edge of its ground, as far as it can see.</summary>
    public const int ReachSlack = SosariaCombat.HuntRangePerception;

    /// <summary>
    /// Prey the hunter stands near this long without a fight is passed over: a few danger
    /// scans, so the combat brain had its look.
    /// </summary>
    public static readonly TimeSpan PassOverAfter = TimeSpan.FromSeconds(3);

    public const string DownWhy = "died or left the world";
    public const string GroundOutOfReachWhy = "the hunt ground is out of reach";
    public const string NoWalkToGroundWhy = "no walk to the hunt ground";
    public const string WalkFailedWhy = "the walk to the hunt ground failed";
    public const string OffGroundWhy = "arrived off the hunt ground";
    public const string TooDangerousWhy = "the hunt ground is too dangerous";
    public const string NoWalkBackWhy = "no walk back to the hunt ground";

    private static readonly ILogger logger = SosariaLog.For(typeof(HuntSkill));

    private readonly Rectangle2D _area;
    private readonly Rectangle2D _reach;
    private readonly CorpseLoot _loot = new();
    private readonly HashSet<Serial> _passedPrey = [];
    private readonly TimeSpan _duration;
    private readonly TimeSpan _emptyLimit;
    private readonly double _stopBelowHitsFraction;
    private readonly string _partyId;
    private Skill _walk;
    private Skill _waitWalk;
    private TravelSkill _toArea;
    private TravelSkill _healerWalk;
    private RestSkill _rest;
    private SosariaCharacter _character;
    private string _approachWhy;
    private DateTime _endsAt;
    private bool _waitingForParty;
    private bool _hunting;
    private bool _wasBelow;
    private bool _recovering;
    private bool _recoveryLogged;
    private DateTime _playerFarSince;
    private HomeRange? _homeBefore;
    private int _lowHitsCount;
    private Mobile _lastCombatant;
    private Point3D _lastCombatantAt;
    private Mobile _prey;
    private Mobile _reachedPrey;
    private DateTime _reachedPreyAt;
    private long _nextPreyScanAt;
    private DateTime _lastPreyAt;
    private DateTime _lastKillAt;
    private int _stays;
    private bool _startedLogged;
    private HuntEndReason _endReason;
    private bool _suppliesLowAtStart;
    private bool? _runSuppliesLowAtStart;

    public HuntSkill(
        Rectangle2D area,
        TimeSpan duration,
        double stopBelowHitsFraction,
        Skill approach,
        string partyId
    ) : this(area, duration, HuntEndDecision.EmptyHuntLimit, stopBelowHitsFraction, approach, partyId)
    {
    }

    /// <summary>A hunt that leaves its ground after <paramref name="emptyLimit"/> with no prey in sight.</summary>
    public HuntSkill(
        Rectangle2D area,
        TimeSpan duration,
        TimeSpan emptyLimit,
        double stopBelowHitsFraction,
        Skill approach,
        string partyId
    )
    {
        _area = area;
        _reach = new Rectangle2D(
            area.X - ReachSlack,
            area.Y - ReachSlack,
            area.Width + ReachSlack * 2,
            area.Height + ReachSlack * 2
        );
        _duration = duration;
        _emptyLimit = emptyLimit;
        _stopBelowHitsFraction = stopBelowHitsFraction;
        _walk = approach;
        _partyId = partyId;
    }

    public override string Name => SkillKinds.Hunt;

    public int Kills { get; private set; }


    public bool IsHunting => _hunting;

    /// <summary>Why the last hunt ended; none while it runs.</summary>
    public HuntEndReason EndReason => _endReason;

    /// <summary>
    /// Ties this hunt to the run it is part of: a dungeon crawl hands the hall's hunt and each
    /// room's hunt the low state its crawl began with. A room entered low inside a crawl that
    /// began low fights on as the crawl does; a hunt on its own reads its state at its start.
    /// </summary>
    public void JoinRun(bool suppliesLowAtRunStart) => _runSuppliesLowAtStart = suppliesLowAtRunStart;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _suppliesLowAtStart = _runSuppliesLowAtStart ?? SupplyCheck.IsLow(character);
        _waitingForParty = false;
        _hunting = false;
        _wasBelow = false;
        _recovering = false;
        _recoveryLogged = false;
        _playerFarSince = default;
        _homeBefore = HomeRange.Capture(character);
        _lowHitsCount = 0;
        Kills = 0;
        _loot.Clear();
        _lastCombatant = null;
        _prey = null;
        _reachedPrey = null;
        _reachedPreyAt = default;
        _passedPrey.Clear();
        _nextPreyScanAt = 0;
        _lastPreyAt = default;
        _lastKillAt = default;
        _stays = 0;
        _endReason = HuntEndReason.None;
        _startedLogged = false;
        _waitWalk?.Abort();
        _waitWalk = null;
        _rest?.Abort();
        _rest = null;
        character.SaidCombatLineThisHunt = false;

        if (!string.IsNullOrEmpty(_partyId) &&
            Party.IsLeader(_partyId, character.CharacterId) &&
            Party.Find(_partyId) is { TripActive: false })
        {
            Party.StartWait(character);
            _waitingForParty = true;
            BeginWaitWalk();
            return true;
        }

        return BeginApproach() || CannotStart(_approachWhy);
    }

    public override SkillStatus Tick()
    {
        if (_character.Deleted || !People.InWorld(_character) || _character.IsGhost)
        {
            Restore();
            return Fail(DownWhy);
        }

        Party.TickCare(_character);

        if (_waitingForParty)
        {
            if (_waitWalk != null)
            {
                var waitWalk = _waitWalk.Tick();

                if (waitWalk != SkillStatus.Running)
                {
                    _waitWalk = null;
                }
            }

            var wait = Party.TickWait(_character);

            if (wait == PartyWaitResult.Waiting)
            {
                return SkillStatus.Running;
            }

            _waitingForParty = false;
            ClearWaitWalk();

            if (!BeginApproach())
            {
                return FailHunt(_approachWhy);
            }
        }

        if (PlayerOutOfRange())
        {
            return TickPlayerFar();
        }

        _playerFarSince = default;

        if (_toArea != null)
        {
            var toArea = _toArea.Tick();

            if (toArea == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _toArea = null;

            if (ArrivalWhy(toArea) is { } offArrival)
            {
                return FailHunt(offArrival);
            }

            StartHunt();
        }

        if (_walk != null)
        {
            var walkStatus = _walk.Tick();

            if (walkStatus == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _walk = null;

            if (ArrivalWhy(walkStatus) is { } offWalk)
            {
                return FailHunt(offWalk);
            }

            StartHunt();
        }

        CountKill();

        if (Party.ShouldWaitForLag(_character))
        {
            return SkillStatus.Running;
        }

        ScanPrey();
        var hitsFraction = Vitals.HitsFraction(_character);
        var status = EvaluateEnd(hitsFraction);

        if (status != SkillStatus.Running)
        {
            EndHunt(status);
            return status;
        }

        if (_character.Motor.Action is CharacterAction.Combat or CharacterAction.Flee)
        {
            return SkillStatus.Running;
        }

        // Hysteresis: drop out below the recovery line, come back only when fit. Without
        // this a character bounces in and out of resting at the same hit points.
        if (ShouldRest(_recovering, hitsFraction, _character.Combatant != null))
        {
            return TickRest();
        }

        ClearRestIfFit(hitsFraction);

        if (_loot.Tick(_character) || CombatBrain.TendMana(_character))
        {
            return SkillStatus.Running;
        }

        if (WalksBackToGround(InArea(), ChasingPreyInReach()))
        {
            return _character.Motor.MoveToPoint(_character.Home) ? SkillStatus.Running : FailHunt(NoWalkBackWhy);
        }

        Seek();
        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _toArea?.Abort();
        _toArea = null;
        _healerWalk?.Abort();
        _healerWalk = null;
        _rest?.Abort();
        _rest = null;
        Restore();
    }

    public override void Resume(TimeSpan held)
    {
        _endsAt = SkillClock.Shift(_endsAt, held);
        _playerFarSince = SkillClock.Shift(_playerFarSince, held);
        _lastPreyAt = SkillClock.Shift(_lastPreyAt, held);
        _lastKillAt = SkillClock.Shift(_lastKillAt, held);
        _reachedPreyAt = SkillClock.Shift(_reachedPreyAt, held);
        _walk?.Resume(held);
        _waitWalk?.Resume(held);
        _toArea?.Resume(held);
        _healerWalk?.Resume(held);
        _rest?.Resume(held);
    }

    /// <summary>
    /// A retreat, or a chase that lost its prey, can end outside the ground: the hunter walks
    /// back to its middle. A chase after prey still in reach goes on past the edge. Walking
    /// back at the edge of a thirteen-tile dungeon room turned every chase round, and the
    /// hunters of Wrong watched the jukas past the room's edge for the whole run.
    /// </summary>
    public static bool WalksBackToGround(bool onGround, bool chasingPreyInReach) => !onGround && !chasingPreyInReach;

    public static bool ShouldRest(bool recovering, double hitsFraction, bool inCombat) =>
        recovering
            ? !RecoveryRules.IsFit(hitsFraction)
            : RecoveryRules.NeedsRecovery(hitsFraction, inCombat);

    /// <summary>A party player who stays out of range for the follow limit ends the hunt.</summary>
    public static bool PlayerFarTooLong(DateTime now, DateTime farSince) =>
        TimeRules.Passed(farSince, now, PartyWaitRules.FollowLimit);

    public static bool TryAcquire(SosariaCharacter character, Func<bool> acquireFocus)
    {
        if (character == null || acquireFocus == null)
        {
            return false;
        }

        var hitsFraction = Vitals.HitsFraction(character);

        if (RecoveryRules.NeedsRecovery(hitsFraction, character.Combatant != null))
        {
            return false;
        }

        if (character.Routine?.CurrentSkill is HuntSkill hunt && !hunt.InReach(character))
        {
            return false;
        }

        if (!acquireFocus())
        {
            return false;
        }

        if (character.Routine?.CurrentSkill is HuntSkill areaHunt &&
            character.FocusMob != null &&
            !areaHunt.InReach(character.FocusMob))
        {
            return false;
        }

        // AcquireFocus already weighed the room with the character's nerve.
        return true;
    }

    private SkillStatus TickRest()
    {
        _recovering = true;
        var pack = _character.Backpack;
        var bandages = pack?.GetAmount(typeof(Bandage)) ?? 0;
        var potions = pack?.GetAmount(typeof(GreaterHealPotion)) ?? 0;

        if (RecoveryRules.ShouldSeekHealer(Vitals.HitsFraction(_character), bandages, potions))
        {
            if (_healerWalk == null)
            {
                _healerWalk = new TravelSkill("healer", CharactersFile.DefaultGoToRange);

                if (!_healerWalk.Begin(_character))
                {
                    _healerWalk = new TravelSkill(BankTeller.BankToken, CharactersFile.DefaultGoToRange);
                    _healerWalk.Begin(_character);
                }

                LogRecovery();
            }

            var walkStatus = _healerWalk.Tick();

            if (walkStatus == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            _healerWalk = null;
        }

        if (_rest == null)
        {
            _rest = new RestSkill(TimeSpan.FromMinutes(SkillFactory.DefaultRestMinutes));
            _rest.Begin(_character);
            LogRecovery();
        }

        var restStatus = _rest.Tick();

        if (restStatus == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _rest = null;
        ClearRestIfFit(Vitals.HitsFraction(_character));
        return SkillStatus.Running;

    }

    private void ClearRestIfFit(double hitsFraction)
    {
        if (_rest != null)
        {
            _rest.Abort();
            _rest = null;
        }

        if (_recovering && RecoveryRules.IsFit(hitsFraction))
        {
            _recovering = false;
            LogFit();
        }
    }

    private SkillStatus TickPlayerFar()
    {
        if (_hunting)
        {
            var status = EvaluateEnd(Vitals.HitsFraction(_character));

            if (status != SkillStatus.Running)
            {
                EndHunt(status);
                return status;
            }
        }

        if (_playerFarSince == default)
        {
            _playerFarSince = Core.Now;
        }

        if (!PlayerFarTooLong(Core.Now, _playerFarSince))
        {
            return SkillStatus.Running;
        }

        EndHunt(SkillStatus.Done);
        return SkillStatus.Done;
    }

    private SkillStatus EvaluateEnd(double hitsFraction)
    {
        // The body's own carry limit, not the pack's: an overloaded player cannot walk.
        var packFull = CarryLoad.IsLoaded(_character, SosariaCombat.HuntPackFillFraction);
        var isBelow = hitsFraction < _stopBelowHitsFraction;
        _lowHitsCount = HuntEndDecision.CountLowHits(_lowHitsCount, _wasBelow, isBelow);
        _wasBelow = isBelow;

        var reason = HuntEndDecision.Reason(
            Core.Now,
            _endsAt,
            packFull,
            hitsFraction,
            _stopBelowHitsFraction,
            _lowHitsCount,
            _lastPreyAt,
            _emptyLimit,
            HuntEndDecision.RanLowOnRun(_suppliesLowAtStart, SupplyCheck.IsLow(_character))
        );

        if (HuntEndDecision.ShouldStay(reason, Core.Now, _lastKillAt, _stays))
        {
            _stays++;
            _endsAt = Core.Now + HuntEndDecision.StayLonger;
            LogStay();
            return SkillStatus.Running;
        }

        _endReason = reason;
        return reason == HuntEndReason.None ? SkillStatus.Running : SkillStatus.Done;
    }

    public bool InArea() => InArea(_character);

    public bool InArea(Mobile mobile) =>
        mobile != null && _area.Width > 0 && _area.Height > 0 && _area.Contains(mobile.Location);

    /// <summary>The ground and as far past its edge as a hunter standing on it can see.</summary>
    public bool InReach(Mobile mobile) =>
        mobile != null && _area.Width > 0 && _area.Height > 0 && _reach.Contains(mobile.Location);

    public static int SightThreat(SosariaCharacter character) =>
        ThreatAt(character, character?.Location ?? Point3D.Zero);

    public static int ThreatAt(SosariaCharacter character, Point3D origin)
    {
        var found = new List<HostileStats>();

        foreach (var (_, stats) in RatedHostiles(character, origin))
        {
            found.Add(stats);
        }

        return ThreatRating.Score(found);
    }

    /// <summary>The hostile in sight that rates highest on its own, or null when none.</summary>
    public static Mobile StrongestHostile(SosariaCharacter character)
    {
        Mobile strongest = null;
        var best = 0;

        foreach (var (mobile, stats) in RatedHostiles(character, character?.Location ?? Point3D.Zero))
        {
            var score = ThreatRating.Score([stats]);

            if (strongest == null || score > best)
            {
                strongest = mobile;
                best = score;
            }
        }

        return strongest;
    }

    private static List<(Mobile Mobile, HostileStats Stats)> RatedHostiles(
        SosariaCharacter character,
        Point3D origin
    )
    {
        var found = new List<(Mobile, HostileStats)>();

        if (!People.InWorld(character) || SosariaCharacter.UnderGuards(character))
        {
            return found;
        }

        foreach (var mobile in character.Map.GetMobilesInRange(origin, ThreatScanRange))
        {
            if (found.Count >= MaxRatedHostiles)
            {
                break;
            }

            if (mobile == character || mobile.Deleted || !mobile.Alive ||
                !character.CanSee(mobile) || !character.IsEnemy(mobile))
            {
                continue;
            }

            var stats = HostileRead.Of(mobile);

            if (HarmlessCreatures.IsHarmless(mobile.GetType().Name, ThreatRating.Score([stats])))
            {
                continue;
            }

            found.Add((mobile, stats));
        }

        return found;
    }

    private bool BeginApproach()
    {
        _approachWhy = null;

        if (_walk != null)
        {
            // An authored hunt ground beyond the leash is not walked: a Magincia hunter once
            // set out for the Britain graveyard.
            if (_walk is TravelSkill trip && !MaySetOutFor(trip.Goal))
            {
                return RefuseApproach(GroundOutOfReachWhy);
            }

            return _walk.Begin(_character) || RefuseApproach(NoWalkToGroundWhy);
        }

        if (!InArea())
        {
            var center = new Point3D(
                _area.X + Math.Max(1, _area.Width) / 2,
                _area.Y + Math.Max(1, _area.Height) / 2,
                _character.Z
            );

            if (!MaySetOutFor(center))
            {
                return RefuseApproach(GroundOutOfReachWhy);
            }

            _toArea = new TravelSkill(center, CharactersFile.DefaultGoToRange);
            return _toArea.Begin(_character) || RefuseApproach(NoWalkToGroundWhy);
        }

        if (EntryTooDangerous())
        {
            return RefuseApproach(TooDangerousWhy);
        }

        StartHunt();
        return true;
    }

    private bool RefuseApproach(string why)
    {
        _approachWhy = why;
        return false;
    }

    /// <summary>
    /// A hunter sets out for ground within its leash. A red's ground is judged by its reach
    /// instead (see <see cref="RedGangReach"/>): Buccaneer's Den is an island with no ground in
    /// the leash, so its reds hunt by the moongates or a rune, and never where the guards stand.
    /// </summary>
    private bool MaySetOutFor(Point3D ground) =>
        _character.Disposition == DispositionKind.Outlaw
            ? RedGangReach.CanReach(_character, ground)
            : !HomeLeash.BeyondLeash(ground, _character.Location, HomeLeash.ConfiguredRadius());

    private bool EntryTooDangerous()
    {
        var threat = ThreatAt(_character, _character.Location);

        if (threat < ThreatRating.MinThreatToFlee)
        {
            return false;
        }

        var hits = Vitals.HitsFraction(_character);
        var multiple = SosariaSettings.Characters?.Career?.ThreatMultiple ?? ThreatRating.DefaultThreatMultiple;
        return !ThreatRating.ShouldEngage(
            CharacterPower.For(_character),
            threat,
            hits,
            hasHealing: true,
            Party.AlliesPower(_character),
            multiple,
            ThreatRating.OpenFightHitsFraction,
            alreadyAttacked: false
        );
    }

    private bool PlayerOutOfRange()
    {
        var party = GameParty.Of(_character);

        if (!GameParty.HasPlayer(party))
        {
            return false;
        }

        var range = SosariaSettings.Characters?.Career?.PartyFollowRange ?? CareerSettings.DefaultPartyFollowRange;

        for (var i = 0; i < party.Members.Count; i++)
        {
            var player = party.Members[i].Mobile;

            if (!People.IsHuman(player) || player.Deleted)
            {
                continue;
            }

            if (player.Map != _character.Map ||
                NavMetric.Chebyshev(_character.Location, player.Location) > range)
            {
                return true;
            }
        }

        return false;
    }

    private void BeginWaitWalk()
    {
        var meetAt = Party.Find(_partyId)?.MeetAt ?? Point3D.Zero;

        if (meetAt == Point3D.Zero)
        {
            return;
        }

        _waitWalk = new TravelSkill(meetAt, CharactersFile.DefaultGoToRange);

        if (!_waitWalk.Begin(_character))
        {
            _waitWalk = null;
        }
    }

    private void ClearWaitWalk()
    {
        _waitWalk?.Abort();
        _waitWalk = null;
    }

    private SkillStatus FailHunt(string why)
    {
        EndHunt(SkillStatus.Failed);
        return Fail(why);
    }

    /// <summary>Why a walk to the ground ends the hunt, or null when the hunter stands on safe enough ground.</summary>
    private string ArrivalWhy(SkillStatus walk) =>
        walk == SkillStatus.Failed ? WalkFailedWhy
        : !InArea() ? OffGroundWhy
        : EntryTooDangerous() ? TooDangerousWhy
        : null;

    private void StartHunt()
    {
        _hunting = true;
        _endsAt = Core.Now + _duration;
        _lastPreyAt = Core.Now;
        _character.LastHuntAt = Core.Now;
        _character.Home = new Point3D(
            _area.X + _area.Width / 2,
            _area.Y + _area.Height / 2,
            _character.Z
        );
        _character.RangeHome = Math.Max(1, Math.Min(_area.Width, _area.Height) / 2);
        _character.EnterHuntStance();

        if (!_startedLogged && SosariaSettings.LogActivity)
        {
            _startedLogged = true;
            logger.Information(
                "{Name} started a hunt at {Location}{Fit}",
                _character.Name,
                _character.Location,
                DungeonGround.FitNoteAt(_character, _character.Home)
            );
            var power = CharacterPower.For(_character);

            if (_character.NotePowerGrowth(power))
            {
                logger.Information("{Name} power is now {Power}", _character.Name, power);
            }
        }
    }

    private void EndHunt(SkillStatus status)
    {
        if (_hunting)
        {
            ReportHunt(status);
        }

        if (status == SkillStatus.Failed &&
            !string.IsNullOrEmpty(_partyId) &&
            Party.IsLeader(_partyId, _character.CharacterId))
        {
            Party.Disband(_partyId);
        }

        Restore();
    }

    private void Restore()
    {
        CombatBrain.EndMeditation(_character);
        _hunting = false;
        _waitingForParty = false;
        ClearWaitWalk();
        _walk?.Abort();
        _walk = null;
        _toArea?.Abort();
        _toArea = null;
        _healerWalk?.Abort();
        _healerWalk = null;
        _rest?.Abort();
        _rest = null;
        _character?.RestoreTownStance();
        _homeBefore?.Restore(_character);
        _homeBefore = null;
    }

    private void CountKill()
    {
        if (_lastCombatant != null && (_lastCombatant.Deleted || !_lastCombatant.Alive))
        {
            Kills++;
            _lastKillAt = Core.Now;
            _character.NoteKill(_lastCombatant.Name);
            _loot.Note(_lastCombatant, _lastCombatantAt);
            LogKill(_lastCombatant, _lastCombatantAt);
            _lastCombatant = null;
        }

        if (_character.Combatant is { Deleted: false, Alive: true } foe)
        {
            _lastCombatant = foe;
            _lastCombatantAt = foe.Location;
            _lastPreyAt = Core.Now;
        }
    }

    /// <summary>
    /// Looks over the whole ground for prey on the ambient pace, the way a player scans the
    /// screen. The time of the last sighting tells when the ground went quiet.
    /// </summary>
    private void ScanPrey()
    {
        if (!_hunting ||
            !ScanPace.Due(Core.TickCount, ref _nextPreyScanAt, ScanPace.AmbientMs, _character.Serial.Value))
        {
            return;
        }

        _prey = HuntPrey.Nearest(_character, AreaCenter(), ReachRadius(), _passedPrey);

        if (_prey != null)
        {
            _lastPreyAt = Core.Now;
        }
    }

    /// <summary>
    /// Walks toward the prey the last scan found; with none in reach, strolls the ground. Prey
    /// the hunter walked up to and still would not fight, or could not reach, is passed over
    /// for the rest of the hunt.
    /// </summary>
    private void Seek()
    {
        if (_prey is not { Deleted: false, Alive: true } prey || !InReach(prey))
        {
            _prey = null;
            _character.Motor.LoiterInHome(IdleWanderSkill.WanderChanceToNotMove);
            return;
        }

        if (_character.InRange(prey, SeekRange))
        {
            if (_reachedPrey != prey)
            {
                _reachedPrey = prey;
                _reachedPreyAt = Core.Now;
            }
            else if (Core.Now - _reachedPreyAt >= PassOverAfter)
            {
                PassOver(prey);
            }

            return;
        }

        if (!_character.Motor.MoveTo(prey, SeekRange))
        {
            PassOver(prey);
        }
    }

    private bool ChasingPreyInReach() =>
        _prey is { Deleted: false, Alive: true } prey && InReach(prey) && InReach(_character);

    private void PassOver(Mobile prey)
    {
        _passedPrey.Add(prey.Serial);
        _prey = null;
        _reachedPrey = null;
    }

    private Point3D AreaCenter() =>
        new(_area.X + _area.Width / 2, _area.Y + _area.Height / 2, _character.Z);

    private int ReachRadius() => Math.Max(_reach.Width, _reach.Height) / 2;

    /// <summary>
    /// The hunt's tally: a log line, a line of talk about the take or the empty ground, and a
    /// dry mark on a ground that gave nothing so the next pick goes elsewhere.
    /// </summary>
    private void ReportHunt(SkillStatus status)
    {
        var center = AreaCenter();

        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} hunt at {Location}: {Kills} kills, {Gold} gold, {Items} items ({Status}, {Reason})",
                _character.Name,
                center,
                Kills,
                _loot.Gold,
                _loot.Items,
                status,
                _endReason
            );
        }

        if (Kills > 0)
        {
            Talk.Maybe(
                _character,
                TalkCategory.HuntHaul,
                TalkOdds.HuntEndPercent,
                new TalkSlots { Count = Kills, Price = _loot.Gold > 0 ? GoldWords.Spoken(_loot.Gold) : null }
            );
            return;
        }

        DryGrounds.Note(_character.Serial.Value, new Point2D(center.X, center.Y), Core.Now);
        Talk.Maybe(
            _character,
            TalkCategory.HuntDry,
            TalkOdds.HuntEndPercent,
            new TalkSlots { Place = TalkWords.Place(_character) }
        );
    }

    private void LogKill(Mobile foe, Point3D at)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} killed {Foe} at {Location}", _character.Name, foe.Name, at);
        }
    }

    private void LogStay()
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} stays on at {Location}: the hunt pays ({Kills} kills, {Gold} gold)",
                _character.Name,
                AreaCenter(),
                Kills,
                _loot.Gold
            );
        }
    }

    private void LogRecovery()
    {
        if (_recoveryLogged || !SosariaSettings.LogActivity)
        {
            return;
        }

        _recoveryLogged = true;
        logger.Information("{Name} broke off to recover at {Location}", _character.Name, _character.Location);
        GameParty.Chat(_character, PartyInviteRules.BackLine());
    }

    private void LogFit()
    {
        if (!_recoveryLogged)
        {
            return;
        }

        _recoveryLogged = false;

        if (SosariaSettings.LogActivity)
        {
            logger.Information("{Name} is fit again at {Location}", _character.Name, _character.Location);
        }
    }

    /// <summary>Bandages, heal potions, or the Magery to cast a heal: something to fight on with.</summary>
    public static bool HasHealing(SosariaCharacter character)
    {
        var pack = character.Backpack;
        return pack != null &&
               (pack.GetAmount(typeof(Bandage)) > 0 || pack.FindItemByType<BaseHealPotion>() != null ||
                SpellBook.IsCaster(character.Skills.Magery.Value));
    }
}
