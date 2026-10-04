using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class ProviderDefinition
{
    /// <summary>
    /// The API shape this endpoint speaks. "openai" (or unset) is a chat-completions
    /// endpoint. "systemone" is the Jev-compatible endpoint, which answers typed
    /// questions instead of generating text; it serves decision calls only.
    /// </summary>
    [JsonPropertyName("api")]
    public string Api { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("apiKeyEnvironmentVariable")]
    public string ApiKeyEnvironmentVariable { get; set; }

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; }

    /// <summary>
    /// Whether calls to this provider count against the paid budget. Null means infer
    /// from the address: a loopback address is free, anything else is paid. Set false for
    /// a model you run yourself on another machine (a GPU box on your LAN), which then
    /// needs no key and is never capped. Set true to force counting.
    /// </summary>
    [JsonPropertyName("paid")]
    public bool? Paid { get; set; }

    /// <summary>
    /// Chat providers only. False (the default) asks the model not to think before it answers:
    /// a thinking model can spend seconds or minutes, and the whole reply budget, on a simple
    /// line. True lets it think, at <see cref="ReasoningEffort"/> when that is set.
    /// </summary>
    [JsonPropertyName("thinking")]
    public bool Thinking { get; set; }

    /// <summary>
    /// Chat providers only, and only with <see cref="Thinking"/> true: the thinking level sent,
    /// such as "low" or "high". Null sends no level, so the model uses its own default.
    /// </summary>
    [JsonPropertyName("reasoningEffort")]
    public string ReasoningEffort { get; set; }

    /// <summary>
    /// System One providers only: the minimum answer confidence (0-1) a decision needs
    /// before it is used. Below this the needs brain picks instead. Null accepts every
    /// answer; the power gate still vetoes routines the character is too weak for.
    /// </summary>
    [JsonPropertyName("minConfidence")]
    public double? MinConfidence { get; set; }

    /// <summary>
    /// System One providers only: which model this is, "jev", "von", or "laya". It decides
    /// which tasks the model is trusted with. Null detects it from the address, the model,
    /// and the provider name; an unknown model gets the careful "von" trust.
    /// </summary>
    [JsonPropertyName("family")]
    public string Family { get; set; }
}
