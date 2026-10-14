using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

public sealed class MapToggle
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// Missing from the file: reds on (<see cref="CharactersFile.DefaultOpenPvpPkEnabled"/>). Reds
    /// live only on a map with open PvP (<see cref="FacetMaps.OpenPvp(string)"/>), whatever this says.
    /// </summary>
    [JsonPropertyName("pkEnabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PkEnabled { get; set; }

    /// <summary>Missing from the file: one in ten of <see cref="Count"/>. A value here always wins.</summary>
    [JsonPropertyName("pkCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PkCount { get; set; }

    /// <summary>
    /// The share of the reds, 0 to 100, that ride in gangs of two to four; the rest hunt
    /// alone. Missing from the file: <see cref="Combat.PkGangRules.DefaultGangPercent"/>.
    /// </summary>
    [JsonPropertyName("pkGangPercent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PkGangPercent { get; set; }

    /// <summary>
    /// false closes PvP on a map whose rules allow it: no reds, duels, Order against Chaos
    /// fights or lifts there. Missing or true: the map's own rules decide
    /// (<see cref="FacetMaps.OpenPvp(Server.Map)"/>); true cannot open a map with Trammel's
    /// rules, because the engine refuses the blow there.
    /// </summary>
    [JsonPropertyName("openPvp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? OpenPvp { get; set; }

    /// <summary>
    /// Where this map's reds live, bank and rest: a region name of the map, or a rectangle
    /// (x, y, width, height). Missing: Buccaneer's Den on a stock map; on another map a place the
    /// nav catalog offers (<see cref="Spawning.WorldAnchors.PickHaven"/>).
    /// </summary>
    [JsonPropertyName("redHaven")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AreaDefinition RedHaven { get; set; }
}
