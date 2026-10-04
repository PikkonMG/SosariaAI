using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Navigation;

public sealed class NavNode
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("z")]
    public int Z { get; set; }

    [JsonPropertyName("connects")]
    public List<string> Connects { get; set; } = [];

    [JsonPropertyName("gates")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NavGateLink> Gates { get; set; }

    [JsonPropertyName("arrivalRange")]
    public int ArrivalRange { get; set; }

    [JsonPropertyName("indoor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Indoor { get; set; }

    [JsonIgnore]
    public Point3D Location => new(X, Y, Z);

    /// <summary>Dense index for path search. Not saved in nav JSON.</summary>
    [JsonIgnore]
    public int Index { get; set; } = -1;
}
