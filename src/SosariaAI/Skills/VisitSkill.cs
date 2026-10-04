using System;
using Server;
using Server.Logging;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

public sealed class VisitSkill : Skill
{
    public const string NeverReachedWhy = "never reached the friend";
    public const string FriendLeftTownWhy = "the friend left town";
    public const string FriendGoneWhy = "the friend died or is gone";
    public const string FriendLoggedOutWhy = "the friend logged out";
    public const string NoFriendNoWalkWhy = "no friend near and no walk to the meeting place";
    public const string MeetingWalkFailedWhy = "the walk to the meeting place failed";
    public const string NoWalkToFriendWhy = "no walk to the friend";
    public const string FriendWalkFailedWhy = "the walk to the friend failed";
    public const int GiveUpMinutes = 5;

    // A friend who steps a few tiles does not need a new route; a short hop is a plain move.
    public const int ReplanTiles = 6;
    public static readonly TimeSpan VisitDuration = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan GiveUp = TimeSpan.FromMinutes(GiveUpMinutes);

    private static readonly ILogger logger = SosariaLog.For(typeof(VisitSkill));

    private readonly string _fallback;
    private readonly PlaceStay _stay;
    private TravelSkill _walk;
    private SosariaCharacter _character;
    private Serial _targetSerial;
    private Point3D _walkDest;
    private DateTime _started;

    /// <summary>A visit with a friend sounds like a visit: a few scripted exchanges with it.</summary>
    public VisitSkill(string destination)
    {
        _fallback = destination;
        _stay = new PlaceStay(
            VisitDuration,
            IdleWanderSkill.WanderChanceToNotMove,
            turns => Meeting.TryChatNearby(_character, CurrentTarget() as SosariaCharacter, turns)
        );
    }

    public override string Name => SkillKinds.Visit;

    /// <summary>The friend it set out for, once it picked one.</summary>
    public override JobTarget? AimedAt =>
        _targetSerial == Serial.Zero ? null : new JobTarget(JobTargetRest.KeyOf(_targetSerial), Point3D.Zero);

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _walkDest = Point3D.Zero;
        _targetSerial = Serial.Zero;
        _started = Core.Now;
        _stay.Prepare(character);

        var target = FindTarget(character);

        if (target != null)
        {
            _targetSerial = target.Serial;

            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} is going to visit {Other}", character.Name, target.Name);
            }

            return true;
        }

        _walk = new TravelSkill(_fallback, CharactersFile.DefaultGoToRange);
        return _walk.Begin(character) || CannotStart(NoFriendNoWalkWhy);
    }

    public override SkillStatus Tick() => _stay.Finish(TickVisit());

    private SkillStatus TickVisit()
    {
        if (Core.Now - _started >= GiveUp)
        {
            ClearWalk();
            return EndVisit(NeverReachedWhy);
        }

        var target = CurrentTarget();

        if (WhyFriendIsGone(target) is { } gone)
        {
            ClearWalk();
            return EndVisit(gone);
        }

        if (target != null && _character.Map == target.Map && VisitRules.Reached(_character.Location, target.Location))
        {
            ClearWalk();
            return Linger();
        }

        if (target != null)
        {
            return WalkTo(target);
        }

        if (!_stay.Started)
        {
            var walk = _walk?.Tick() ?? SkillStatus.Failed;

            if (walk == SkillStatus.Running)
            {
                return SkillStatus.Running;
            }

            if (walk == SkillStatus.Failed)
            {
                return Fail(MeetingWalkFailedWhy);
            }

            ClearWalk();
        }

        return Linger();
    }

    public override void Abort()
    {
        ClearWalk();
        _stay.Abort();
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _stay.Resume(held);
        _walk?.Resume(held);
    }

    /// <summary>A visit that had started counts, even when the friend wandered off.</summary>
    public static SkillStatus GiveUpOutcome(bool lingered) =>
        lingered ? SkillStatus.Done : SkillStatus.Failed;

    private SkillStatus EndVisit(string reason) =>
        GiveUpOutcome(_stay.Started) == SkillStatus.Failed ? Fail(reason) : SkillStatus.Done;

    public static bool NeedsReplan(Point3D walkDest, Point3D target) =>
        NavMetric.Chebyshev(walkDest, target) > ReplanTiles;

    private SkillStatus Linger()
    {
        if (!_stay.Started)
        {
            _stay.Settle(CrowdSpread.LingerRadius);
        }

        return _stay.Tick();
    }

    private SkillStatus WalkTo(Mobile target)
    {
        var dest = target.Location;

        if (_character.Map == target.Map &&
            NavMetric.Chebyshev(_character.Location, dest) <= NavLimits.SoftLegDistance)
        {
            ClearWalk();
            _character.Motor.MoveTo(target, SosariaCombat.FollowRangeMax);
            return SkillStatus.Running;
        }

        if (_walk != null && NeedsReplan(_walkDest, dest))
        {
            ClearWalk();
        }

        if (_walk == null)
        {
            _walkDest = dest;
            _walk = new TravelSkill(dest, SosariaCombat.FollowRangeMax);

            if (!_walk.Begin(_character))
            {
                ClearWalk();
                return Fail(NoWalkToFriendWhy);
            }
        }

        var status = _walk.Tick();

        if (status != SkillStatus.Running)
        {
            ClearWalk();
        }

        return status == SkillStatus.Failed ? Fail(FriendWalkFailedWhy) : SkillStatus.Running;
    }

    /// <summary>
    /// Why the friend this visit set out for can no longer be visited, or null: it died or was
    /// deleted, it logged out, or it left town for a hunt or a dungeon, where it is not followed.
    /// </summary>
    private string WhyFriendIsGone(Mobile target)
    {
        if (_targetSerial == Serial.Zero)
        {
            return null;
        }

        if (target == null)
        {
            return FriendGoneWhy;
        }

        if (!People.InWorld(target))
        {
            return FriendLoggedOutWhy;
        }

        return IsVisitCandidate(_character, target) ? null : FriendLeftTownWhy;
    }

    private void ClearWalk()
    {
        _walk?.Abort();
        _walk = null;
        _walkDest = Point3D.Zero;
    }

    private Mobile CurrentTarget()
    {
        if (_targetSerial == Serial.Zero)
        {
            return null;
        }

        var mobile = World.FindMobile(_targetSerial);
        return mobile is { Deleted: false, Alive: true } ? mobile : null;
    }

    private static Mobile FindTarget(SosariaCharacter character)
    {
        var friend = FindPerson(character, MemoryChoiceRules.FriendId(MemoryStore.Shared, Recall.IdOf(character)));

        if (friend != null)
        {
            return friend;
        }

        return FindAnyOther(character);
    }

    /// <summary>The person with this long-term memory id, when that person may be visited.</summary>
    private static Mobile FindPerson(SosariaCharacter self, string personId)
    {
        if (string.IsNullOrWhiteSpace(personId) || World.Mobiles == null)
        {
            return null;
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (IsVisitPick(self, mobile) && Recall.IdOf(mobile) == personId)
            {
                return mobile;
            }
        }

        return null;
    }

    /// <summary>
    /// With no friend in mind yet, the nearest person who may be picked (<see cref="IsVisitPick"/>).
    /// The first person in the world list was Connor, and thirty-four people set out to visit him.
    /// </summary>
    private static Mobile FindAnyOther(SosariaCharacter self)
    {
        if (World.Mobiles == null)
        {
            return null;
        }

        Mobile nearest = null;
        var best = int.MaxValue;
        var selfId = Recall.IdOf(self);

        foreach (var mobile in World.Mobiles.Values)
        {
            if (!IsVisitPick(self, mobile))
            {
                continue;
            }

            if (MemoryChoiceRules.Avoids(MemoryStore.Shared, selfId, Recall.IdOf(mobile)))
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(self.Location, mobile.Location);

            if (distance < best)
            {
                best = distance;
                nearest = mobile;
            }
        }

        return nearest;
    }

    /// <summary>
    /// A person to set out for: one who may be visited, near, and settled (<see cref="VisitRules.MayPick"/>),
    /// and not a friend this visitor failed to reach the same way three times lately.
    /// </summary>
    private static bool IsVisitPick(SosariaCharacter self, Mobile mobile) =>
        VisitRules.MayPick(
            IsVisitCandidate(self, mobile),
            NavMetric.Chebyshev(self.Location, mobile.Location),
            IsSettled(mobile)
        ) &&
        !JobTargetRest.Rests(self, SkillKinds.Visit, JobTargetRest.KeyOf(mobile), Core.Now);

    /// <summary>
    /// A character standing about (<see cref="WorldPlay.IsIdle"/>) or working a skill on the spot
    /// (<see cref="PracticeRules.IsPractice"/>), and not off on a visit of its own; a human is
    /// taken as settled, since nothing says where it is going.
    /// </summary>
    public static bool IsSettled(Mobile mobile)
    {
        if (mobile is not SosariaCharacter character)
        {
            return true;
        }

        var current = character.Routine?.CurrentSkill;
        return current is not VisitSkill && (WorldPlay.IsIdle(character) || PracticeRules.IsPractice(current?.Name));
    }

    private static bool IsVisitCandidate(SosariaCharacter self, Mobile mobile)
    {
        if (mobile == null || mobile == self || mobile.Deleted || !People.Perceives(self, mobile))
        {
            return false;
        }

        var sameMap = People.InWorld(mobile) && mobile.Map == self.Map;
        var isPerson = mobile is PlayerMobile;
        return VisitRules.MayVisit(mobile.Alive, isPerson, sameMap, sameMap && SosariaCharacter.UnderGuards(mobile));
    }
}
