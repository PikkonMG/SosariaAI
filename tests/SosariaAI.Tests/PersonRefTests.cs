using Server;
using Server.Mobiles;
using SosariaAI.Memory;
using SosariaAI.Mobiles;
using Xunit;

namespace SosariaAI.Tests;

public class PersonRefTests
{
    private const uint BotSerial = 0x7A01;
    private const uint IdlessBotSerial = 0x7A02;
    private const uint PlayerSerial = 0x7A03;
    private const uint CreatureSerial = 0x7A04;

    public PersonRefTests() => TestMap.EnsureInternal();

    [Fact]
    public void Of_ABotWithACharacterId_IsNamedByThatId()
    {
        var bot = new SosariaCharacter((Serial)BotSerial) { Name = "Halvard", CharacterId = "Felucca:halvard" };

        var person = PersonRef.Of(bot);

        Assert.Equal(new PersonRef(PersonRef.BotPrefix + "Felucca:halvard", "Halvard", true), person);
    }

    [Fact]
    public void Of_ABotWithNoCharacterId_IsNobody()
    {
        Assert.Null(PersonRef.Of(new SosariaCharacter((Serial)IdlessBotSerial) { Name = "Nameless" }));
    }

    [Fact]
    public void Of_ARealPlayer_IsNamedByItsSerial()
    {
        var player = new PlayerMobile((Serial)PlayerSerial) { Name = "Tamsin" };

        var person = PersonRef.Of(player);

        Assert.Equal(PersonRef.PlayerPrefix + player.Serial, person?.Id);
        Assert.Equal("Tamsin", person?.Name);
        Assert.False(person?.IsBot);
        Assert.Equal(PersonRef.Player(player.Serial, "Tamsin"), person);
    }

    [Fact]
    public void Of_ACreatureOrNoMobile_IsNobody()
    {
        Assert.Null(PersonRef.Of(new Mobile((Serial)CreatureSerial)));
        Assert.Null(PersonRef.Of(null));
    }
}
