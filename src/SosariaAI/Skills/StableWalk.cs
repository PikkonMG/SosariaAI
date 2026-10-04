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
/// The walk across town to an animal trainer, for a stable-in or a claim. The stable is the
/// nearest catalog stable within the leash where a live trainer stands (<see cref="Find"/>):
/// a marker is only where the stable was drawn, and 186 claims reached one with nobody at it.
/// The walk ends in the trainer's hearing (<see cref="StableRules.TrainerHearingTiles"/>). The
/// trainer roams its counter while the tamer walks, so a walk that ends out of its hearing walks
/// on to the trainer itself, wherever it stands now. A stable the tamer failed at the same way
/// three times rests for that job (<see cref="JobTargetRest"/>).
/// </summary>
public sealed class StableWalk : Skill
{
    public const string NoStableWhy = "no stable in reach";
    public const string NoWalkWhy = "no walk to the stable";
    public const string WalkFailedWhy = "the walk to the stable failed";
    public const string NoTrainerWhy = "no animal trainer at the stable";

    private readonly string _job;
    private SosariaCharacter _owner;
    private Skill _leg;
    private Point3D _stable;
    private AnimalTrainer _trainer;
    private bool _closingIn;

    /// <param name="job">The stable job the walk is for: a stable it rests for that job is passed over.</param>
    public StableWalk(string job) => _job = job;

    public override string Name => SkillKinds.GoTo;

    /// <summary>The stable it walks to.</summary>
    public override JobTarget? AimedAt => _stable == Point3D.Zero ? null : new JobTarget(_stable.ToString(), _stable);

    /// <summary>The stable's marker, for the tamer's log line.</summary>
    public Point3D Stable => _stable;

    /// <summary>
    /// The nearest catalog stable within the leash with a live animal trainer at its counter,
    /// passing over a stable this owner rests for <paramref name="job"/>: the marker, and the
    /// trainer. Null when there is none.
    /// </summary>
    public static (Point3D Marker, AnimalTrainer Trainer)? Find(SosariaCharacter owner, string job)
    {
        var map = owner?.Map;
        var catalog = owner == null ? null : NavWorld.DestinationsFor(owner.HomeFacet);

        if (map == null || map == Map.Internal || catalog == null)
        {
            return null;
        }

        var reach = HomeLeash.ConfiguredRadius();
        var markers = catalog.NearestFirst(ShopFinder.AnimalTrainerToken, owner.Location, ShopFinder.StockedLooks);

        for (var i = 0; i < markers.Count; i++)
        {
            var marker = markers[i].Arrival;

            if (marker == Point3D.Zero || HomeLeash.BeyondLeash(marker, owner.Location, reach) ||
                JobTargetRest.Rests(owner, job, marker.ToString(), Core.Now))
            {
                continue;
            }

            if (VendorDeal.VendorsNear(map, marker, VendorDeal.CounterRange).Find(static vendor => vendor is AnimalTrainer) is
                AnimalTrainer trainer)
            {
                return (marker, trainer);
            }
        }

        return null;
    }

    public override bool Begin(SosariaCharacter character)
    {
        _owner = character;
        _leg = null;
        _stable = Point3D.Zero;
        _trainer = null;
        _closingIn = false;

        if (Find(character, _job) is not { } stable)
        {
            return CannotStart(NoStableWhy);
        }

        _stable = stable.Marker;
        _trainer = stable.Trainer;
        _leg = new TravelSkill(_trainer.Location, NavLimits.ShopArrivalRange, arrivalFloor: true);
        return _leg.Begin(character) || CannotStart(NoWalkWhy);
    }

    public override SkillStatus Tick()
    {
        if (_owner == null || _owner.Deleted || !People.InWorld(_owner) || _leg == null)
        {
            return Fail(LeftWorldReason);
        }

        var status = _leg.Tick();

        if (status == SkillStatus.Running)
        {
            return status;
        }

        if (TrainerHears())
        {
            _leg = null;
            return SkillStatus.Done;
        }

        if (status == SkillStatus.Failed && !_closingIn)
        {
            return Fail(WalkFailedWhy);
        }

        if (_closingIn || _trainer is not { Deleted: false, Alive: true } || _trainer.Map != _owner.Map)
        {
            return Fail(NoTrainerWhy);
        }

        // The trainer roamed off while the tamer walked: it walks on to the trainer itself.
        _closingIn = true;
        _leg = new GoToSkill(_trainer, StableRules.TrainerCloseTiles);
        return _leg.Begin(_owner) ? SkillStatus.Running : Fail(NoTrainerWhy);
    }

    public override void Abort()
    {
        _leg?.Abort();
        _leg = null;
    }

    public override void Resume(TimeSpan held) => _leg?.Resume(held);

    private bool TrainerHears() =>
        _trainer is { Deleted: false, Alive: true } && _trainer.Map == _owner.Map &&
        _owner.InRange(_trainer, StableRules.TrainerHearingTiles);
}
