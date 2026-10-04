using System;
using System.IO;
using Server.Json;
using SosariaAI.Behaviour;
using SosariaAI.Combat;
using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using SosariaAI.Spawning;
using Xunit;

namespace SosariaAI.Tests;

public class PersonaPromptTests
{
    private const string PersonaWriterKey = "\"personaWriter\"";
    private const string OldBrainJson = "{ \"enabled\": false, \"personaWritesPerMinute\": 3 }";
    private const string WriterOffBrainJson = "{ \"enabled\": false, \"personaWriter\": false }";

    private static readonly PersonaFacts Miner = new(
        "Tessa",
        true,
        PersonClass.Miner,
        SkillTier.Journeyman,
        PersonTrait.Cautious | PersonTrait.Greedy,
        PersonWealth.Poor,
        "Minoc",
        "SoL",
        EraBand.T2A
    );

    [Fact]
    public void UserMessage_NamesThePerson()
    {
        var user = PersonaPrompt.UserMessage(Miner);

        Assert.Contains("Name: Tessa", user);
        Assert.Contains("Gender: female", user);
        Assert.Contains("journeyman miner", user);
        Assert.Contains("cautious, greedy", user);
        Assert.Contains("short on coin", user);
        Assert.Contains("Home town: Minoc", user);
        Assert.Contains("[SoL]", user);
        Assert.Contains("Guild tag: none", PersonaPrompt.UserMessage(Miner with { GuildTag = null }));
    }

    [Fact]
    public void SystemMessage_HoldsTheChatRulesTheShapeAndWhatIsNotInTheEra()
    {
        var early = PersonaPrompt.SystemMessage(EraBand.T2A);
        var modern = PersonaPrompt.SystemMessage(EraBand.Modern);

        Assert.Contains("lowercase", early);
        Assert.Contains("no roleplay emotes", early);
        Assert.Contains(PersonaPrompt.JsonShape, early);
        Assert.Contains("paladin", early);
        Assert.Contains("gargish", early);
        Assert.DoesNotContain("do not exist yet", modern);
        Assert.Contains(PersonaPrompt.EraText(EraBand.Modern), modern);
        Assert.Contains(PersonaPrompt.NoInviteRule, early);
    }

    [Fact]
    public void JsonShape_AsksForSpareLines_OverWhatTheRulesKeep()
    {
        var shape = PersonaPrompt.JsonShape;

        Assert.True(PersonaPrompt.SpareLines > 0);
        Assert.True(PersonaPrompt.SpareFewLines > 0);
        Assert.Contains($"\"idleLines\":[{PersonaDraftRules.IdleCount + PersonaPrompt.SpareLines} lines]", shape);
        Assert.Contains($"\"greetingLines\":[{PersonaDraftRules.GreetingCount + PersonaPrompt.SpareLines} lines]", shape);
        Assert.Contains($"\"returnLines\":[{PersonaDraftRules.ReturnCount + PersonaPrompt.SpareFewLines} lines]", shape);
        Assert.Contains($"\"combatLines\":[{PersonaDraftRules.CombatCount + PersonaPrompt.SpareFewLines} lines]", shape);
        Assert.Contains($"\"lootLines\":[{PersonaDraftRules.LootCount + PersonaPrompt.SpareFewLines} lines]", shape);
        Assert.Contains($"\"wants\":[{PersonaDraftRules.WantCount} items]", shape);
    }

    [Fact]
    public void SystemMessage_FramesWantsAsLongHopes_NotTodaysPlan()
    {
        var system = PersonaPrompt.SystemMessage(EraBand.T2A);

        Assert.Contains("long hopes for some later day", system);
        Assert.Contains("never a plan for today", system);
        Assert.DoesNotContain("working toward", system);
    }

    [Fact]
    public void IsPersonaWrite_OnlyForTheNewKind()
    {
        Assert.True(PersonaPrompt.IsPersonaWrite(BrainEventKind.PersonaWrite));
        Assert.False(PersonaPrompt.IsPersonaWrite(BrainEventKind.Musing));
        Assert.False(DecisionEvents.IsChat(BrainEventKind.PersonaWrite));
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.PersonaWrite, speakerIsPlayer: true));
    }

    [Fact]
    public void SettingDefaultsOn_AndTheNewFileWritesIt()
    {
        Assert.Equal(BrainConfiguration.DefaultPersonaWritesPerMinute, new BrainConfiguration().PersonaWritesPerMinute);
        Assert.Equal(BrainConfiguration.DefaultPersonaWritesPerMinute, BrainFile.CreateDefault().PersonaWritesPerMinute);
        Assert.True(BrainConfiguration.DefaultPersonaWritesPerMinute > 0);
        Assert.True(BrainConfiguration.DefaultPersonaWriter);
        Assert.Equal(BrainConfiguration.DefaultPersonaWriter, BrainFile.CreateDefault().PersonaWriter);
        Assert.Contains(PersonaWriterKey, JsonConfig.Serialize(BrainFile.CreateDefault()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OldBrainJson, true)]
    [InlineData(WriterOffBrainJson, false)]
    public void PersonaWriterSwitch_LoadsFromBrainJson_AndAnOldFileKeepsItOn(string json, bool expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-brain-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, json);
            Assert.Equal(expected, BrainFile.LoadOrCreate(path).PersonaWriter);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
