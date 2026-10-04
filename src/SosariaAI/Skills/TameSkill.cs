using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Logging;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Social;

namespace SosariaAI.Skills;

/// <summary>
/// A taming trip the way a 1999 mage-tamer made one. With no beast worth a try in sight it
/// travels to the ground <see cref="TamingGrounds"/> picks for it, recalling when a rune lands
/// there, and looks the ground over. Its pets stay back ("all stay") while it walks up to the
/// strongest beast it may take and uses the engine's own Animal Taming on it: the cursor, the
/// three or four rounds of sweet talk, the engine's check. A beast that bites back is not
/// fought: the tamer takes its blows, heals itself with its magery between tries (the combat
/// brain's out-of-fight care), and gives up only when its hits run low. A beast strong for the
/// tamer's tier is kept and the trip ends; a practice beast is let go at once and the tamer
/// tries the next, until its practice session is full (<see cref="TameRules.SessionFull"/>):
/// then its taming rests (<see cref="TamePractice"/>) and the trip goes on for a keeper only. A
/// kept beast that outdoes the tamer's fighter sends the old one back to the wild
/// (<see cref="PetKeeper.LetGoSparePets"/>). A ground with nothing on it is marked dry and the
/// next ground is tried. A trip holds its area (<see cref="TamingGrounds.Claim"/>) and starts
/// on no beast in an area other tamers already fill.
/// </summary>
public sealed class TameSkill : Skill
{
    /// <summary>The engine's taming cursor reaches this far before Age of Shadows, and <see cref="AosTargetRange"/> after.</summary>
    public const int PreAosTargetRange = 2;

    public const int AosTargetRange = 3;

    /// <summary>The engine stops a try at six tiles; the tamer keeps well inside that.</summary>
    public const int StayCloseTiles = 4;

    /// <summary>Beasts this close are in sight of a tamer anywhere: the engine's pet-command reach.</summary>
    public const int SightTiles = PetRules.PetScanRange;

    /// <summary>A tamer at its ground looks as far as the spawner lets its creatures roam.</summary>
    public const int GroundSightTiles = HuntGround.AreaRadius;

    /// <summary>
    /// The pets are told to stay once the tamer is this close to its beast: out of the beast's
    /// way, and still inside the reach of the next "all follow me".
    /// </summary>
    public const int PetHoldTiles = 8;

    /// <summary>A walk to the ground ends this close to the spawn spot.</summary>
    public const int GroundArrivalRange = 3;

    /// <summary>A trip looks over at most this many grounds before it goes home.</summary>
    public const int MaxGroundsPerTrip = 2;

    /// <summary>Tries on one trip: a grandmaster on a dragon needs eight on average.</summary>
    public const int MaxAttempts = 40;

    /// <summary>Below this share of its hits the tamer stops taking the beast's blows and fights it.</summary>
    public const double GiveUpHitsFraction = 0.4;

    /// <summary>A whole trip, road and ground: past it the tamer goes home with what it has.</summary>
    public static readonly TimeSpan TripTime = TimeSpan.FromMinutes(30);

    /// <summary>The engine's rounds last three or four times three seconds; past this a try is over.</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(15);

    private const string NoBeastWhy = "found no beast it could tame";

    private static readonly ILogger logger = SosariaLog.For(typeof(TameSkill));

    private enum Phase
    {
        Travel,
        Seek,
        Approach,
        Attempt
    }

    private readonly HashSet<Serial> _passed = [];
    private SosariaCharacter _tamer;
    private Phase _phase;
    private TamingGround _ground;
    private Skill _walk;
    private BaseCreature _quarry;
    private DateTime _endsAt;
    private DateTime _attemptEnds;
    private bool _attempting;
    private int _attempts;
    private int _quarryTries;
    private int _grounds;
    private int _tamed;
    private int _practiced;

    public override string Name => SkillKinds.Tame;

    /// <summary>The beast the tamer is walking up to or taming now, or null.</summary>
    public BaseCreature Quarry => _phase is Phase.Approach or Phase.Attempt ? _quarry : null;

    /// <summary>True while the tamer works a beast close by: its pets stay back, out of the way.</summary>
    public bool HoldsPets => Quarry != null && _tamer?.InRange(_quarry, PetHoldTiles) == true;

    public static int TargetRange => Core.AOS ? AosTargetRange : PreAosTargetRange;

    /// <summary>True while a tamer out on a taming trip is well enough to take what the ground throws at it.</summary>
    public static bool KeepsTaming(double hitsFraction) => hitsFraction >= GiveUpHitsFraction;

    public static bool HasMoreAttempts(int attempts) => attempts < MaxAttempts;

    public static bool MayTryAnotherGround(int grounds) => grounds < MaxGroundsPerTrip;

    /// <summary>
    /// True when the tamer has room for a beast and one in sight, in an area not full of tamers,
    /// or a ground in reach suits it.
    /// </summary>
    public static bool CanSeek(SosariaCharacter tamer) =>
        People.InWorld(tamer) && tamer.Followers < tamer.FollowersMax &&
        (!TamingGrounds.IsCrowded(tamer, tamer.Location) && BestQuarry(tamer, SightTiles, null) != null ||
         TamingGrounds.Pick(tamer) != null);

    /// <summary>
    /// True when the tamer taking a blow from <paramref name="aggressor"/> keeps taming instead of
    /// fighting back: the blow comes from the beast it works, or from a beast it may take up,
    /// and its hits hold. A beast that attacks it becomes its quarry.
    /// </summary>
    public static bool TakesBlowFrom(SosariaCharacter tamer, Mobile aggressor) =>
        tamer?.Routine?.CurrentSkill is TameSkill tame && tame.TakeUp(tamer, aggressor);

    /// <summary>True while the tamer's trip braves the ground's danger instead of running from it.</summary>
    public static bool BravesDanger(SosariaCharacter tamer) =>
        tamer?.Routine?.CurrentSkill is TameSkill && KeepsTaming(Vitals.HitsFraction(tamer));

    /// <summary>The strongest beast in range the tamer may try now, a nearer one when about as strong; null when none.</summary>
    public static BaseCreature BestQuarry(SosariaCharacter tamer, int range, ISet<Serial> passed)
    {
        var beasts = new List<BaseCreature>();
        var options = new List<(int Power, int Distance)>();

        foreach (var mobile in tamer.GetMobilesInRange(range))
        {
            if (mobile is BaseCreature creature && passed?.Contains(creature.Serial) != true &&
                TamingGrounds.IsQuarry(tamer, creature))
            {
                beasts.Add(creature);
                options.Add((TamingGrounds.ProfileOf(creature).Power, NavMetric.Chebyshev(tamer.Location, creature.Location)));
            }
        }

        var pick = TameRules.Choose(options);
        return pick == TameRules.NoChoice ? null : beasts[pick];
    }

    public override bool Begin(SosariaCharacter character)
    {
        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        if (character.Followers >= character.FollowersMax)
        {
            return CannotStart("has no room for another pet");
        }

        _tamer = character;
        _passed.Clear();
        _walk = null;
        _quarry = null;
        _ground = null;
        _attempting = false;
        _attempts = 0;
        _grounds = 0;
        _tamed = 0;
        _practiced = 0;
        _endsAt = Core.Now + TripTime;

        if (!TamingGrounds.IsCrowded(character, character.Location) && BestQuarry(character, SightTiles, _passed) is { } near)
        {
            TamingGrounds.Claim(character, character.Location);
            StartApproach(near);
            return true;
        }

        return StartTrip() || CannotStart(NoBeastWhy);
    }

    public override SkillStatus Tick()
    {
        var status = TickTrip();

        if (status != SkillStatus.Running)
        {
            EndPracticeSession();
        }

        return status;
    }

    private SkillStatus TickTrip()
    {
        if (_tamer == null || _tamer.Deleted || !_tamer.Alive || !People.InWorld(_tamer))
        {
            return Fail(LeftWorldReason);
        }

        if (Core.Now >= _endsAt)
        {
            return _tamed > 0 ? Finish() : Fail("ran out of time on the taming trip");
        }

        if (Quarry != null && !KeepsTaming(Vitals.HitsFraction(_tamer)))
        {
            return Fail($"the {_quarry.Name} was too much for it");
        }

        return _phase switch
        {
            Phase.Travel => TickTravel(),
            Phase.Seek => TickSeek(),
            Phase.Approach => TickApproach(),
            _ => TickAttempt()
        };
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        Unclaim();
        EndPracticeSession();
        _tamer = null;
    }

    public override void Resume(TimeSpan held)
    {
        _endsAt = SkillClock.Shift(_endsAt, held);
        _walk?.Resume(held);
    }

    /// <summary>
    /// A beast attacking the tamer on its trip: its current quarry, or a beast it may take up while
    /// it works none. A trip that never began or has ended belongs to no tamer and takes up nothing.
    /// </summary>
    internal bool TakeUp(SosariaCharacter tamer, Mobile aggressor)
    {
        if (tamer == null || tamer != _tamer || aggressor is not BaseCreature beast || !KeepsTaming(Vitals.HitsFraction(_tamer)))
        {
            return false;
        }

        if (beast == _quarry && _phase is Phase.Approach or Phase.Attempt)
        {
            return true;
        }

        if (_attempting || !TamingGrounds.IsQuarry(_tamer, beast))
        {
            return false;
        }

        StartApproach(beast);
        return true;
    }

    private bool StartTrip()
    {
        var ground = TamingGrounds.Pick(_tamer);

        if (ground == null)
        {
            return false;
        }

        _ground = ground;
        _grounds++;
        TamingGrounds.Claim(_tamer, ground.Place.Arrival);
        _phase = Phase.Travel;
        // The bound node names the floor: a dungeon ground lies far below the land over it.
        _walk = new TravelSkill(ground.Place.ApproachPoint(NavWorld.GraphFor(_tamer.HomeFacet)), GroundArrivalRange, arrivalFloor: true);

        if (!_walk.Begin(_tamer))
        {
            _walk = null;
            _phase = Phase.Seek;
        }

        return true;
    }

    private SkillStatus TickTravel()
    {
        if (_walk != null && _walk.Tick() == SkillStatus.Running)
        {
            // A beast met on the road is as good as one on the ground.
            return BestQuarry(_tamer, SightTiles, _passed) is { } met ? Approach(met) : SkillStatus.Running;
        }

        _walk?.Abort();
        _walk = null;
        _phase = Phase.Seek;
        return SkillStatus.Running;
    }

    private SkillStatus TickSeek()
    {
        var range = _ground != null && _tamer.InRange(_ground.Place.Arrival, GroundSightTiles) ? GroundSightTiles : SightTiles;

        if (BestQuarry(_tamer, range, _passed) is { } quarry)
        {
            return Approach(quarry);
        }

        if (_ground != null)
        {
            TamingGrounds.MarkDry(_tamer, _ground.Place.Arrival);
            Unclaim();
        }

        if (MayTryAnotherGround(_grounds) && StartTrip())
        {
            return SkillStatus.Running;
        }

        return _tamed > 0 ? Finish() : Fail(NoBeastWhy);
    }

    private SkillStatus Approach(BaseCreature quarry)
    {
        StartApproach(quarry);
        return SkillStatus.Running;
    }

    private void StartApproach(BaseCreature quarry)
    {
        _walk?.Abort();
        _walk = null;
        _quarry = quarry;
        _quarryTries = 0;
        _attempting = false;
        _phase = Phase.Approach;
        PetOrders.Direct(_tamer, force: true);
    }

    private SkillStatus TickApproach()
    {
        if (!StillQuarry())
        {
            return PassOver();
        }

        // Near the beast the pets hear "all stay"; a new order waits for the command gap.
        if (HoldsPets)
        {
            PetOrders.Direct(_tamer, force: false);
        }

        if (_tamer.InRange(_quarry, TargetRange))
        {
            // A beast in reach behind a wall or a tree is one the cursor cannot take.
            if (!_tamer.InLOS(_quarry))
            {
                return PassOver();
            }

            _walk?.Abort();
            _walk = null;
            _tamer.Motor.ClearMoveIntent();
            _phase = Phase.Attempt;
            return SkillStatus.Running;
        }

        return WalkTo(TargetRange) ? SkillStatus.Running : PassOver();
    }

    private SkillStatus TickAttempt()
    {
        if (!_attempting)
        {
            if (!StillQuarry())
            {
                return PassOver();
            }

            if (!_tamer.InRange(_quarry, TargetRange))
            {
                _phase = Phase.Approach;
                return SkillStatus.Running;
            }

            // A spell in the hands or a cursor up waits: the engine takes no skill then.
            return _tamer.Spell != null || _tamer.Target != null ? SkillStatus.Running : StartAttempt();
        }

        if (_quarry.ControlMaster == _tamer)
        {
            return Tamed();
        }

        if (Core.TickCount - _tamer.NextSkillTime < 0 && Core.Now < _attemptEnds)
        {
            // The engine stops a try once the beast is six tiles off: the tamer keeps close.
            if (!_tamer.InRange(_quarry, StayCloseTiles))
            {
                WalkTo(TargetRange);
            }

            return SkillStatus.Running;
        }

        _attempting = false;
        _attempts++;
        _quarryTries++;
        Talk.Maybe(_tamer, TalkCategory.TameFail, TalkOdds.TameFailPercent, new TalkSlots { Item = TalkWords.Foe(_quarry) });
        return HasMoreAttempts(_attempts) ? SkillStatus.Running : Fail($"failed to tame the {_quarry.Name}");
    }

    /// <summary>Uses Animal Taming on the beast the way a client does: the skill, then its cursor on the beast.</summary>
    private SkillStatus StartAttempt()
    {
        if (!Server.Skills.UseSkill(_tamer, SkillName.AnimalTaming) || _tamer.Target is not { } cursor)
        {
            return SkillStatus.Running;
        }

        cursor.Invoke(_tamer, _quarry);

        // A started try holds the skill clock until its last round; a refusal gives it back at once.
        if (Core.TickCount - _tamer.NextSkillTime >= 0)
        {
            return PassOver();
        }

        _attempting = true;
        _attemptEnds = Core.Now + AttemptTimeout;
        return SkillStatus.Running;
    }

    private SkillStatus Tamed()
    {
        var pet = _quarry;
        _attempting = false;
        _quarry = null;
        _tamed++;
        Talk.Maybe(_tamer, TalkCategory.TameSuccess, TalkOdds.TameSuccessPercent, new TalkSlots { Item = TalkWords.Foe(pet) });

        if (PetKeeper.Keeps(_tamer, pet))
        {
            LogTame("tamed", pet);
            PetKeeper.LetGoSparePets(_tamer);
            return Finish();
        }

        // A practice beast goes back to the wild at once, as players let their training tames go.
        LogTame("tamed and let go", pet);
        PetKeeper.Release(_tamer, pet);
        _passed.Add(pet.Serial);
        _practiced++;

        // A full session closes practice: the rest of the trip looks for a keeper only.
        if (TameRules.SessionFull(_practiced))
        {
            TamePractice.Close(_tamer, Core.Now);
        }

        _phase = Phase.Seek;
        return SkillStatus.Running;
    }

    /// <summary>A trip that let practice beasts go ends the tamer's session: its taming rests a while.</summary>
    private void EndPracticeSession()
    {
        if (_practiced > 0 && _tamer != null)
        {
            TamePractice.Close(_tamer, Core.Now);
            _practiced = 0;
        }
    }

    private SkillStatus PassOver()
    {
        if (_quarry != null)
        {
            _passed.Add(_quarry.Serial);
        }

        _walk?.Abort();
        _walk = null;
        _quarry = null;
        _attempting = false;
        _phase = Phase.Seek;
        return SkillStatus.Running;
    }

    private bool StillQuarry() =>
        _quarry is { Deleted: false, Alive: true } && _quarry.Map == _tamer.Map &&
        (_attempting || TamingGrounds.IsQuarry(_tamer, _quarry));

    /// <summary>Walks at the beast itself, so the walk follows it as it roams; false when no walk will start.</summary>
    private bool WalkTo(int range)
    {
        if (_walk != null && _walk.Tick() == SkillStatus.Running)
        {
            return true;
        }

        _walk?.Abort();
        _walk = new GoToSkill(_quarry, range);

        if (_walk.Begin(_tamer))
        {
            return true;
        }

        _walk = null;
        return false;
    }

    /// <summary>The trip ends with a pet kept or practice done: the pets are called to heel.</summary>
    private SkillStatus Finish()
    {
        _walk?.Abort();
        _walk = null;
        Unclaim();
        _phase = Phase.Seek;
        PetOrders.Direct(_tamer, force: true);
        return SkillStatus.Done;
    }

    private void Unclaim() => TamingGrounds.Unclaim(_tamer);

    private void LogTame(string what, BaseCreature pet)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} {What} a {Beast} after {Tries} tries at {Location}",
                _tamer.Name,
                what,
                pet.GetType().Name,
                _quarryTries + 1,
                _tamer.Location
            );
        }
    }
}
