using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class MapToggle
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>Missing from the file: the facet's default (reds on in Felucca only).</summary>
    [JsonPropertyName("pkEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PkEnabled { get; set; }

    /// <summary>Missing from the file: one in ten of <see cref="Count"/>. A value here always wins.</summary>
    [JsonPropertyName("pkCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PkCount { get; set; }

    /// <summary>
    /// The share of the reds, 0 to 100, that ride in gangs of two to four; the rest hunt
    /// alone. Missing from the file: <see cref="Combat.PkGangRules.DefaultGangPercent"/>.
    /// </summary>
    [JsonPropertyName("pkGangPercent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PkGangPercent { get; set; }
}
