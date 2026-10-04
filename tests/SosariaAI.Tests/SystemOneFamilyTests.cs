using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class SystemOneFamilyTests
{
    private const string TypeSafeUrl = "https://api.typesafe.ai";
    private const string GpuBoxUrl = "http://192.168.1.50:8013";

    [Fact]
    public void Detect_JevByTheTypeSafeAddress()
    {
        var provider = new ProviderDefinition { BaseUrl = TypeSafeUrl, Model = "anything" };

        Assert.Equal(SystemOneFamily.Jev, SystemOneFamilies.Detect("paid", provider));
    }

    [Fact]
    public void Detect_VonByTheModelName()
    {
        var provider = new ProviderDefinition { BaseUrl = GpuBoxUrl, Model = "von-latest" };

        Assert.Equal(SystemOneFamily.Von, SystemOneFamilies.Detect("gpu", provider));
    }

    [Fact]
    public void Detect_LayaByTheProviderName_WhenTheModelIsAuto()
    {
        var provider = new ProviderDefinition { BaseUrl = GpuBoxUrl, Model = "auto" };

        Assert.Equal(SystemOneFamily.Laya, SystemOneFamilies.Detect("laya", provider));
    }

    [Fact]
    public void Detect_TheFamilyKeyWins()
    {
        var provider = new ProviderDefinition { BaseUrl = TypeSafeUrl, Model = "jev-latest", Family = " Laya " };

        Assert.Equal(SystemOneFamily.Laya, SystemOneFamilies.Detect("jev", provider));
    }

    [Fact]
    public void Detect_AnUnknownModelGetsTheCarefulTrust()
    {
        var provider = new ProviderDefinition { BaseUrl = GpuBoxUrl, Model = "mystery-1", Family = "nonsense" };

        Assert.Equal(SystemOneFamily.Von, SystemOneFamilies.Detect("other", provider));
    }

    [Theory]
    [InlineData(SystemOneTask.Stance)]
    [InlineData(SystemOneTask.NextJob)]
    [InlineData(SystemOneTask.Intent)]
    [InlineData(SystemOneTask.Trade)]
    [InlineData(SystemOneTask.SpeechGate)]
    public void Handles_JevTakesEveryTask(SystemOneTask task) =>
        Assert.True(SystemOneFamilies.Handles(SystemOneFamily.Jev, task));

    [Theory]
    [InlineData(SystemOneFamily.Von)]
    [InlineData(SystemOneFamily.Laya)]
    public void Handles_TheFreeModelsTakeOnlyFightStances(SystemOneFamily family)
    {
        Assert.True(SystemOneFamilies.Handles(family, SystemOneTask.Stance));
        Assert.False(SystemOneFamilies.Handles(family, SystemOneTask.NextJob));
        Assert.False(SystemOneFamilies.Handles(family, SystemOneTask.Intent));
        Assert.False(SystemOneFamilies.Handles(family, SystemOneTask.Trade));
        Assert.False(SystemOneFamilies.Handles(family, SystemOneTask.SpeechGate));
    }

    [Theory]
    [InlineData(JevKind.PersonFight, JevAsk.Answers, SystemOneTask.Stance)]
    [InlineData(JevKind.MonsterFight, JevAsk.Answers, SystemOneTask.Stance)]
    [InlineData(JevKind.Trade, JevAsk.Answers, SystemOneTask.Trade)]
    [InlineData(JevKind.BigMoment, JevAsk.Intent, SystemOneTask.Intent)]
    [InlineData(JevKind.SpeechGate, JevAsk.Gate, SystemOneTask.SpeechGate)]
    [InlineData(JevKind.BigMoment, JevAsk.Plain, SystemOneTask.NextJob)]
    [InlineData(JevKind.Decision, JevAsk.Plain, SystemOneTask.NextJob)]
    public void TaskOf_MapsEveryBrainCall(JevKind use, JevAsk ask, SystemOneTask expected) =>
        Assert.Equal(expected, SystemOneFamilies.TaskOf(use, ask));

    [Fact]
    public void Words_NameWhatTheModelDecidesAndWhatTheRulesKeep()
    {
        Assert.Equal("fight stances", SystemOneFamilies.HandledWords(SystemOneFamily.Von));
        Assert.Equal("next jobs, player intent, trade reads, the speech gate", SystemOneFamilies.RulesWords(SystemOneFamily.Von));
        Assert.Empty(SystemOneFamilies.RulesWords(SystemOneFamily.Jev));
    }
}
