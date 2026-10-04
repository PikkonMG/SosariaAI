using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// Town groups are fine. A packed tile against a wall is not: walkers fail steps and
/// the group never opens. When too many people share a two-tile ring, a person who
/// stands about picks a quieter tile still next to the group once, when its stand ends
/// (<see cref="SosariaAI.Skills.LoiterPace"/>). A shove judged again on every think sent the whole
/// group stepping back and forth as each move packed the next tile. Pure.
/// </summary>
public static class CrowdSpread
{
    public const int PackedCount = 4;
    public const int TightRange = 2;
    public const int SpreadRadius = 8;
    public const int LingerRadius = 10;

    public static bool IsPacked(int nearbyCount) => nearbyCount >= PackedCount;

    public static int CountWithin(Point3D from, IReadOnlyList<Point3D> others, int range)
    {
        if (others == null)
        {
            return 0;
        }

        var count = 0;

        for (var i = 0; i < others.Count; i++)
        {
            if (NavMetric.Chebyshev(from, others[i]) <= range)
            {
                count++;
            }
        }

        return count;
    }

    public static Point3D StepAway(Point3D from, IReadOnlyList<Point3D> others)
    {
        if (others == null || others.Count == 0)
        {
            return from;
        }

        var best = from;
        var bestCrowd = CountWithin(from, others, TightRange);
        var bestAway = 0;

        for (var dx = -SpreadRadius; dx <= SpreadRadius; dx++)
        {
            for (var dy = -SpreadRadius; dy <= SpreadRadius; dy++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var at = new Point3D(from.X + dx, from.Y + dy, from.Z);
                var crowd = CountWithin(at, others, TightRange);
                var away = NavMetric.Chebyshev(from, at);

                if (crowd < bestCrowd || (crowd == bestCrowd && away > bestAway))
                {
                    best = at;
                    bestCrowd = crowd;
                    bestAway = away;
                }
            }
        }

        return best;
    }
}
