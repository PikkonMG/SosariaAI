using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class BuildDefinition
{
    [JsonPropertyName("preset")]
    public string Preset { get; set; }

    [JsonPropertyName("style")]
    public string Style { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("veteran")]
    public bool Veteran { get; set; }

    [JsonPropertyName("skills")]
    public Dictionary<string, double> Skills { get; set; }

    [JsonPropertyName("stats")]
    public StatsDefinition Stats { get; set; }

    [JsonPropertyName("kit")]
    public List<string> Kit { get; set; }

    [JsonPropertyName("canHeal")]
    public bool? CanHeal { get; set; }

    [JsonPropertyName("healInterval")]
    public double? HealInterval { get; set; }
}
