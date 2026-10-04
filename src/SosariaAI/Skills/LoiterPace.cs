using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// One person's stand-about clock, kept by its motor across skills. It stands still for a
/// dwell, turns to someone near, then strolls to one free tile inside its home ring that a
/// straight walk reaches, and stands again. A packed tile is left once, for the quietest
/// tile in reach, not re-judged every think. A stroll never ends in a doorway, where a
/// person holds the door open and blocks the way. World thread only.
/// </summary>
public sealed class LoiterPace
{
    private readonly SosariaCharacter _character;
    private DateTime _standUntil;
    private IPoint3D _goal;
    private int _stepsLeft;
    private Point3D _homeSeen;
    private int _rangeSeen;

    public LoiterPace(SosariaCharacter character) => _character = character;

    public void Tick(int stayWeight)
    {
        var home = _character.Home;
        var range = _character.RangeHome;

        if (home != _homeSeen || range != _rangeSeen)
        {
            // A new ring from a new skill: start from it now, not after the old stand.
            _homeSeen = home;
            _rangeSeen = range;
            _goal = null;
            _standUntil = default;
        }

        if (_goal != null)
        {
            ContinueWalk(stayWeight);
            return;
        }

        if (Core.Now < _standUntil)
        {
            return;
        }

        var at = _character.Location;

        if (LoiterPaceRules.MustReturn(at, home, range))
        {
            StartWalk(home, stayWeight);
        }
        else if (_standUntil == default)
        {
            // A person who just came into its ring stands first; it does not stroll on at once.
            Stand(stayWeight);
        }
        else
        {
            PickStroll(at, home, range, stayWeight);
        }
    }

    /// <summary>Turns to the nearest visible person in talking range: you talk to someone, not to a wall.</summary>
    public static void FaceNearest(Mobile member)
    {
        Mobile nearest = null;
        var best = double.MaxValue;

        foreach (var mobile in member.GetMobilesInRange(MeetingRules.ChatRange))
        {
            if (mobile == member || !mobile.Alive || !People.Perceives(member, mobile) || !mobile.Player)
            {
                continue;
            }

            var distance = member.GetDistanceToSqrt(mobile);

            if (distance < best)
            {
                best = distance;
                nearest = mobile;
            }
        }

        if (nearest != null)
        {
            member.Direction = member.GetDirectionTo(nearest);
        }
    }

    private void PickStroll(Point3D at, Point3D home, int range, int stayWeight)
    {
        var others = Neighbours(at);
        var packed = CrowdSpread.IsPacked(CrowdSpread.CountWithin(at, others, CrowdSpread.TightRange));

        if (!packed && (stayWeight <= LoiterPaceRules.StandStill || LoiterPaceRules.PinnedToHome(home, range)))
        {
            Stand(stayWeight);
            return;
        }

        var walker = Standable.Walker(_character.Map);
        Point3D? best = null;
        var bestCrowd = int.MaxValue;

        for (var i = 0; i < LoiterPaceRules.StrollTries; i++)
        {
            var goal = packed && i == 0
                ? CrowdSpread.StepAway(at, others)
                : LoiterPaceRules.StrollGoal(at, home, range, Utility.Random(int.MaxValue), Utility.Random(int.MaxValue));

            if (goal == at || LoiterPaceRules.MustReturn(goal, home, range) ||
                DoorTiles.IsDoorway(_character.Map, goal.X, goal.Y) ||
                !WalkLine.TryWalk(walker, at.X, at.Y, at.Z, goal.X, goal.Y, null, out var goalZ))
            {
                continue;
            }

            var reached = new Point3D(goal.X, goal.Y, goalZ);
            var crowd = CrowdSpread.CountWithin(reached, others, CrowdSpread.TightRange);

            if (crowd < bestCrowd)
            {
                best = reached;
                bestCrowd = crowd;
            }
        }

        if (best is { } stroll)
        {
            StartWalk(stroll, stayWeight);
        }
        else
        {
            Stand(stayWeight);
        }
    }

    private void StartWalk(Point3D goal, int stayWeight)
    {
        _goal = goal;
        _stepsLeft = LoiterPaceRules.StepBudget(_character.Location, goal);
        ContinueWalk(stayWeight);
    }

    private void ContinueWalk(int stayWeight)
    {
        if (!_character.Motor.MoveToPoint(_goal) || --_stepsLeft <= 0)
        {
            Stand(stayWeight);
        }
    }

    private void Stand(int stayWeight)
    {
        _goal = null;
        _character.Motor.ClearMoveIntent();
        _standUntil = Core.Now + LoiterPaceRules.Dwell(stayWeight, Utility.Random(int.MaxValue));

        if (LoiterPaceRules.ShouldFace(Utility.Random(int.MaxValue)))
        {
            FaceNearest(_character);
        }
    }

    private List<Point3D> Neighbours(Point3D at)
    {
        var found = new List<Point3D>();

        foreach (var mobile in _character.Map.GetMobilesInRange(at, CrowdSpread.SpreadRadius))
        {
            if (mobile != _character && mobile.Alive && mobile.Player && People.Perceives(_character, mobile))
            {
                found.Add(mobile.Location);
            }
        }

        return found;
    }
}
