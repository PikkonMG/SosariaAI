using System;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class MusingTests
{
    private static BrainEvent Musing() =>
        new(
            BrainEventKind.Musing,
            (Serial)1u,
            "Connor",
            null,
            Serial.Zero,
            false,
            string.Empty,
            "cutting wood",
            "Britain, Felucca",
            DateTime.UtcNow
        );

    [Fact]
    public void Musing_IsChatSoItUsesTheFreeChatProvider()
    {
        Assert.True(DecisionEvents.IsChat(BrainEventKind.Musing));
    }

    [Fact]
    public void Musing_IsNotADecision()
    {
        // A musing must never replace the character's routine.
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.Musing, speakerIsPlayer: false));
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.Musing, speakerIsPlayer: true));
    }

    [Fact]
    public void Musing_AsksForASpokenLineNotARoutine()
    {
        var messages = PromptBuilder.Build(Persona.CreateNeutral(), Musing(), [], 160);
        var user = messages[1].Content;

        Assert.Contains("Something just happened", user);
        Assert.Contains("Do not repeat an idea", user);
        Assert.DoesNotContain("\"choose\"", messages[0].Content);
    }

    [Fact]
    public void Musing_CarriesTheCharactersOwnLife()
    {
        var life = new LifePrompt
        {
            Ambition = "saving 400 of 2000 gold for a small boat",
            AmbitionMood = "well along",
            DayPart = "Evening",
            Opinion = "plain"
        };

        var system = PromptBuilder.Build(Persona.CreateNeutral(), Musing(), [], 160, life)[0].Content;

        Assert.Contains("a small boat", system);
        Assert.Contains("Evening", system);
    }

    [Fact]
    public void Musing_ListsRecentSpokenLinesSoTheModelDoesNotRepeatThem()
    {
        var life = new LifePrompt
        {
            Ambition = "saving for a small boat",
            AmbitionMood = "well along",
            DayPart = "Morning",
            Opinion = "plain",
            RecentSpeech = ["Aye, the axe is keen today. Another load and I'm a step closer to that boat."]
        };

        var system = PromptBuilder.Build(Persona.CreateNeutral(), Musing(), [], 160, life)[0].Content;

        Assert.Contains("Do not repeat these lines or the same idea in different words:", system);
        Assert.Contains("Aye, the axe is keen today", system);
    }
}
