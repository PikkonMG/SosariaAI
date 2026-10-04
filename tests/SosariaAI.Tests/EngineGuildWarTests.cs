using Server;
using Server.Guilds;
using SosariaAI.Behaviour;
using SosariaAI.Mobiles;
using SosariaAI.Social;
using Xunit;

namespace SosariaAI.Tests;

/// <summary>
/// Before Samurai Empire the engine reads a war from the guild's enemy list, not from a war
/// declaration. The plugin wrote only declarations, so in the Second Age a war blow was a
/// crime and the gray watch drew on the warrior.
/// </summary>
[Collection(EngineGuildsCollection.Name)]
public class EngineGuildWarTests
{
    private const int FirstGuild = 1;
    private const int SecondGuild = 2;
    private static uint _nextSerial = 0x7C01;

    static EngineGuildWarTests() => Timer.Init(0);

    public EngineGuildWarTests()
    {
        TestMap.EnsureInternal();
        TestMap.EnsureRunningWorld();
    }

    [Fact]
    public void DeclareWar_IsAWarTheEngineReadsAndALawfulFight()
    {
        var first = Member(FirstGuild);
        var second = Member(SecondGuild);
        var firstGuild = (Guild)first.Guild;
        var secondGuild = (Guild)second.Guild;

        try
        {
            Assert.False(EngineGuilds.AtWar(first, second));

            EngineGuilds.DeclareWar(FirstGuild, SecondGuild, GuildWarRegistry.WarDuration);

            Assert.True(firstGuild.IsWar(secondGuild));
            Assert.True(EngineGuilds.AtWar(first, second));
            Assert.True(FactionWar.LawfulFight(first, second));
            Assert.True(first.ConflictLegal(second));

            EngineGuilds.EndWar(firstGuild, secondGuild);

            Assert.False(firstGuild.IsWar(secondGuild));
            Assert.False(FactionWar.LawfulFight(first, second));
        }
        finally
        {
            firstGuild.Disband();
            secondGuild.Disband();
        }
    }

    private static SosariaCharacter Member(int guildIndex)
    {
        var character = new SosariaCharacter((Serial)_nextSerial++);
        character.DefaultMobileInit();
        _ = new Guild(character, GuildCatalog.All[guildIndex].Name, GuildCatalog.All[guildIndex].Tag);
        return character;
    }
}
