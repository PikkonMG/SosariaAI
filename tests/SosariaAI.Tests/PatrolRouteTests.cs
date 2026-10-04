using System;
using Server;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class PatrolRouteTests
{
    [Fact]
    public void Constructor_RejectsEmptyRoute()
    {
        Assert.Throws<ArgumentException>(() => new PatrolRoute(Array.Empty<Point3D>()));
    }

    [Fact]
    public void Advance_LoopsBackToStart()
    {
        var route = new PatrolRoute([new Point3D(1, 1, 0), new Point3D(2, 2, 0)]);
        var first = route.Current;

        route.Advance();
        Assert.Equal(new Point3D(2, 2, 0), route.Current);

        route.Advance();
        Assert.Equal(first, route.Current);
    }
}
