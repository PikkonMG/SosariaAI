using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Navigation;

public static class NavMetric
{
    public static double Planar(Point3D a, Point3D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The tile distance between any two points: the larger of the X and Y gaps.</summary>
    public static int Chebyshev(IPoint2D a, IPoint2D b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public static int Chebyshev(Point3D a, Point3D b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return dx > dy ? dx : dy;
    }

    public static double Distance(Point3D from, Point3D to)
    {
        var planar = Planar(from, to);
        var dz = Math.Abs(from.Z - to.Z);

        if (dz <= NavLimits.SameFloorMaxDeltaZ)
        {
            return planar;
        }

        return planar + (dz - NavLimits.SameFloorMaxDeltaZ) * NavLimits.FloorPenaltyPerZ;
    }

    public static bool WithinLegCap(Point3D a, Point3D b) =>
        Chebyshev(a, b) <= NavLimits.MaxLegDistance;

    /// <summary>
    /// Tiles walked from one spot to another when the public moongates may shorten the
    /// trip: to the gate nearest the start, through the ring, and on from the gate nearest
    /// the goal. Every public gate reaches every other, so the step through costs nothing.
    /// A Magincia person reaches Despise by the Yew gate, though the straight line crosses
    /// the sea. With no gates the trip is the straight walk.
    /// </summary>
    public static int ByMoongate(Point3D from, Point3D to, IReadOnlyList<Point3D> moongates)
    {
        var direct = Chebyshev(from, to);

        if (moongates is not { Count: > 0 })
        {
            return direct;
        }

        return Math.Min(direct, NearestOf(from, moongates) + NearestOf(to, moongates));
    }

    /// <summary>Tiles from <paramref name="at"/> to the nearest of the points; <see cref="int.MaxValue"/> for none.</summary>
    public static int NearestOf(Point3D at, IReadOnlyList<Point3D> points)
    {
        var nearest = int.MaxValue;

        for (var i = 0; i < points.Count; i++)
        {
            nearest = Math.Min(nearest, Chebyshev(at, points[i]));
        }

        return nearest;
    }

    /// <summary>Two spots share a floor when their height gap is one a walk can cross.</summary>
    public static bool SameFloor(IPoint3D a, IPoint3D b) => SameFloor(a.Z, b.Z);

    /// <summary>Two heights are one floor when their gap is one a walk can cross.</summary>
    public static bool SameFloor(int aZ, int bZ) =>
        Math.Abs(aZ - bZ) <= NavLimits.SameFloorMaxDeltaZ;
}
