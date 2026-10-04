using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

/// <summary>
/// One life routine every character shares: its steps, how often a character wants it,
/// and a switch that removes it from everyone. Held once in the file instead of copied
/// into each roster entry.
/// </summary>
public sealed class LifeRoutineDefinition
{
    public const int DefaultWeight = 1;

    /// <summary>False removes the routine and its choice from every character.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>How strongly a character wants this, against the other choices.</summary>
    [JsonPropertyName("weight")]
    public int Weight { get; set; } = DefaultWeight;

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("requiredPower")]
    public int? RequiredPower { get; set; }

    [JsonPropertyName("steps")]
    public List<SkillStepDefinition> Steps { get; set; } = [];
}
