using Server;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class IdentityLineTests
{
    [Fact]
    public void For_NamesClassTraitsPurseJobGuildAndRedFlag()
    {
        var line = IdentityLine.For("Journeyman Mage", PersonTrait.Brave | PersonTrait.Greedy, PersonWealth.Rich, "Bank", "BM", murderer: true);

        Assert.Equal(
            "You are a journeyman mage, brave and greedy, rich, your job today: bank, you wear the guild tag [BM], you are a known murderer.",
            line
        );
    }

    [Fact]
    public void For_LeavesOutWhatTheCharacterDoesNotHave()
    {
        var line = IdentityLine.For("Novice Miner", PersonTrait.None, PersonWealth.Poor, job: null, guildTag: null, murderer: false);

        Assert.Equal("You are a novice miner, short on coin.", line);
    }

    [Fact]
    public void Prompt_CarriesTheIdentity()
    {
        const string Identity = "You are a master archer, cautious, comfortable.";
        var evt = new BrainEvent(
            BrainEventKind.Spoken,
            (Serial)1u,
            "Connor",
            "Bob",
            (Serial)2u,
            true,
            "what do you do?",
            "idle",
            "Britain, Felucca",
            new System.DateTime(2026, 1, 1),
            Identity: Identity
        );

        var system = PromptBuilder.Build(PersonasFile.CreateDefaultConnor(), evt, [], 160)[0].Content;

        Assert.Contains(Identity, system);
    }
}
