using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class NavSettings
{
    public const bool DefaultRebuildOnBoot = false;
    public const int DefaultGateCost = 400;

    [JsonPropertyName("rebuildOnBoot")]
    public bool RebuildOnBoot { get; set; } = DefaultRebuildOnBoot;

    [JsonPropertyName("gateCost")]
    public int GateCost { get; set; } = DefaultGateCost;
}
