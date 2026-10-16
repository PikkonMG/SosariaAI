using System.Text.Json.Serialization;
using Server;
using SosariaAI.Behaviour;

namespace SosariaAI.Configuration;

/// <summary>
/// A plot a character may build its house on. One house stands on a plot. A vendor plot goes
/// to a crafter, whose house keeps its shop there. The house stands at least <see cref="Clear"/>
/// and at most <see cref="Reach"/> tiles from the plot's middle, so a plot beside a landmark
/// keeps the landmark free.
/// </summary>
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

    [JsonPropertyName("vendor")]
    public bool Vendor { get; set; }

    [JsonPropertyName("clear")]
    public int Clear { get; set; }

    [JsonPropertyName("reach")]
    public int Reach { get; set; } = HouseRules.PlaceSearchRadius;

    public Point3D Location => new(X, Y, Z);
}
