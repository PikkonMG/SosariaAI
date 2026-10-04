using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class StatsDefinition
{
    [JsonPropertyName("str")]
    public int? Strength { get; set; }

    [JsonPropertyName("dex")]
    public int? Dexterity { get; set; }

    [JsonPropertyName("int")]
    public int? Intelligence { get; set; }
}
