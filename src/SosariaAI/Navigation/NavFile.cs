using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SosariaAI.Navigation;

public sealed class NavFile
{
    public const int CurrentFormat = 2;

    [JsonPropertyName("format")]
    public int Format { get; set; }

    [JsonPropertyName("facet")]
    public string Facet { get; set; }

    [JsonPropertyName("nodes")]
    public List<NavNode> Nodes { get; set; } = [];
}
