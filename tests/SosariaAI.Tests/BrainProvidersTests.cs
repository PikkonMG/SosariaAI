using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class BrainProvidersTests
{
    [Fact]
    public void ProviderNameForKind_UsesRouteMap()
    {
        var config = new BrainConfiguration
        {
            Route = new BrainRoute { Chat = BrainProviders.LocalName, Decision = BrainProviders.FrontierName }
        };

        Assert.Equal(BrainProviders.LocalName, BrainProviders.ProviderNameForKind(config, BrainProviders.ChatKind));
        Assert.Equal(BrainProviders.FrontierName, BrainProviders.ProviderNameForKind(config, BrainProviders.DecisionKind));
    }

    [Fact]
    public void ProviderNameForKind_UsesDecisionOverrideAndFallsBack()
    {
        var config = BrainFile.CreateDefault();
        config.Route.Decision = BrainProviders.JevName;
        config.Route.DecisionUses["personFight"] = BrainProviders.VonName;
        config.Route.DecisionUses["speechGate"] = BrainProviders.LayaName;
        config.Normalize();

        Assert.Equal(BrainProviders.VonName,
            BrainProviders.ProviderNameForKind(config, BrainProviders.DecisionKind, "personFight"));
        Assert.Equal(BrainProviders.LayaName,
            BrainProviders.ProviderNameForKind(config, BrainProviders.DecisionKind, "SPEECHGATE"));
        Assert.Equal(BrainProviders.JevName,
            BrainProviders.ProviderNameForKind(config, BrainProviders.DecisionKind, "trade"));
        Assert.Equal(BrainProviders.FrontierName,
            BrainProviders.ProviderNameForKind(config, BrainProviders.ChatKind, "personFight"));
        Assert.Contains(BrainProviders.VonName, BrainProviders.UsedNames(config));
        Assert.Contains(BrainProviders.LayaName, BrainProviders.UsedNames(config));
    }

    [Fact]
    public void Find_UnknownName_ReturnsNull()
    {
        var config = BrainFile.CreateDefault();
        config.Normalize();
        Assert.Null(BrainProviders.Find(config, "missing"));
        Assert.Equal(BrainProviders.FrontierName, BrainProviders.ProviderNameForKind(config, BrainProviders.ChatKind));
    }

    [Fact]
    public void Normalize_UnknownRouteName_IsLeftForCallerToDisable()
    {
        var config = BrainFile.CreateDefault();
        config.Route.Chat = "ghost";
        config.Normalize();
        Assert.Equal("ghost", config.Route.Chat);
        Assert.Null(BrainProviders.Find(config, "ghost"));
    }

    [Fact]
    public void IsUsable_Localhost_AllowsMissingKey()
    {
        var local = new ProviderDefinition
        {
            BaseUrl = BrainProviders.DefaultLocalBaseUrl,
            Model = BrainProviders.DefaultLocalModel,
            ApiKey = null
        };

        Assert.True(BrainProviders.IsLocalhost(local.BaseUrl));
        Assert.True(BrainProviders.IsUsable(local, apiKey: null));
        Assert.False(BrainProviders.CountsAsPaid(local, countLocalAsPaid: false));
        Assert.True(BrainProviders.CountsAsPaid(local, countLocalAsPaid: true));
    }

    [Fact]
    public void PaidFlag_LanProviderDeclaredFree_IsUsableAndUncapped()
    {
        var lanGpu = new ProviderDefinition
        {
            BaseUrl = "http://192.168.1.50:11434/v1",
            Model = "gemma4:12b",
            Paid = false
        };

        Assert.False(BrainProviders.IsLocalhost(lanGpu.BaseUrl));
        Assert.True(BrainProviders.IsUsable(lanGpu, apiKey: null));
        Assert.False(BrainProviders.CountsAsPaid(lanGpu, countLocalAsPaid: false));
        Assert.True(BrainProviders.CountsAsPaid(lanGpu, countLocalAsPaid: true));
    }

    [Fact]
    public void PaidFlag_LanProviderWithoutFlag_CountsAsPaid()
    {
        var lan = new ProviderDefinition
        {
            BaseUrl = "http://192.168.1.50:11434/v1",
            Model = "gemma4:12b",
            ApiKey = "ollama"
        };

        Assert.True(BrainProviders.CountsAsPaid(lan, countLocalAsPaid: false));
    }

    [Fact]
    public void PaidFlag_TrueForcesCountingEvenOnLoopback()
    {
        var forced = new ProviderDefinition
        {
            BaseUrl = "http://127.0.0.1:11434/v1",
            Model = "llama3.1:8b",
            Paid = true
        };

        Assert.True(BrainProviders.CountsAsPaid(forced, countLocalAsPaid: false));
    }

    [Fact]
    public void IsUsable_Remote_RequiresKey()
    {
        var remote = new ProviderDefinition
        {
            BaseUrl = BrainConfiguration.DefaultBaseUrl,
            Model = BrainConfiguration.DefaultModel,
            ApiKey = null
        };

        Assert.False(BrainProviders.IsLocalhost(remote.BaseUrl));
        Assert.False(BrainProviders.IsUsable(remote, apiKey: null));
        Assert.True(BrainProviders.IsUsable(remote, apiKey: "secret"));
        Assert.True(BrainProviders.CountsAsPaid(remote, countLocalAsPaid: false));
    }

    [Fact]
    public void IsSystemOne_ReadsTheApiField()
    {
        Assert.True(
            BrainProviders.IsSystemOne(new ProviderDefinition { Api = "systemone" })
        );
        Assert.True(
            BrainProviders.IsSystemOne(new ProviderDefinition { Api = "SystemOne" })
        );
        Assert.False(BrainProviders.IsSystemOne(new ProviderDefinition()));
        Assert.False(
            BrainProviders.IsSystemOne(new ProviderDefinition { Api = "openai" })
        );
        Assert.False(BrainProviders.IsSystemOne(null));
    }

    [Fact]
    public void ResolveApiKey_EnvironmentBeatsFile()
    {
        var provider = new ProviderDefinition
        {
            ApiKeyEnvironmentVariable = "SOSARIAAI_API_KEY",
            ApiKey = "file-key"
        };

        Assert.Equal("env-key", BrainProviders.ResolveApiKey(provider, name => name == "SOSARIAAI_API_KEY" ? "env-key" : null));
        Assert.Equal("file-key", BrainProviders.ResolveApiKey(provider, _ => null));
    }

    [Fact]
    public void ResolveApiKey_NeitherMeansOff()
    {
        var provider = new ProviderDefinition
        {
            ApiKeyEnvironmentVariable = "SOSARIAAI_API_KEY",
            ApiKey = null
        };

        Assert.Null(BrainProviders.ResolveApiKey(provider, _ => "  "));
    }
}
