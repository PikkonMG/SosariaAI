using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// One flee run. Begins once and runs leg by leg away from the strongest hostile in sight,
/// each leg to a place it can reach (<see cref="EscapeRoute"/>): along a coast or a wall
/// instead of into the water, back the way it came, or to a road node, leaning toward its
/// PvP base's door when one is near (see <see cref="HouseBases"/>), else toward the nearest
/// bank when that lies the same way. A leg that fails is not tried again. Ends when
/// the threat is gone or a distance is covered. Never chosen inside a guarded region.
/// </summary>
public sealed class FleeSkill : Skill
{
    public const int MaxFailedLegs = 3;
    public const int FleeLimitSeconds = 60;
    public static readonly TimeSpan FleeLimit = TimeSpan.FromSeconds(FleeLimitSeconds);

    public const string NoWayWhy = "no way to run from the threat";
    public const string LegsFailedWhy = "every way it ran was blocked";
    public const string TimeUpWhy = "could not get clear of the threat in time";

    private readonly List<Point3D> _failedGoals = new();
    private SosariaCharacter _character;
    private Point3D _start;
    private Point3D _goal;
    private GoToSkill _walk;
    private bool _begunFlee;
    private DateTime _started;
    private int _failedLegs;

    public override string Name => SkillKinds.Flee;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _start = character.Location;
        _begunFlee = false;
        _started = Core.Now;
        _failedLegs = 0;
        _failedGoals.Clear();
        var threat = ThreatLocation();

        if (threat != character.Location)
        {
            character.Memory.Danger.Note(threat, Core.Now);
        }

        if (FleeRules.KeepsAwayFromParty(character.Memory.Danger.RecentRuns(Core.Now)))
        {
            Party.LeaveTrip(character);
        }

        character.SetRunPace();
        character.Combatant = null;
        character.Warmode = false;

        return StartLeg(threat) || CannotStart(NoWayWhy);
    }

    public override SkillStatus Tick()
    {
        if (_character == null || _character.Deleted)
        {
            return Fail(LeftWorldReason);
        }

        if (!_begunFlee)
        {
            _begunFlee = true;
            _character.BeginFlee(TimeSpan.FromSeconds(FleeRules.CoveredTiles));
        }

        var covered = NavMetric.Chebyshev(_start, _character.Location);
        var threat = HuntSkill.SightThreat(_character);

        if (FleeRules.IsDone(threat, covered))
        {
            _character.RestoreTownStance();
            return SkillStatus.Done;
        }

        if (ShouldGiveUp(_failedLegs, Core.Now, _started))
        {
            _character.RestoreTownStance();
            return Fail(GiveUpWhy(_failedLegs));
        }

        var walk = _walk?.Tick() ?? SkillStatus.Failed;

        if (walk == SkillStatus.Running)
        {
            return SkillStatus.Running;
        }

        if (walk == SkillStatus.Failed)
        {
            _failedLegs++;
            _failedGoals.Add(_goal);
        }

        if (!StartLeg(ThreatLocation()))
        {
            _failedLegs++;
        }

        return SkillStatus.Running;
    }

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
        _character?.RestoreTownStance();
    }

    public override void Resume(TimeSpan held)
    {
        _started = SkillClock.Shift(_started, held);
        _walk?.Resume(held);
    }

    /// <summary>Why a flee gave up: its legs kept failing, or its time ran out first.</summary>
    public static string GiveUpWhy(int failedLegs) => failedLegs >= MaxFailedLegs ? LegsFailedWhy : TimeUpWhy;

    /// <summary>A flee that keeps failing its legs, or runs too long, ends so GoHome can take over.</summary>
    public static bool ShouldGiveUp(int failedLegs, DateTime now, DateTime started) =>
        failedLegs >= MaxFailedLegs || (started != default && now - started >= FleeLimit);

    /// <summary>Where a runner heads when it can: the door of its own house, else the nearest bank under the guards.</summary>
    public static Point3D? SafePlace(SosariaCharacter character) =>
        HouseBases.SafeDoor(character) ??
        NavWorld.DestinationsFor(character.HomeFacet)
            ?.Nearest(character.Location, DestinationKind.Bank)
            ?.Arrival;

    private bool StartLeg(Point3D threat)
    {
        _walk?.Abort();
        _walk = null;
        if (EscapeRoute.PickGoal(_character, threat, FleeRules.CoveredTiles, SafePlace(_character), _failedGoals) is not { } goal)
        {
            return false;
        }

        _goal = goal;
        _walk = new GoToSkill(goal, CharactersFile.DefaultGoToRange);

        if (_walk.Begin(_character))
        {
            return true;
        }

        _walk = null;
        _failedGoals.Add(goal);
        return false;
    }

    private Point3D ThreatLocation()
    {
        var hostile = HuntSkill.StrongestHostile(_character) ?? _character.Combatant;
        return hostile is { Deleted: false } ? hostile.Location : _character.Location;
    }
}
