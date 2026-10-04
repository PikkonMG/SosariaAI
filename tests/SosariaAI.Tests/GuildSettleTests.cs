using System;
using Server;
using Server.Guilds;
using SosariaAI.Combat;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EngineGuildsCollection
{
    /// <summary>Tests that found engine guilds or read the world's guild list run alone.</summary>
    public const string Name = "Engine guilds";
}

/// <summary>
/// The rebalance of the plugin's guilds reaches every person on its bind, but never a
/// person a player's guild has accepted: it waits to use the stone.
/// </summary>
[Collection(EngineGuildsCollection.Name)]
public class GuildSettleTests
{
    private const int SearchLimit = 2000;
    private static uint _nextSerial = 0x7B41;

    static GuildSettleTests() => Timer.Init(0);

    public GuildSettleTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    [Fact]
    public void Settle_NeverMovesAPlayersRecruit()
    {
        var founder = Character("Felucca:founder#1");
        var guild = new Guild(founder, "Players Of Test", "ZZQ");
        var recruit = Character(GuildedId());
        guild.Accepted.Add(recruit);

        try
        {
            Assert.True(GuildRecruits.Recruited(recruit));

            EngineGuilds.Settle(recruit, thief: false, murderer: false);

            Assert.Equal(GuildCatalog.None, recruit.GuildIndex);
        }
        finally
        {
            guild.Disband();
        }
    }

    [Fact]
    public void Settle_GivesAnyoneElseTheirRoll()
    {
        var id = GuildedId();
        var person = Character(id);

        Assert.False(GuildRecruits.Recruited(person));

        EngineGuilds.Settle(person, thief: false, murderer: false);

        Assert.Equal(GuildCatalog.SeedFor(id, thief: false, person.Build.IsFighter), person.GuildIndex);
    }

    [Fact]
    public void Settle_MovesAWorkerOutOfOrderAndChaos_AndLeavesAFighterThere()
    {
        var id = GuildedId(seed => GuildCatalog.AlignmentOf(seed) != GuildType.Regular);
        var side = GuildCatalog.SeedFor(id, thief: false, fighter: true);
        var worker = Character(id);
        var fighter = Character(id);
        fighter.Build = BuildPresets.SwordsmanNovice();
        worker.GuildIndex = side;
        fighter.GuildIndex = side;

        EngineGuilds.Settle(worker, thief: false, murderer: false);
        EngineGuilds.Settle(fighter, thief: false, murderer: false);

        Assert.False(worker.Build.IsFighter);
        Assert.NotEqual(GuildCatalog.None, worker.GuildIndex);
        Assert.Equal(GuildType.Regular, GuildCatalog.AlignmentOf(worker.GuildIndex));
        Assert.Equal(side, fighter.GuildIndex);
    }

    /// <summary>An id whose roll puts it in a plugin guild, so a move would show.</summary>
    private static string GuildedId() => GuildedId(seed => seed != GuildCatalog.None);

    /// <summary>An id whose fighter's roll is one <paramref name="wanted"/> takes.</summary>
    private static string GuildedId(Func<int, bool> wanted)
    {
        for (var i = 0; i < SearchLimit; i++)
        {
            var id = $"Felucca:settle#{i}";

            if (wanted(GuildCatalog.SeedFor(id, thief: false, fighter: true)))
            {
                return id;
            }
        }

        throw new Xunit.Sdk.XunitException("no guilded id");
    }

    private static SosariaCharacter Character(string id)
    {
        var character = new SosariaCharacter((Serial)_nextSerial++) { CharacterId = id };
        character.DefaultMobileInit();
        return character;
    }
}
