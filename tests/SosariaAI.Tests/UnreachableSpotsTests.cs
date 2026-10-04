using System;
using Server;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class UnreachableSpotsTests
{
    private static readonly DateTime Start = new(2026, 9, 12, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Note_IsRememberedThenForgotten()
    {
        var spots = new UnreachableSpots();
        var camp = new Point3D(3515, 2128, 0);

        spots.Note(camp, Start);

        Assert.Equal([camp], spots.Active(Start + TimeSpan.FromMinutes(1)));
        Assert.Empty(spots.Active(Start + SpotMemory.Memory));
    }

    [Fact]
    public void Note_NearAnOldSpot_RefreshesItInsteadOfAddingOne()
    {
        var spots = new UnreachableSpots();

        spots.Note(new Point3D(100, 100, 0), Start);
        spots.Note(new Point3D(100 + SpotMemory.SameSpotTiles, 100, 0), Start + TimeSpan.FromMinutes(5));

        Assert.Single(spots.Active(Start + SpotMemory.Memory));
    }

    [Fact]
    public void Note_KeepsOnlyTheNewest()
    {
        var spots = new UnreachableSpots();
        const int apart = 100;

        for (var i = 0; i < SpotMemory.MaxSpots + 1; i++)
        {
            spots.Note(new Point3D(i * apart, 0, 0), Start);
        }

        var active = spots.Active(Start);

        Assert.Equal(SpotMemory.MaxSpots, active.Count);
        Assert.DoesNotContain(new Point3D(0, 0, 0), active);
    }
}
