using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class BrainRoute
{
    [JsonPropertyName("chat")]
    public string Chat { get; set; } = BrainProviders.FrontierName;

    [JsonPropertyName("decision")]
    public string Decision { get; set; } = BrainProviders.FrontierName;

    /// <summary>Optional provider per typed decision use. Missing uses follow decision.</summary>
    [JsonPropertyName("decisionUses")]
    public Dictionary<string, string> DecisionUses { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);
}
