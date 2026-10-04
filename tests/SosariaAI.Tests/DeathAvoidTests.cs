using System;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class DeathAvoidTests
{
    private const string Graveyard = "Britain Graveyard";
    private static readonly DateTime Noon = new(2026, 4, 1, 12, 0, 0);

    [Fact]
    public void ShouldAvoid_AfterTwoDeathsTheSameDay()
    {
        Assert.True(
            DeathAvoid.ShouldAvoid(
                Graveyard,
                Graveyard,
                Noon,
                Noon.AddHours(1),
                CareerSettings.DefaultDeathAvoidHours,
                deathsThereToday: 2,
                power: 200,
                areaDifficulty: 40
            )
        );
    }

    [Fact]
    public void ShouldAvoid_WithinHoursUntilPowerReturns()
    {
        Assert.True(
            DeathAvoid.ShouldAvoid(
                Graveyard,
                Graveyard,
                Noon,
                Noon.AddHours(2),
                CareerSettings.DefaultDeathAvoidHours,
                deathsThereToday: 1,
                power: 50,
                areaDifficulty: 40
            )
        );
        Assert.False(
            DeathAvoid.ShouldAvoid(
                Graveyard,
                Graveyard,
                Noon,
                Noon.AddHours(2),
                CareerSettings.DefaultDeathAvoidHours,
                deathsThereToday: 1,
                power: 80,
                areaDifficulty: 40
            )
        );
    }

    [Fact]
    public void ShouldAvoid_DifferentPlace_IsFalse()
    {
        Assert.False(
            DeathAvoid.ShouldAvoid(
                Graveyard,
                "Despise",
                Noon,
                Noon.AddHours(1),
                CareerSettings.DefaultDeathAvoidHours,
                2,
                40,
                120
            )
        );
    }

    [Fact]
    public void AfterDeath_CountsSamePlaceSameDay()
    {
        Assert.Equal(1, DeathAvoid.AfterDeath(null, Graveyard, 0, default, Noon));
        Assert.Equal(2, DeathAvoid.AfterDeath(Graveyard, Graveyard, 1, Noon, Noon.AddHours(1)));
        Assert.Equal(1, DeathAvoid.AfterDeath(Graveyard, Graveyard, 2, Noon, Noon.AddDays(1)));
    }
}
