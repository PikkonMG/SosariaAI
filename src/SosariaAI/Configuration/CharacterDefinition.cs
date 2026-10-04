using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;
using SosariaAI.Combat;

namespace SosariaAI.Configuration;

public sealed class CharacterDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("persona")]
    public string Persona { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("returnAfterDeath")]
    public TimeSpan? ReturnAfterDeath { get; set; }

    [JsonPropertyName("spawn")]
    public Point3D Spawn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("build")]
    public BuildDefinition Build { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("routine")]
    public List<SkillStepDefinition> Routine { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("routines")]
    public Dictionary<string, List<SkillStepDefinition>> Routines { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("choices")]
    public List<ChoiceDefinition> Choices { get; set; }

    public IReadOnlyDictionary<string, List<SkillStepDefinition>> ResolvedRoutines()
    {
        if (Routines is { Count: > 0 })
        {
            return Routines;
        }

        return new Dictionary<string, List<SkillStepDefinition>>(StringComparer.OrdinalIgnoreCase)
        {
            [DecideChoice.DefaultRoutineId] = Routine ?? []
        };
    }

    /// <summary>
    /// True when any of this character's routines has a step of this kind. A character
    /// starts on its decide routine, so looking only at the running routine would miss
    /// the work it will choose next.
    /// </summary>
    public bool UsesSkill(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        foreach (var steps in ResolvedRoutines().Values)
        {
            if (steps == null)
            {
                continue;
            }

            for (var i = 0; i < steps.Count; i++)
            {
                if (string.Equals(steps[i]?.Skill, kind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
