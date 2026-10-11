using System;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// Runs the party order that stands for a member (<see cref="PartyOrders.Current"/>), and
/// switches when a new one replaces it. Follow, Regroup and Back walk behind the leader; Hold
/// stands and faces it; Go to walks to the place by the roads, pausing while the leader is
/// far behind, and holds there until the leader arrives; Attack takes the target and leaves
/// the blows to the combat brain. Done only when no order stands any more.
/// </summary>
public sealed class PartyOrderSkill : Skill
{
    public const string NoOrderWhy = "no party order stands";

    private SosariaCharacter _character;
    private PartyOrder _running;
    private Skill _inner;
    private bool _pausedForLeader;

    public override string Name => SkillKinds.PartyOrder;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _running = null;
        _inner = null;
        _pausedForLeader = false;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        return PartyOrders.Current(character) != null || CannotStart(NoOrderWhy);
    }

    public override SkillStatus Tick()
    {
        if (!People.InWorld(_character))
        {
            return Fail(LeftWorldReason);
        }

        var order = PartyOrders.Current(_character);

        if (order == null)
        {
            EndInner();
            return SkillStatus.Done;
        }

        if (!ReferenceEquals(order, _running))
        {
            Switch(order);
        }

        return _running.Kind switch
        {
            PartyOrderKind.Hold => TickHold(),
            PartyOrderKind.GoTo => TickGoTo(),
            PartyOrderKind.Attack => TickAttack(),
            _ => TickWalk()
        };
    }

    public override void Abort() => EndInner();

    public override void Resume(TimeSpan held) => _inner?.Resume(held);

    private void Switch(PartyOrder order)
    {
        EndInner();
        _running = order;

        switch (order.Kind)
        {
            case PartyOrderKind.Hold:
            {
                _character.Motor.Stop();
                return;
            }
            case PartyOrderKind.Attack:
            {
                if (World.FindMobile(order.Target) is { Deleted: false, Alive: true } foe && FollowSkill.TakesLeadersFoe(_character, foe))
                {
                    _character.Combatant = foe;
                }

                return;
            }
            case PartyOrderKind.GoTo:
            {
                var walk = new TravelSkill(order.Place, CharactersFile.DefaultGoToRange, onFoot: true);

                if (walk.Begin(_character))
                {
                    _inner = walk;
                }
                else
                {
                    PartyOrders.CannotReach(_character);
                }

                return;
            }
            case PartyOrderKind.Back:
            {
                CombatBrain.StandDown(_character);
                BeginFollow(holdsFire: true);
                return;
            }
            default:
            {
                BeginFollow(holdsFire: false);
                return;
            }
        }
    }

    private void BeginFollow(bool holdsFire)
    {
        var follow = new FollowSkill(null, PartyOrderRules.CloseRange, holdsFire);

        if (follow.Begin(_character))
        {
            _inner = follow;
        }
        else
        {
            PartyOrders.Drop(_character);
        }
    }

    private SkillStatus TickHold()
    {
        var leader = GameParty.LeaderToFollow(_character);

        if (leader == null)
        {
            PartyOrders.Drop(_character);
            return SkillStatus.Done;
        }

        if (leader.Map == _character.Map)
        {
            _character.Direction = _character.GetDirectionTo(leader);

            // A hold at a place ends when the leader gets there.
            if (_running.Place != Point3D.Zero && !PartyOrderRules.LagsBehind(sameMap: true, (int)_character.GetDistanceToSqrt(leader)))
            {
                PartyOrders.Settle(_character);
            }
        }

        return SkillStatus.Running;
    }

    private SkillStatus TickGoTo()
    {
        if (_inner == null)
        {
            return SkillStatus.Running;
        }

        var leader = GameParty.LeaderToFollow(_character);
        var leaderPresent = leader is { Deleted: false };
        var sameMap = leaderPresent && leader.Map == _character.Map;
        var distance = sameMap ? (int)_character.GetDistanceToSqrt(leader) : 0;

        if (PartyOrderRules.PausesWalk(leaderPresent, sameMap, distance))
        {
            if (!_pausedForLeader)
            {
                _pausedForLeader = true;
                _character.Motor.Stop();
            }

            return SkillStatus.Running;
        }

        _pausedForLeader = false;
        var status = _inner.Tick();

        if (status == SkillStatus.Done)
        {
            EndInner();
            PartyOrders.Arrived(_character);
        }
        else if (status == SkillStatus.Failed)
        {
            EndInner();
            PartyOrders.CannotReach(_character);
        }

        return SkillStatus.Running;
    }

    private SkillStatus TickAttack()
    {
        if (World.FindMobile(_running.Target) is not { Deleted: false, Alive: true } foe || foe.Map != _character.Map)
        {
            PartyOrders.Settle(_character);
        }
        else if (_character.Combatant == null && FollowSkill.TakesLeadersFoe(_character, foe))
        {
            _character.Combatant = foe;
        }

        return SkillStatus.Running;
    }

    private SkillStatus TickWalk()
    {
        if (_inner == null)
        {
            return SkillStatus.Done;
        }

        var status = _inner.Tick();

        if (status == SkillStatus.Running)
        {
            return status;
        }

        // The follow ended: no leader stands to walk behind.
        EndInner();
        PartyOrders.Drop(_character);
        return SkillStatus.Done;
    }

    private void EndInner()
    {
        _inner?.Abort();
        _inner = null;
        _pausedForLeader = false;
    }
}
