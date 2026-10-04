using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class PersonaPartPool
{
    [JsonPropertyName("pool")]
    public string Pool { get; set; }

    [JsonPropertyName("parts")]
    public List<PersonaPart> Parts { get; set; } = [];
}
