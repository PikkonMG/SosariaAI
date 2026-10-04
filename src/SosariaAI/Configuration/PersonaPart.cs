using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class PersonaPart
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("jobs")]
    public List<string> Jobs { get; set; } = [];

    [JsonPropertyName("exclude")]
    public List<string> Exclude { get; set; } = [];

    /// <summary>Era bands this part fits: t2a, ml, modern. Empty fits every era.</summary>
    [JsonPropertyName("eras")]
    public List<string> Eras
    {
        get;
        set => field = PersonaEras.OrNull(value);
    }
}
