using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Configuration;

public sealed class PartyDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("leader")]
    public string Leader { get; set; }

    [JsonPropertyName("members")]
    public List<string> Members { get; set; } = [];

    [JsonPropertyName("meetAt")]
    public Point3D MeetAt { get; set; }
}
