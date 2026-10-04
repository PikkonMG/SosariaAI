using System;
using System.Collections.Generic;
using Server;

namespace SosariaAI.Mobiles;

/// <summary>
/// A closed loop of patrol points and a cursor that walks it forever.
/// </summary>
public sealed class PatrolRoute
{
    private readonly Point3D[] _goals;
    private int _index;

    public PatrolRoute(IReadOnlyList<Point3D> points)
    {
        if (points == null || points.Count == 0)
        {
            throw new ArgumentException("A patrol route needs at least one point.", nameof(points));
        }

        _goals = new Point3D[points.Count];

        for (var i = 0; i < points.Count; i++)
        {
            _goals[i] = points[i];
        }
    }

    public int Count => _goals.Length;

    public Point3D Current => _goals[_index];

    public void Advance() => _index = (_index + 1) % _goals.Length;
}
