using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class BrainConfiguration
{
    public const string DefaultBaseUrl = "https://openrouter.ai/api/v1";
    public const string DefaultModel = "deepseek/deepseek-chat";
    public const string DefaultApiKeyEnvironmentVariable = "SOSARIAAI_API_KEY";
    public const int DefaultTimeoutSeconds = 20;
    public const int DefaultMaxConcurrentRequests = 8;
    public const int DefaultPerCharacterCooldownSeconds = 4;
    public const int DefaultBotToBotCooldownMinutes = 2;
    public const int DefaultBotToBotMaxExchanges = 4;
    public const int DefaultBotToBotPairRestMinutes = 10;
    public const int DefaultHearRange = 6;
    public const int DefaultMaxReplyCharacters = 160;
    public const double DefaultTemperature = 0.8;
    public const int DefaultFailuresBeforePause = 3;
    public const int DefaultPauseAfterFailuresSeconds = 60;
    public const int DefaultAttentionWindowSeconds = 30;
    public const int DefaultDecideCooldownMinutes = 2;
    public const int DefaultNearbyPlayerNoticeMinutes = 3;
    public const bool DefaultSpeechGate = true;
    public const double DefaultSpeechGateThreshold = 0.5;

    /// <summary>The hourly spend the default Jev token cap is sized for, in dollars.</summary>
    public const double DefaultJevDollarsPerHour = 0.10;

    /// <summary>
    /// Input tokens every Jev call together may use in one clock hour: about
    /// <see cref="DefaultJevDollarsPerHour"/> at the documented price.
    /// </summary>
    public const long DefaultJevInputTokensPerHour =
        (long)(DefaultJevDollarsPerHour / BrainProviders.JevDollarsPerMillionInputTokens * BrainProviders.TokensPerMillion);

    /// <summary>A decision answer below this confidence is not used; the scorer picks instead.</summary>
    public const double DefaultDecisionMinConfidence = 0.5;

    /// <summary>
    /// After one Jev next-job ask, a character's next picks within this many seconds go to the
    /// scorer, so a skill that ends at once cannot ask every tick.
    /// </summary>
    public const int DefaultJevDecideCooldownSeconds = 20;

    /// <summary>
    /// A chat provider that answers slower than this on average does not help with decisions a
    /// System One model leaves to the rules. Zero turns that help off.
    /// </summary>
    public const int DefaultChatHelpMaxSeconds = 5;

    /// <summary>Jev decides fights that matter and big moments; the scorer keeps routine jobs.</summary>
    public const string JevScopeCombatAndBig = "combat-and-big";

    /// <summary>Jev picks every next job, as it did before the scope switch.</summary>
    public const string JevScopeAll = "all";

    public const string DefaultJevScope = JevScopeCombatAndBig;

    /// <summary>
    /// Jev calls out at once, apart from the chat providers' limit: a Jev answer takes about a
    /// tenth of a second, so a short queue behind this many beats falling back to the scorer.
    /// </summary>
    public const int DefaultJevMaxConcurrentRequests = 32;

    /// <summary>
    /// A few copies a minute get a persona of their own from the chat model, once each, so a
    /// full population fills in over hours without a burst of calls.
    /// </summary>
    public const int DefaultPersonaWritesPerMinute = 3;

    /// <summary>The persona writer is on unless the operator wants every copy on the part library alone.</summary>
    public const bool DefaultPersonaWriter = true;

    /// <summary>
    /// On unless the operator turns it off: the plugin is built around the models. A
    /// provider with no key still switches itself off at boot with a warning.
    /// </summary>
    public const bool DefaultEnabled = true;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = DefaultEnabled;

    [JsonPropertyName("providers")]
    public Dictionary<string, ProviderDefinition> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("route")]
    public BrainRoute Route { get; set; } = new();

    [JsonPropertyName("budget")]
    public BrainBudgetSettings Budget { get; set; } = new();

    [JsonPropertyName("maxConcurrentRequests")]
    public int MaxConcurrentRequests { get; set; } = DefaultMaxConcurrentRequests;

    [JsonPropertyName("perCharacterCooldownSeconds")]
    public int PerCharacterCooldownSeconds { get; set; } = DefaultPerCharacterCooldownSeconds;

    [JsonPropertyName("botToBotCooldownMinutes")]
    public int BotToBotCooldownMinutes { get; set; } = DefaultBotToBotCooldownMinutes;

    [JsonPropertyName("botToBotMaxExchanges")]
    public int BotToBotMaxExchanges { get; set; } = DefaultBotToBotMaxExchanges;

    [JsonPropertyName("botToBotPairRestMinutes")]
    public int BotToBotPairRestMinutes { get; set; } = DefaultBotToBotPairRestMinutes;

    [JsonPropertyName("hearRange")]
    public int HearRange { get; set; } = DefaultHearRange;

    [JsonPropertyName("maxReplyCharacters")]
    public int MaxReplyCharacters { get; set; } = DefaultMaxReplyCharacters;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = DefaultTemperature;

    [JsonPropertyName("failuresBeforePause")]
    public int FailuresBeforePause { get; set; } = DefaultFailuresBeforePause;

    [JsonPropertyName("pauseAfterFailuresSeconds")]
    public int PauseAfterFailuresSeconds { get; set; } = DefaultPauseAfterFailuresSeconds;

    [JsonPropertyName("attentionWindowSeconds")]
    public int AttentionWindowSeconds { get; set; } = DefaultAttentionWindowSeconds;

    [JsonPropertyName("decideCooldownMinutes")]
    public int DecideCooldownMinutes { get; set; } = DefaultDecideCooldownMinutes;

    [JsonPropertyName("nearbyPlayerNoticeMinutes")]
    public int NearbyPlayerNoticeMinutes { get; set; } = DefaultNearbyPlayerNoticeMinutes;

    /// <summary>
    /// When the decision route is a System One provider, character-to-character speech asks a
    /// cheap noul before paying for a generated reply. Player speech is never gated.
    /// </summary>
    [JsonPropertyName("speechGate")]
    public bool SpeechGate { get; set; } = DefaultSpeechGate;

    /// <summary>The noul probability at or above which a spoken line earns a reply.</summary>
    [JsonPropertyName("speechGateThreshold")]
    public double SpeechGateThreshold { get; set; } = DefaultSpeechGateThreshold;

    /// <summary>Personal personas the chat model writes each minute. 0 turns persona writing off.</summary>
    [JsonPropertyName("personaWritesPerMinute")]
    public int PersonaWritesPerMinute { get; set; } = DefaultPersonaWritesPerMinute;

    /// <summary>
    /// False turns the persona writer off: no new writes and no saved personas, so every copy
    /// uses only the part library. A file without the key keeps it on.
    /// </summary>
    [JsonPropertyName("personaWriter")]
    public bool PersonaWriter { get; set; } = DefaultPersonaWriter;

    /// <summary>Input tokens every Jev call together may use in one clock hour; past 80% only the calls that matter most go out.</summary>
    [JsonPropertyName("jevInputTokensPerHour")]
    public long JevInputTokensPerHour { get; set; } = DefaultJevInputTokensPerHour;

    /// <summary>
    /// The lowest Jev decision confidence (0-1) that is used. A provider's own higher
    /// <c>minConfidence</c> wins.
    /// </summary>
    [JsonPropertyName("decisionMinConfidence")]
    public double DecisionMinConfidence { get; set; } = DefaultDecisionMinConfidence;

    /// <summary>Seconds after a Jev next-job ask in which that character's picks go to the scorer.</summary>
    [JsonPropertyName("jevDecideCooldownSeconds")]
    public int JevDecideCooldownSeconds { get; set; } = DefaultJevDecideCooldownSeconds;

    /// <summary>
    /// When the decision route's System One model is not trusted with an event decision, the chat
    /// provider decides it instead, while its average reply time stays at or under this many
    /// seconds. Zero turns that help off, so the rules decide.
    /// </summary>
    [JsonPropertyName("chatHelpMaxSeconds")]
    public int ChatHelpMaxSeconds { get; set; } = DefaultChatHelpMaxSeconds;

    /// <summary>"combat-and-big" (the default): Jev decides fights that matter and big moments. "all": every next job too.</summary>
    [JsonPropertyName("jevScope")]
    public string JevScope { get; set; } = DefaultJevScope;

    /// <summary>Jev calls out at once, counted apart from maxConcurrentRequests.</summary>
    [JsonPropertyName("jevMaxConcurrentRequests")]
    public int JevMaxConcurrentRequests { get; set; } = DefaultJevMaxConcurrentRequests;

    public void Normalize()
    {
        Providers = FacetNames.Copy(Providers);
        Route ??= new BrainRoute();
        Route.DecisionUses = FacetNames.Copy(Route.DecisionUses);

        if (string.IsNullOrWhiteSpace(Route.Chat))
        {
            Route.Chat = BrainProviders.FrontierName;
        }

        if (string.IsNullOrWhiteSpace(Route.Decision))
        {
            Route.Decision = BrainProviders.FrontierName;
        }

        if (SpeechGateThreshold is < 0 or > 1)
        {
            SpeechGateThreshold = DefaultSpeechGateThreshold;
        }

        if (DecisionMinConfidence is < 0 or > 1)
        {
            DecisionMinConfidence = DefaultDecisionMinConfidence;
        }

        if (JevInputTokensPerHour <= 0)
        {
            JevInputTokensPerHour = DefaultJevInputTokensPerHour;
        }

        if (JevDecideCooldownSeconds <= 0)
        {
            JevDecideCooldownSeconds = DefaultJevDecideCooldownSeconds;
        }

        if (ChatHelpMaxSeconds < 0)
        {
            ChatHelpMaxSeconds = DefaultChatHelpMaxSeconds;
        }

        JevScope = string.Equals(JevScope?.Trim(), JevScopeAll, StringComparison.OrdinalIgnoreCase)
            ? JevScopeAll
            : JevScopeCombatAndBig;

        if (JevMaxConcurrentRequests <= 0)
        {
            JevMaxConcurrentRequests = DefaultJevMaxConcurrentRequests;
        }

        Budget ??= new BrainBudgetSettings();

        if (Budget.MaxPaidCallsPerDay <= 0)
        {
            Budget.MaxPaidCallsPerDay = CallBudget.DefaultMaxPaidCallsPerDay;
        }

        if (Budget.MaxPaidCallsPerHour <= 0)
        {
            Budget.MaxPaidCallsPerHour = CallBudget.DefaultMaxPaidCallsPerHour;
        }

        if (Providers == null)
        {
            return;
        }

        foreach (var pair in Providers)
        {
            if (pair.Value != null && pair.Value.TimeoutSeconds <= 0)
            {
                pair.Value.TimeoutSeconds = DefaultTimeoutSeconds;
            }
        }
    }
}
