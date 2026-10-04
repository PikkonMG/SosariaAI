using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Configuration;

public sealed class HousePlot
{
    [JsonPropertyName("map")]
    public string Map { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("z")]
    public int Z { get; set; }

    public Point3D Location => new(X, Y, Z);
}
