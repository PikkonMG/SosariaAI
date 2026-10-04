using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Combat;

/// <summary>
/// What one glance at a room tells a player: how many hostiles, what they rate together,
/// how many are on me, how many of those can hit me this second, where the mass of them
/// stands, and how far the nearest two are.
/// </summary>
public readonly record struct RoomPicture(
    int Count,
    int Threat,
    int Attackers,
    int CloseAttackers,
    int CloseThreat,
    int CenterX,
    int CenterY,
    int NearestDistance,
    int SecondDistance
)
{
    public bool Any => Count > 0;
}

/// <summary>
/// Adds up the hostiles of one scan. One tally lives per character and is reused, so a
/// survey allocates nothing after the first.
/// </summary>
public sealed class RoomTally
{
    private readonly List<HostileStats> _all = [];
    private readonly List<HostileStats> _close = [];
    private int _attackers;
    private long _sumX;
    private long _sumY;
    private int _nearest;
    private int _second;

    public RoomTally() => Reset();

    public void Reset()
    {
        _all.Clear();
        _close.Clear();
        _attackers = 0;
        _sumX = 0;
        _sumY = 0;
        _nearest = RoomSurvey.NoDistance;
        _second = RoomSurvey.NoDistance;
    }

    public void Add(int x, int y, int distance, bool onSelf, HostileStats stats)
    {
        _all.Add(stats);
        _sumX += x;
        _sumY += y;

        if (distance < _nearest)
        {
            _second = _nearest;
            _nearest = distance;
        }
        else if (distance < _second)
        {
            _second = distance;
        }

        if (!onSelf)
        {
            return;
        }

        _attackers++;

        if (RoomSurvey.IsClose(distance))
        {
            _close.Add(stats);
        }
    }

    /// <summary>The picture of everything added. An empty room centres on the origin.</summary>
    public RoomPicture Finish(int originX, int originY)
    {
        var count = _all.Count;
        return new RoomPicture(
            count,
            ThreatRating.Score(_all),
            _attackers,
            _close.Count,
            ThreatRating.Score(_close),
            count == 0 ? originX : (int)(_sumX / count),
            count == 0 ? originY : (int)(_sumY / count),
            _nearest,
            _second
        );
    }
}

/// <summary>
/// Pure room maths. A pack that strings out while it chases gives a runner the chance to
/// turn on the one in front; a runner goes away from the middle of the pack, not from the
/// one foe it looked at (<see cref="EscapeRules"/> picks where).
/// </summary>
public static class RoomSurvey
{
    public const int NoDistance = int.MaxValue;

    /// <summary>An attacker this close can land a blow this second.</summary>
    public const int CloseTiles = 2;

    /// <summary>The lead chaser must be this far ahead of the next one to be fought alone.</summary>
    public const int StragglerGapTiles = 4;

    /// <summary>A melee runner turns only on a chaser it can reach at once.</summary>
    public const int MeleeStragglerReach = 5;

    /// <summary>An archer turns on a chaser a little past its shooting band.</summary>
    public const int RangedStragglerSlack = 2;

    public static bool IsClose(int distance) => distance <= CloseTiles;

    /// <summary>Tiles between the nearest hostile and the next. No second hostile is an open gap.</summary>
    public static int Gap(int nearest, int second) =>
        nearest == NoDistance ? 0 : second == NoDistance ? NoDistance : second - nearest;

    /// <summary>True when the nearest chaser is alone in front and in reach of this style.</summary>
    public static bool IsStraggler(int nearest, int second, bool ranged, int keepMin, int keepMax)
    {
        if (nearest == NoDistance || Gap(nearest, second) < StragglerGapTiles)
        {
            return false;
        }

        return ranged
            ? nearest >= keepMin && nearest <= keepMax + RangedStragglerSlack
            : nearest <= MeleeStragglerReach;
    }

    /// <summary>A leg is still good while its end lies farther from the pack than the runner does.</summary>
    public static bool StillAway(Point3D goal, Point3D from, int centerX, int centerY)
    {
        var center = new Point3D(centerX, centerY, from.Z);
        return NavMetric.Chebyshev(goal, center) > NavMetric.Chebyshev(from, center);
    }
}
