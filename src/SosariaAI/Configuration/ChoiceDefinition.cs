using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class ChoiceDefinition
{
    [JsonPropertyName("routine")]
    public string Routine { get; set; }

    [JsonPropertyName("weight")]
    public int Weight { get; set; } = 1;

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("requiredPower")]
    public int? RequiredPower { get; set; }
}
