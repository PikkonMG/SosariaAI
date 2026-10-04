using System;
using System.IO;
using Server.Json;
using SosariaAI.Configuration;
using Xunit;

namespace SosariaAI.Tests;

public class BrainFileTests
{
    [Fact]
    public void CreateDefault_MatchesShippedSettings()
    {
        var file = BrainFile.CreateDefault();

        Assert.Equal(BrainConfiguration.DefaultEnabled, file.Enabled);
        var frontier = file.Providers[BrainProviders.FrontierName];
        Assert.Equal(BrainConfiguration.DefaultBaseUrl, frontier.BaseUrl);
        Assert.Equal(BrainConfiguration.DefaultModel, frontier.Model);
        Assert.Equal(BrainConfiguration.DefaultApiKeyEnvironmentVariable, frontier.ApiKeyEnvironmentVariable);
        Assert.Equal(string.Empty, frontier.ApiKey);
        Assert.True(frontier.Paid);
        Assert.Equal(BrainConfiguration.DefaultTimeoutSeconds, frontier.TimeoutSeconds);
        Assert.Equal(BrainProviders.FrontierName, file.Route.Chat);
        Assert.Equal(BrainProviders.FrontierName, file.Route.Decision);
        Assert.Equal(10000, file.Budget.MaxPaidCallsPerDay);
        Assert.Equal(10000, file.Budget.MaxPaidCallsPerHour);
        Assert.False(file.Budget.CountLocalAsPaid);
        Assert.Equal(BrainProviders.DefaultLocalBaseUrl, file.Providers[BrainProviders.LocalName].BaseUrl);
        Assert.Equal(8, file.MaxConcurrentRequests);
        Assert.Equal(4, file.PerCharacterCooldownSeconds);
        Assert.Equal(2, file.BotToBotCooldownMinutes);
        Assert.Equal(4, file.BotToBotMaxExchanges);
        Assert.Equal(10, file.BotToBotPairRestMinutes);
        Assert.Equal(BrainConfiguration.DefaultHearRange, file.HearRange);
        Assert.Equal(BrainConfiguration.DefaultMaxReplyCharacters, file.MaxReplyCharacters);
        Assert.Equal(BrainConfiguration.DefaultTemperature, file.Temperature);
        Assert.Equal(BrainConfiguration.DefaultFailuresBeforePause, file.FailuresBeforePause);
        Assert.Equal(BrainConfiguration.DefaultPauseAfterFailuresSeconds, file.PauseAfterFailuresSeconds);
        Assert.Equal(2, file.DecideCooldownMinutes);
        Assert.Equal(3, file.NearbyPlayerNoticeMinutes);
        Assert.Equal(BrainConfiguration.DefaultJevInputTokensPerHour, file.JevInputTokensPerHour);
        Assert.Equal(0.5, file.DecisionMinConfidence);
        Assert.Equal(20, file.JevDecideCooldownSeconds);
        Assert.Equal(BrainConfiguration.JevScopeCombatAndBig, file.JevScope);
        Assert.Equal(32, file.JevMaxConcurrentRequests);
        Assert.Equal(BrainProviders.SystemOneApi, file.Providers[BrainProviders.VonName].Api);
        Assert.Equal(BrainProviders.DefaultVonBaseUrl, file.Providers[BrainProviders.VonName].BaseUrl);
        Assert.Equal(BrainProviders.DefaultVonModel, file.Providers[BrainProviders.VonName].Model);
        Assert.False(file.Providers[BrainProviders.VonName].Paid);
        Assert.Equal(BrainProviders.SystemOneApi, file.Providers[BrainProviders.LayaName].Api);
        Assert.Equal(BrainProviders.DefaultLayaBaseUrl, file.Providers[BrainProviders.LayaName].BaseUrl);
        Assert.Equal(BrainProviders.DefaultLayaModel, file.Providers[BrainProviders.LayaName].Model);
        Assert.False(file.Providers[BrainProviders.LayaName].Paid);
    }

    [Fact]
    public void LoadOrCreate_OldFileWithoutJevKeys_GetsTheDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-oldbrain-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ \"enabled\": true, \"route\": { \"chat\": \"frontier\", \"decision\": \"jev\" } }");

            var loaded = BrainFile.LoadOrCreate(path);

            Assert.Equal(BrainConfiguration.DefaultJevInputTokensPerHour, loaded.JevInputTokensPerHour);
            Assert.Equal(BrainConfiguration.DefaultDecisionMinConfidence, loaded.DecisionMinConfidence);
            Assert.Equal(BrainConfiguration.DefaultJevDecideCooldownSeconds, loaded.JevDecideCooldownSeconds);
            Assert.Equal(BrainConfiguration.JevScopeCombatAndBig, loaded.JevScope);
            Assert.Equal(BrainConfiguration.DefaultJevMaxConcurrentRequests, loaded.JevMaxConcurrentRequests);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Normalize_ReplacesOutOfRangeJevKeys()
    {
        var config = new BrainConfiguration
        {
            JevInputTokensPerHour = 0,
            DecisionMinConfidence = 1.5,
            JevDecideCooldownSeconds = -1,
            JevScope = "everything",
            JevMaxConcurrentRequests = 0,
            ChatHelpMaxSeconds = -1
        };

        config.Normalize();

        Assert.Equal(BrainConfiguration.DefaultChatHelpMaxSeconds, config.ChatHelpMaxSeconds);

        Assert.Equal(BrainConfiguration.JevScopeCombatAndBig, config.JevScope);
        Assert.Equal(BrainConfiguration.DefaultJevMaxConcurrentRequests, config.JevMaxConcurrentRequests);

        Assert.Equal(BrainConfiguration.DefaultJevInputTokensPerHour, config.JevInputTokensPerHour);
        Assert.Equal(BrainConfiguration.DefaultDecisionMinConfidence, config.DecisionMinConfidence);
        Assert.Equal(BrainConfiguration.DefaultJevDecideCooldownSeconds, config.JevDecideCooldownSeconds);
    }

    [Fact]
    public void LoadOrCreate_WritesDefaultsWhenMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-brain-{Guid.NewGuid():N}.json");

        try
        {
            var loaded = BrainFile.LoadOrCreate(path);
            Assert.Equal(BrainConfiguration.DefaultEnabled, loaded.Enabled);
            Assert.True(File.Exists(path));

            var roundTrip = JsonConfig.Deserialize<BrainConfiguration>(path);
            roundTrip.Normalize();
            Assert.Equal(BrainConfiguration.DefaultModel, roundTrip.Providers[BrainProviders.FrontierName].Model);
            Assert.Equal(string.Empty, roundTrip.Providers[BrainProviders.FrontierName].ApiKey);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadOrCreate_InvalidJson_UsesDefaultsInsteadOfThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sosariaai-badbrain-{Guid.NewGuid():N}.json");

        try
        {
            // A key typed without quotes, the most common hand-edit mistake.
            File.WriteAllText(path, "{ \"enabled\": true, \"providers\": { \"frontier\": { \"apiKey\": abc123 } } }");

            var loaded = BrainFile.LoadOrCreate(path);

            Assert.NotNull(loaded);
            Assert.Equal(BrainConfiguration.DefaultEnabled, loaded.Enabled);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Normalize_KeepsScopeAllInAnyCase()
    {
        var config = new BrainConfiguration { JevScope = " ALL " };

        config.Normalize();

        Assert.Equal(BrainConfiguration.JevScopeAll, config.JevScope);
    }
}
