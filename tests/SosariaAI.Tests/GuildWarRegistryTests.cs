using System;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

public class GuildWarRegistryTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void RecordAggression_StartsWarBetweenDifferentGuilds()
    {
        var wars = new GuildWarRegistry();

        Assert.True(wars.RecordAggression(1, 2, Now));
        Assert.True(wars.AtWar(2, 1, Now));
        Assert.False(wars.RecordAggression(2, 1, Now));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void FeedsWar_NotAMurderNorABlowOnARed(bool attackerRed, bool defenderRed, bool feeds) =>
        Assert.Equal(feeds, GuildWarRegistry.FeedsWar(attackerRed, defenderRed));

    [Fact]
    public void SameGuild_DoesNotStartWar()
    {
        var wars = new GuildWarRegistry();

        Assert.False(wars.RecordAggression(1, 1, Now));
        Assert.False(wars.AtWar(1, 1, Now));
    }

    [Fact]
    public void War_EndsAfterTheFeudGoesQuiet()
    {
        var wars = new GuildWarRegistry();
        wars.RecordAggression(0, 3, Now);

        Assert.False(wars.AtWar(0, 3, Now + GuildWarRegistry.WarDuration));
        Assert.False(wars.AtWar(0, 3, Now));
    }

    [Fact]
    public void SyncStep_KeepsTheSavedWarsOnceThenTheRegistryWins()
    {
        Assert.Equal(WarSync.None, GuildWarRegistry.SyncStep(engineWar: true, registryWar: true, seeding: false));
        Assert.Equal(WarSync.None, GuildWarRegistry.SyncStep(engineWar: false, registryWar: false, seeding: true));
        Assert.Equal(WarSync.Keep, GuildWarRegistry.SyncStep(engineWar: true, registryWar: false, seeding: true));
        Assert.Equal(WarSync.End, GuildWarRegistry.SyncStep(engineWar: true, registryWar: false, seeding: false));
        Assert.Equal(WarSync.Declare, GuildWarRegistry.SyncStep(engineWar: false, registryWar: true, seeding: false));
        Assert.Equal(WarSync.Declare, GuildWarRegistry.SyncStep(engineWar: false, registryWar: true, seeding: true));
    }
}
