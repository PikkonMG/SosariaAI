using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class DangerSpotsTests
{
    private static readonly DateTime Start = new(2026, 9, 12, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Note_IsRememberedThenForgotten()
    {
        var spots = new DangerSpots();
        var graveyard = new Point3D(2740, 880, 0);

        spots.Note(graveyard, Start);

        Assert.Equal([graveyard], spots.Active(Start + TimeSpan.FromMinutes(1)));
        Assert.Empty(spots.Active(Start + SpotMemory.Memory));
    }

    [Fact]
    public void Note_NearAnOldSpot_RefreshesItInsteadOfAddingOne()
    {
        var spots = new DangerSpots();

        spots.Note(new Point3D(100, 100, 0), Start);
        spots.Note(new Point3D(100 + SpotMemory.SameSpotTiles, 100, 0), Start + TimeSpan.FromMinutes(5));

        Assert.Single(spots.Active(Start + SpotMemory.Memory));
    }

    [Fact]
    public void Note_KeepsOnlyTheNewest()
    {
        var spots = new DangerSpots();
        const int apart = 100;

        for (var i = 0; i <= SpotMemory.MaxSpots; i++)
        {
            spots.Note(new Point3D(i * apart, 0, 0), Start);
        }

        var active = spots.Active(Start);
        Assert.Equal(SpotMemory.MaxSpots, active.Count);
        Assert.DoesNotContain(new Point3D(0, 0, 0), active);
    }

    [Fact]
    public void FindPath_SteersRoundAPlaceTheCharacterFled()
    {
        // Two roads home: a short one past the graveyard and a longer one round it.
        const int roadStep = 30;
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("start", 0, 0, "graveyard", "detour"),
                Node("graveyard", roadStep, 0, "start", "home"),
                Node("detour", roadStep, roadStep * 2, "start", "home"),
                Node("home", roadStep * 2, 0, "graveyard", "detour")
            ]
        );

        Assert.Equal(["start", "graveyard", "home"], NavSearch.FindPath(graph, "start", "home"));

        var path = NavSearch.FindPath(graph, "start", "home", -1, [new Point3D(roadStep, 0, 0)]);

        Assert.Equal(["start", "detour", "home"], path);
    }

    [Fact]
    public void FindPath_NoWayRound_StillUsesTheRoad()
    {
        var graph = new NavGraph(
            FacetNames.Felucca,
            [
                Node("start", 0, 0, "graveyard"),
                Node("graveyard", 30, 0, "start", "home"),
                Node("home", 60, 0, "graveyard")
            ]
        );

        var path = NavSearch.FindPath(graph, "start", "home", -1, [new Point3D(30, 0, 0)]);

        Assert.Equal(["start", "graveyard", "home"], path);
    }

    private static NavNode Node(string name, int x, int y, params string[] connects) =>
        new() { Name = name, X = x, Y = y, Z = 0, Connects = [.. connects] };

    [Fact]
    public void NoteSighting_SamePlace_CountsOneRunNotMany()
    {
        // A hostile that stays in sight is one escape, not one run per score tick.
        // Counting each tick pins the character to GoHome for as long as it watches.
        var spots = new DangerSpots();
        var gate = new Point3D(605, 888, 0);

        spots.NoteSighting(gate, Start);
        for (var i = 1; i < 60; i++)
        {
            spots.NoteSighting(gate, Start + TimeSpan.FromSeconds(i));
        }

        Assert.Equal(1, spots.RecentRuns(Start + TimeSpan.FromMinutes(1)));
        Assert.Single(spots.Active(Start + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void NoteSighting_ANewPlace_CountsAnotherRun()
    {
        var spots = new DangerSpots();

        spots.NoteSighting(new Point3D(100, 100, 0), Start);
        spots.NoteSighting(new Point3D(300, 300, 0), Start + TimeSpan.FromMinutes(1));

        Assert.Equal(2, spots.RecentRuns(Start + TimeSpan.FromMinutes(2)));
    }

    [Fact]
    public void NoteSighting_KeepsTheSpotAliveWhileItIsSeen()
    {
        // The run count ages out, but a threat still in sight keeps its spot fresh,
        // so routes keep steering around it.
        var spots = new DangerSpots();
        var gate = new Point3D(605, 888, 0);

        spots.NoteSighting(gate, Start);
        spots.NoteSighting(gate, Start + TimeSpan.FromMinutes(9));

        var later = Start + SpotMemory.Memory + TimeSpan.FromSeconds(30);
        Assert.Single(spots.Active(later));
        Assert.Equal(0, spots.RecentRuns(later));
    }

    [Fact]
    public void Sightings_TellWhenEachPlaceWasLastSeen()
    {
        // A walker reads how long a place lay quiet before it walks that road again.
        var spots = new DangerSpots();
        var gate = new Point3D(2701, 692, 5);
        var lastSeen = Start + TimeSpan.FromMinutes(1);

        spots.Note(gate, Start);
        spots.NoteSighting(gate, lastSeen);

        Assert.Equal([(gate, lastSeen)], spots.Sightings(Start + TimeSpan.FromMinutes(2)));
        Assert.Empty(spots.Sightings(lastSeen + SpotMemory.Memory));
    }

    [Fact]
    public void RecentRuns_CountsEveryRunThenForgetsIt()
    {
        // Three runs from the same graveyard refresh one spot, but each is still a run.
        var spots = new DangerSpots();
        var graveyard = new Point3D(1380, 1480, 0);

        spots.Note(graveyard, Start);
        spots.Note(graveyard, Start + TimeSpan.FromSeconds(12));
        spots.Note(graveyard, Start + TimeSpan.FromSeconds(24));

        Assert.Single(spots.Active(Start + TimeSpan.FromSeconds(30)));
        Assert.Equal(3, spots.RecentRuns(Start + TimeSpan.FromSeconds(30)));
        Assert.Equal(0, spots.RecentRuns(Start + TimeSpan.FromSeconds(24) + SpotMemory.Memory));
    }
}
