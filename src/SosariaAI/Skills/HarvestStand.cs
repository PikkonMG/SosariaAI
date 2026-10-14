using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Stand tiles beside a resource. Cardinals first, then diagonals. The resource
/// tile is never a stand tile.
/// </summary>
public static class HarvestStand
{
    public const int CardinalNeighbourCount = 4;
    public const int DiagonalNeighbourCount = 4;
    public const int NeighbourCount = CardinalNeighbourCount + DiagonalNeighbourCount;

    public const int NoStep = 0;
    public const int Step = 1;

    /// <summary>A walk range that ends on the stand tile itself.</summary>
    public const int OnTheTile = 0;

    private const int NotANeighbour = -1;

    private static readonly int[] CardinalOffsetX = [NoStep, Step, NoStep, -Step];
    private static readonly int[] CardinalOffsetY = [-Step, NoStep, Step, NoStep];
    private static readonly int[] DiagonalOffsetX = [Step, Step, -Step, -Step];
    private static readonly int[] DiagonalOffsetY = [-Step, Step, Step, -Step];

    public static IReadOnlyList<Point2D> Neighbours(Point3D resource)
    {
        var points = new Point2D[NeighbourCount];

        for (var i = 0; i < CardinalNeighbourCount; i++)
        {
            points[i] = new Point2D(resource.X + CardinalOffsetX[i], resource.Y + CardinalOffsetY[i]);
        }

        for (var i = 0; i < DiagonalNeighbourCount; i++)
        {
            points[CardinalNeighbourCount + i] = new Point2D(
                resource.X + DiagonalOffsetX[i],
                resource.Y + DiagonalOffsetY[i]
            );
        }

        return points;
    }

    /// <summary>
    /// How far short of <paramref name="standAt"/> the walk there may stop and still leave
    /// <paramref name="resource"/> inside the engine's harvest reach (<paramref name="reach"/>,
    /// the definition's MaxRange), never more than <paramref name="walkRange"/>. The walk once
    /// stopped the whole reach short of the stand tile, one tile beside the tree, so it ended up
    /// to three tiles from a tree the engine reaches at two: it refused every swing "That is too
    /// far away" (500446), and 163 woodcutting and 50 mining trips of one night ended with
    /// 100 and more swings and nothing cut, at skills 66 to 99 that never rose. Pure.
    /// </summary>
    public static int WalkRange(int walkRange, int reach, Point3D standAt, Point3D resource) =>
        Math.Max(OnTheTile, Math.Min(walkRange, reach - NavMetric.Chebyshev(standAt, resource)));

    public static bool TryBeside(
        Point3D resource,
        IReadOnlyList<Point3D> candidates,
        Point3D from,
        out Point3D standAt
    )
    {
        standAt = default;
        var bestDistance = int.MaxValue;
        var bestOrder = int.MaxValue;
        var found = false;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var order = OrderOf(candidate.X - resource.X, candidate.Y - resource.Y);

            if (order == NotANeighbour)
            {
                continue;
            }

            var distance = NavMetric.Chebyshev(from, candidate);

            if (found && (distance > bestDistance || (distance == bestDistance && order >= bestOrder)))
            {
                continue;
            }

            bestDistance = distance;
            bestOrder = order;
            standAt = candidate;
            found = true;
        }

        return found;
    }

    private static int OrderOf(int dx, int dy)
    {
        for (var i = 0; i < CardinalNeighbourCount; i++)
        {
            if (dx == CardinalOffsetX[i] && dy == CardinalOffsetY[i])
            {
                return i;
            }
        }

        for (var i = 0; i < DiagonalNeighbourCount; i++)
        {
            if (dx == DiagonalOffsetX[i] && dy == DiagonalOffsetY[i])
            {
                return CardinalNeighbourCount + i;
            }
        }

        return NotANeighbour;
    }
}
