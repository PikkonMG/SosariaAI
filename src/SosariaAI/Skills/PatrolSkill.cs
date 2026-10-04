using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;
using SosariaAI.Logging;
using SosariaAI.Navigation;
using SosariaAI.Behaviour;

namespace SosariaAI.Skills;

/// <summary>
/// Walks a closed loop of points once per step. Each point is reached with the route
/// planner, as every other walk is: the game pathfinder alone gave up on nearly every
/// point across town. A point with no route is skipped, so it never freezes the routine.
/// </summary>
public sealed class PatrolSkill : Skill
{
    private readonly IReadOnlyList<Point3D> _authored;
    private PatrolRoute _route;
    private static readonly ILogger logger = SosariaLog.For(typeof(PatrolSkill));

    private SosariaCharacter _character;
    private TravelSkill _walk;
    private int _pointsVisited;
    private int _pointsSkipped;

    public PatrolSkill(IReadOnlyList<Point3D> points)
    {
        _authored = points;
        _route = new PatrolRoute(points);
    }

    public override string Name => SkillKinds.Patrol;

    public override bool Begin(SosariaCharacter character)
    {
        _character = character;
        _walk = null;
        _pointsVisited = 0;
        _pointsSkipped = 0;

        if (character is not { Deleted: false } || !People.InWorld(character))
        {
            return false;
        }

        if (TownPatrol.NeedsLocalLoop(_authored, character.HomeCorner, HomeLeash.ConfiguredRadius()))
        {
            var local = TownPatrol.Build(NavWorld.DestinationsFor(character.HomeFacet), character.HomeCorner);

            if (local.Count == 0)
            {
                return false;
            }

            _route = new PatrolRoute(local);
        }

        return true;
    }

    public override SkillStatus Tick()
    {
        var goal = _route.Current;

        if (_character.InRange(goal, CharactersFile.DefaultGoToRange))
        {
            return AfterPoint(reached: true);
        }

        if (_walk == null)
        {
            _walk = new TravelSkill(goal, CharactersFile.DefaultGoToRange);

            if (!_walk.Begin(_character))
            {
                return Skip(goal);
            }
        }

        var status = _walk.Tick();

        return status switch
        {
            SkillStatus.Running => SkillStatus.Running,
            SkillStatus.Done => AfterPoint(reached: true),
            _ => Skip(goal)
        };
    }

    private SkillStatus Skip(Point3D goal)
    {
        if (SosariaSettings.LogActivity)
        {
            logger.Information(
                "{Name} could not reach patrol point {X},{Y},{Z}",
                _character.Name,
                goal.X,
                goal.Y,
                goal.Z
            );
        }

        return AfterPoint(reached: false);
    }

    private SkillStatus AfterPoint(bool reached)
    {
        if (reached)
        {
            _pointsVisited++;
        }
        else
        {
            _pointsSkipped++;
        }

        _walk?.Abort();
        _walk = null;
        _route.Advance();

        if (_pointsVisited + _pointsSkipped < _route.Count)
        {
            return SkillStatus.Running;
        }

        return FinishStatus(_pointsVisited, _pointsSkipped);
    }

    /// <summary>
    /// A lap that skips more points than it reaches is not a patrol. Reporting Done
    /// cleared the failure count, so a route of dead points ran again every few
    /// seconds forever.
    /// </summary>
    internal static SkillStatus FinishStatus(int visited, int skipped) =>
        visited == 0 || skipped > visited ? SkillStatus.Failed : SkillStatus.Done;

    public override void Resume(TimeSpan held) => _walk?.Resume(held);

    public override void Abort()
    {
        _walk?.Abort();
        _walk = null;
    }
}
