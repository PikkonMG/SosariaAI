using System.Text.Json.Serialization;

namespace SosariaAI.Configuration;

/// <summary>
/// Small Order against Chaos scuffles on town streets (<see cref="Behaviour.TownScuffles"/>).
/// A key missing from characters.json keeps its default; the rules clamp a value out of range.
/// </summary>
public sealed class TownScuffleSettings
{
    public const bool DefaultEnabled = true;
    public const int DefaultTownGapMinMinutes = 20;
    public const int DefaultTownGapMaxMinutes = 45;
    public const int DefaultShardGapMinutes = 5;
    public const int DefaultMaxActive = 3;
    public const int DefaultMaxSide = 3;
    public const int DefaultMaxFighters = 6;
    public const int DefaultBankClearTiles = 20;
    public const int DefaultPeaceClearTiles = 12;
    public const int DefaultTimeLimitSeconds = 150;
    public const int DefaultFighterRestMinutes = 30;
    public const int DefaultPairRestMinutes = 120;
    public const int DefaultCallTiles = 150;
    public const int DefaultGatherMinutes = 4;

    /// <summary>The settings used when characters.json has no townScuffles block.</summary>
    public static readonly TownScuffleSettings Defaults = new();

    /// <summary>False turns town scuffles off; Order and Chaos then only trade words in town.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = DefaultEnabled;

    /// <summary>The least time, in minutes, from one scuffle in a town to the next there.</summary>
    [JsonPropertyName("townGapMinMinutes")]
    public int TownGapMinMinutes { get; set; } = DefaultTownGapMinMinutes;

    /// <summary>The most time, in minutes, from one scuffle in a town to the next there.</summary>
    [JsonPropertyName("townGapMaxMinutes")]
    public int TownGapMaxMinutes { get; set; } = DefaultTownGapMaxMinutes;

    /// <summary>The least time, in minutes, between two scuffle starts anywhere on the shard.</summary>
    [JsonPropertyName("shardGapMinutes")]
    public int ShardGapMinutes { get; set; } = DefaultShardGapMinutes;

    /// <summary>Scuffles going on at one time, shard wide.</summary>
    [JsonPropertyName("maxActive")]
    public int MaxActive { get; set; } = DefaultMaxActive;

    /// <summary>The most fighters of one side in one scuffle.</summary>
    [JsonPropertyName("maxSide")]
    public int MaxSide { get; set; } = DefaultMaxSide;

    /// <summary>The most fighters of both sides in one scuffle.</summary>
    [JsonPropertyName("maxFighters")]
    public int MaxFighters { get; set; } = DefaultMaxFighters;

    /// <summary>No scuffle starts or goes on this close, in tiles, to a bank.</summary>
    [JsonPropertyName("bankClearTiles")]
    public int BankClearTiles { get; set; } = DefaultBankClearTiles;

    /// <summary>No scuffle starts or goes on this close, in tiles, to a healer, a shrine or a public moongate.</summary>
    [JsonPropertyName("peaceClearTiles")]
    public int PeaceClearTiles { get; set; } = DefaultPeaceClearTiles;

    /// <summary>A scuffle still going after this many seconds ends; the side more hurt gives way.</summary>
    [JsonPropertyName("timeLimitSeconds")]
    public int TimeLimitSeconds { get; set; } = DefaultTimeLimitSeconds;

    /// <summary>A fighter rests this many minutes before its next town scuffle.</summary>
    [JsonPropertyName("fighterRestMinutes")]
    public int FighterRestMinutes { get; set; } = DefaultFighterRestMinutes;

    /// <summary>The same Order and Chaos pair scuffles again only after this many minutes.</summary>
    [JsonPropertyName("pairRestMinutes")]
    public int PairRestMinutes { get; set; } = DefaultPairRestMinutes;

    /// <summary>Faction-mates this many tiles from a town's street spot hear a call to scuffle there.</summary>
    [JsonPropertyName("callTiles")]
    public int CallTiles { get; set; } = DefaultCallTiles;

    /// <summary>A called scuffle waits this many minutes for its fighters, then starts with those who came.</summary>
    [JsonPropertyName("gatherMinutes")]
    public int GatherMinutes { get; set; } = DefaultGatherMinutes;
}
