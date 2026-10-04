using System.Collections.Generic;
using System.Text.Json.Serialization;
using Server;

namespace SosariaAI.Navigation;

public sealed class Destination
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("z")]
    public int Z { get; set; }

    [JsonPropertyName("node")]
    public string Node { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("difficulty")]
    public int? Difficulty { get; set; }

    [JsonIgnore]
    public Point3D Location => new(X, Y, Z);

    /// <summary>Where a trip to the place ends: the place's own spot.</summary>
    [JsonIgnore]
    public Point3D Arrival => Location;

    /// <summary>
    /// The point a traveller should arrive at. A seed's own z is unreliable —
    /// tavern keepers seed z = 0 under an upstairs common room and shop signs seed
    /// z = 20 on a roof. The bound node sits on the floor the approach actually
    /// uses, so its z is the arrival floor when the node resolves.
    /// </summary>
    public Point3D ApproachPoint(NavGraph graph)
    {
        var arrival = Location;

        if (arrival == Point3D.Zero)
        {
            return arrival;
        }

        return graph?.TryGetNode(Node, out var node) == true
            ? new Point3D(arrival.X, arrival.Y, node.Location.Z)
            : arrival;
    }

    [JsonIgnore]
    public DestinationKind ParsedKind =>
        Kind switch
        {
            "Bank" => DestinationKind.Bank,
            "Vendor" => DestinationKind.Vendor,
            "Dungeon" => DestinationKind.Dungeon,
            "Hunt" => DestinationKind.Hunt,
            "Resource" => DestinationKind.Resource,
            "Healer" => DestinationKind.Healer,
            "Shrine" => DestinationKind.Shrine,
            _ => DestinationKind.Bank
        };
}
