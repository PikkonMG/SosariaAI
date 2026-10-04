using System;
using Server;
using Server.Mobiles;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Mount a nearby owned BaseMount (horse, ostard, llama, and other IMount).
/// Owned followers first; wild mounts stay for AnimalTaming via Tame.
/// A mount beyond arm's reach is walked to: a pet told to Follow trails behind,
/// and a beast stands where it was tamed.
/// </summary>
public sealed class MountSkill : Skill
{
    public const string RidesWhy = "already rides";
    public const string NoMountWhy = "no mount of its own in reach";
    public const string WalkFailedWhy = "the walk to its mount failed";
    public const string ClimbFailedWhy = "could not climb on its mount";

    private SosariaCharacter _rider;
    private BaseMount _target;
    private GoToSkill _walk;

    public override string Name => SkillKinds.Mount;

    /// <summary>The mount it walks to and climbs on.</summary>
    public override JobTarget? AimedAt => _target == null ? null : new JobTarget(JobTargetRest.KeyOf(_target), Point3D.Zero);

    public override bool Begin(SosariaCharacter character)
    {
        _rider = character;
        _target = null;
        _walk = null;

        if (!People.InWorld(character))
        {
            return CannotStart(NotInWorldReason);
        }

        if (!MountRules.RiderMayMount(character.Mounted))
        {
            return CannotStart(RidesWhy);
        }

        _target = OwnedMounts.Nearest(character, MountRules.SearchTiles);
        return _target != null || CannotStart(NoMountWhy);
    }

    public override SkillStatus Tick()
    {
        if (_rider == null || _rider.Deleted || !People.InWorld(_rider))
        {
            return Fail(NotInWorldReason);
        }

        if (!MountRules.RiderMayMount(_rider.Mounted))
        {
            return Fail(RidesWhy);
        }

        var chosen = _target is { Deleted: false } ? _target : OwnedMounts.Nearest(_rider, MountRules.SearchTiles);

        if (chosen == null)
        {
            return Fail(NoMountWhy);
        }

        _target = chosen;

        if (MountRules.NeedsWalk(NavMetric.Chebyshev(_rider.Location, chosen.Location)))
        {
            return WalkToward(chosen);
        }

        _walk?.Abort();
        _walk = null;
        return OwnedMounts.Climb(_rider, chosen) ? SkillStatus.Done : Fail(ClimbFailedWhy);
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _rider = null;
    }

    public override void Resume(TimeSpan held) => _walk?.Resume(held);

    /// <summary>The mount walks too, so the goal is the animal itself, not a fixed tile.</summary>
    private SkillStatus WalkToward(BaseMount chosen)
    {
        if (_walk == null)
        {
            _walk = new GoToSkill(chosen, MountRules.ReachTiles);

            if (!_walk.Begin(_rider))
            {
                _walk = null;
                return Fail(WalkFailedWhy);
            }
        }

        var status = _walk.Tick();

        if (status == SkillStatus.Failed)
        {
            _walk = null;
            return Fail(WalkFailedWhy);
        }

        return SkillStatus.Running;
    }
}
