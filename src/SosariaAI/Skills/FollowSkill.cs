using System;
using Server;
using Server.Logging;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Common;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;
using SosariaAI.Logging;

namespace SosariaAI.Skills;

/// <summary>
/// Walks with a leader: the leader of an authored crew, or, without one, whoever leads the
/// real party the character is in, a human player included. While that leader lies dead the
/// next living member leads. The follower goes where the leader goes: along the roads,
/// down the stairs, through the group's own gate at the muster (see <see cref="PartyGate"/>),
/// which a follower who can cast may open and hold for the rest, through a spell gate the
/// leader just walked into, and by its own
/// recall after a leader that recalled far away, to a dungeon door it has a rune for. It joins the
/// leader's fight, and in a real party it clears the corpses of the foes it fought once the
/// fight is over and the leader stands close.
/// </summary>
public sealed class FollowSkill : Skill
{
    private const string FollowedThroughGateEvent = "followed the leader through a gate";

    private static readonly ILogger logger = SosariaLog.For(typeof(FollowSkill));

    private readonly string _partyId;
    private readonly int _range;
    private readonly CorpseLoot _loot = new();
    private Mobile _lastFoe;
    private Point3D _lastFoeAt;
    private SosariaCharacter _character;
    private Skill _walk;
    private RecallSkill _recall;
    private DateTime _recallTriedAt;
    private Point3D _walkDest;
    private bool _waitingForRoute;
    private int _routeWaitTicks;
    private bool _sawTrip;
    private DateTime _waitStarted;
    private DateTime _started;

    public FollowSkill(string partyId, int range)
    {
        _partyId = partyId;
        _range = range;
    }

    public override string Name => SkillKinds.Follow;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        ClearWalk();
        _loot.Clear();
        _lastFoe = null;
        _sawTrip = false;
        _waitStarted = default;
        _recallTriedAt = default;
        _started = Core.Now;
        return character is { Deleted: false, Map: not null } &&
               character.Map != Map.Internal &&
               (Party.IsMember(_partyId, character.CharacterId) || GameParty.LeaderToFollow(character) != null);
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _waitStarted = SkillClock.Shift(_waitStarted, held);
    }

    public override SkillStatus Tick()
    {
        if (PartyGate.Involves(_character))
        {
            ClearWalk();

            if (PartyGate.Turn(_character) != GateTurn.None)
            {
                return SkillStatus.Running;
            }
        }

        var party = Party.Find(_partyId);

        // A real party is followed for as long as it lasts; it ends when the leader breaks it up.
        if (party == null || !Party.IsMember(_partyId, _character.CharacterId))
        {
            return TickRealParty();
        }

        if (TimeRules.Passed(_started, Core.Now, PartyWaitRules.FollowLimit))
        {
            ClearWalk();
            return SkillStatus.Done;
        }

        if (party.Disbanded || (_sawTrip && !party.TripActive))
        {
            ClearWalk();
            return SkillStatus.Done;
        }

        if (party.TripActive)
        {
            _sawTrip = true;
        }

        if (!party.TripActive)
        {
            return WaitForTrip(party);
        }

        if (_sawTrip && !Party.IsPresent(_partyId, _character.CharacterId))
        {
            var catchUp = Party.FindMobile(party.LeaderId);

            if (catchUp is { Deleted: false, IsGhost: false } && catchUp.Map == _character.Map)
            {
                return WalkTo(catchUp);
            }

            ClearWalk();
            return SkillStatus.Done;
        }

        if (Party.ShouldRest(_character))
        {
            ClearWalk();
            return SkillStatus.Running;
        }

        var leader = Party.FindMobile(party.LeaderId);

        if (leader == null || leader.Deleted || leader.IsGhost)
        {
            return SkillStatus.Running;
        }

        return KeepUp(leader);
    }

    public override void Abort()
    {
        _recall?.Abort();
        _recall = null;
        ClearWalk();
    }

    /// <summary>A real party with no authored crew behind it: follow whoever leads it now.</summary>
    private SkillStatus TickRealParty()
    {
        var leader = GameParty.LeaderToFollow(_character);

        if (leader == null)
        {
            ClearWalk();
            return SkillStatus.Done;
        }

        // A leader who hid, a hidden GM among them, cannot be walked after: the follower holds.
        if (!People.Perceives(_character, leader))
        {
            ClearWalk();
            return SkillStatus.Running;
        }

        NoteFallenFoe();

        if (_character.Combatant == null && leader.Map == _character.Map &&
            _character.GetDistanceToSqrt(leader) <= SosariaCombat.FollowFarTiles && _loot.Tick(_character))
        {
            ClearWalk();
            return SkillStatus.Running;
        }

        return KeepUp(leader);
    }

    /// <summary>A foe this member fought that is now dead leaves a corpse worth a look.</summary>
    private void NoteFallenFoe()
    {
        if (_lastFoe != null && (_lastFoe.Deleted || !_lastFoe.Alive))
        {
            _loot.Note(_lastFoe, _lastFoeAt);
            _lastFoe = null;
        }

        if (_character.Combatant is { Deleted: false, Alive: true } foe)
        {
            _lastFoe = foe;
            _lastFoeAt = foe.Location;
        }
    }

    private SkillStatus KeepUp(Mobile leader)
    {
        if (leader.Map != _character.Map || _character.GetDistanceToSqrt(leader) > SosariaCombat.FollowFarTiles)
        {
            return WalkTo(leader);
        }

        ClearWalk();
        _character.Motor.MoveTo(leader, _range);

        if (leader.Combatant is { } target && TakesLeadersFoe(_character, target) &&
            _character.GetDistanceToSqrt(target) <= _character.RangePerception)
        {
            _character.Combatant = target;
        }

        return SkillStatus.Running;
    }

    private SkillStatus WaitForTrip(Party party)
    {
        if (_waitStarted == default)
        {
            _waitStarted = Core.Now;
        }

        if (PartyWaitRules.GiveUpWait(
                Core.Now,
                _waitStarted,
                party.TripActive,
                party.IsGathering,
                PartyWaitRules.WaitLimit))
        {
            if (SosariaSettings.LogActivity)
            {
                logger.Information("{Name} stopped waiting for party {Id}", _character.Name, _partyId);
            }

            ClearWalk();
            return SkillStatus.Done;
        }

        return WalkTo(party.MeetAt);
    }

    /// <summary>
    /// A leader who just stepped into a spell gate is far away at once, while the gate still
    /// stands beside the follower. Walking into that gate is the way after it.
    /// </summary>
    private SkillStatus WalkTo(Mobile leader)
    {
        if (GateTravel.FindSpellGateToward(_character, leader.Location, leader.Map) is { } gate)
        {
            ClearWalk();
            return GateTravel.StepIntoSpellGate(_character, gate, FollowedThroughGateEvent) == GateStep.Refused
                ? SkillStatus.Failed
                : SkillStatus.Running;
        }

        if (leader.Map != _character.Map)
        {
            return SkillStatus.Running;
        }

        return RecallAfter(leader) ? SkillStatus.Running : WalkTo(leader.Location);
    }

    /// <summary>
    /// A leader that recalled far away is followed by a recall to a mark near it, when the
    /// follower has one and the means. True while the words are spoken.
    /// </summary>
    private bool RecallAfter(Mobile leader)
    {
        if (_recall != null)
        {
            if (_recall.Tick() == SkillStatus.Running)
            {
                return true;
            }

            _recall = null;
            ClearWalk();
            return false;
        }

        if (!FollowWalk.MayTryRecall(_recallTriedAt, Core.Now))
        {
            return false;
        }

        _recallTriedAt = Core.Now;

        if (!RecallRules.CanRecallToward(_character, leader.Location))
        {
            return false;
        }

        var recall = new RecallSkill(leader.Location);

        if (!recall.Begin(_character))
        {
            return false;
        }

        ClearWalk();
        _recall = recall;
        return true;
    }

    private SkillStatus WalkTo(Point3D dest)
    {
        var chebyshev = NavMetric.Chebyshev(_character.Location, dest);
        var useNav = FollowWalk.NeedsNav(chebyshev);

        if (_waitingForRoute)
        {
            // No route right now. Stand still, then ask the graph again rather than
            // waiting out the whole trip in one spot. The leader walking on is not a
            // reason to ask sooner.
            if (!FollowWalk.ShouldReplan(waitingForRoute: true, _walkDest != dest, ++_routeWaitTicks))
            {
                return SkillStatus.Running;
            }

            ClearWalk();
        }
        else if (_walk != null && FollowWalk.ShouldReplan(waitingForRoute: false, _walkDest != dest, 0))
        {
            ClearWalk();
        }

        if (_walk == null)
        {
            _walkDest = dest;
            _walk = useNav
                ? new TravelSkill(dest, CharactersFile.DefaultGoToRange)
                : new GoToSkill(dest, CharactersFile.DefaultGoToRange);

            if (!_walk.Begin(_character))
            {
                _walk = null;

                if (useNav)
                {
                    _waitingForRoute = true;
                    _routeWaitTicks = 0;
                    return SkillStatus.Running;
                }

                return SkillStatus.Failed;
            }
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        _walk = null;

        if (status == SkillStatus.Failed && useNav)
        {
            _waitingForRoute = true;
            _routeWaitTicks = 0;
            return SkillStatus.Running;
        }

        return status == SkillStatus.Failed ? SkillStatus.Failed : SkillStatus.Running;
    }

    /// <summary>
    /// A follower takes up the leader's foe unless it is running, or it ran from that foe or its
    /// pack lately or stood down from it (<see cref="WorldPlay.LeavesBeSide"/>). Every other way
    /// into a fight kept that grace; the follow step alone handed the runner its foe back each
    /// think, so it ran, turned and ran from the same person over and over. A blue in
    /// Buccaneer's Den takes it up only on a Den raid or when it is an outlaw hurting a friend
    /// (<see cref="WorldPlay.MayStartFightInDen"/>). A blow that would be a crime for the follower
    /// itself is not taken up: a leader's Order or Chaos foe is no foe of an unguilded friend, and
    /// 9 of 60 deaths to the Britain guards were followers turned gray by that blow.
    /// </summary>
    public static bool TakesLeadersFoe(SosariaCharacter follower, Mobile target) =>
        follower != null && target is { Deleted: false, Alive: true } &&
        !follower.CheckFlee() && !WorldPlay.LeavesBeSide(follower, target) &&
        WorldPlay.MayStartFightInDen(follower, target) && !follower.IsHarmfulCriminal(target);

    private void ClearWalk()
    {
        _walk?.Abort();
        _walk = null;
        _waitingForRoute = false;
        _routeWaitTicks = 0;
        _walkDest = Point3D.Zero;
    }
}
