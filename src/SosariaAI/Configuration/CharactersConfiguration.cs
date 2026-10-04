using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class CharactersConfiguration
{
    public const int DefaultMusingIntervalMinutes = 8;

    [JsonPropertyName("logActivity")]
    public bool LogActivity { get; set; }

    [JsonPropertyName("musingIntervalMinutes")]
    public int MusingIntervalMinutes { get; set; } = DefaultMusingIntervalMinutes;

    /// <summary>
    /// The Felucca public moongates that keep their guards, by town. The era had guards at
    /// Britain, Moonglow, and Jhelom and none at the rest, so reds travel by the others. An
    /// empty list takes every guard off the pads; list all eight towns to keep every pad guarded.
    /// </summary>
    [JsonPropertyName("feluccaGuardedMoongates")]
    public List<string> FeluccaGuardedMoongates { get; set; } = [.. Behaviour.MoongateGuards.DefaultGuardedGates];

    [JsonPropertyName("career")]
    public CareerSettings Career { get; set; }

    /// <summary>Order against Chaos scuffles on town streets. Null when the file has no block: the defaults hold.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("townScuffles")]
    public TownScuffleSettings TownScuffles { get; set; }

    /// <summary>
    /// The life routines every character shares, held once. An entry switched off is
    /// taken away from everyone; its weight sets how often a character wants it.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("lifeRoutines")]
    public Dictionary<string, LifeRoutineDefinition> LifeRoutines { get; set; }

    [JsonPropertyName("housePlots")]
    public List<HousePlot> HousePlots { get; set; }

    [JsonPropertyName("nav")]
    public NavSettings Nav { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("maps")]
    public Dictionary<string, MapToggle> Maps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("facets")]
    public Dictionary<string, FacetContent> Facets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        Maps = FacetNames.Copy(Maps);
        Facets = FacetNames.Copy(Facets);

        if (Facets == null)
        {
            return;
        }

        foreach (var pair in Facets)
        {
            pair.Value?.Normalize();
        }
    }
}
