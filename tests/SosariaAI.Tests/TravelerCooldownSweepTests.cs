using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TravelerCooldownSweepCollection
{
    public const string Name = "Traveler cooldown sweep";
}

/// <summary>
/// A failed trip cools down its cell and goal for seconds, but the cooldown stayed in the map
/// for good: one entry per cell and goal that ever failed, all night. A sweep drops the lapsed
/// ones. The class runs alone, as the cooldowns are shared by every search.
/// </summary>
[Collection(TravelerCooldownSweepCollection.Name)]
public class TravelerCooldownSweepTests
{
    private const int CellTiles = 16;
    private const string Goal = "sweep-test goal";

    [Fact]
    public void SweepCooldowns_DropsEveryLapsedCooldown_AndKeepsTheLiveOnes()
    {
        var graph = new NavGraph(FacetNames.Felucca, []);
        var trips = Traveler.CooldownSweepAbove + 1;

        for (var i = 0; i < trips; i++)
        {
            Traveler.NoteFailedRoute(graph, new Point3D(i * CellTiles, 0, 0), Goal);
        }

        Traveler.SweepCooldowns(Environment.TickCount64);
        Assert.True(Traveler.InFailedCooldown(graph, Point3D.Zero, Goal));

        var lapsedAt = Environment.TickCount64 + Traveler.FailedRouteCooldownMs + 1;

        Assert.True(Traveler.SweepCooldowns(lapsedAt) >= trips);
        Assert.Equal(0, Traveler.SweepCooldowns(lapsedAt));
        Assert.False(Traveler.InFailedCooldown(graph, Point3D.Zero, Goal));
    }
}
