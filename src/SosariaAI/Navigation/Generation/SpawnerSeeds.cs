using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SosariaAI.Admin;

namespace SosariaAI.Navigation.Generation;

/// <summary>
/// One spawner of the server's spawn files: where it stands, how far its creatures roam, the
/// names it spawns, how many it keeps up at once (zero when the file does not say), and each
/// entry's own cap and roll weight.
/// </summary>
public readonly record struct SpawnerSeed(
    string Map,
    int X,
    int Y,
    int Z,
    int HomeRange,
    IReadOnlyList<string> CreatureNames,
    int Count = 0,
    IReadOnlyList<SpawnEntry> Entries = null
);

/// <summary>One entry of a spawner: the name, the most of it up at once, and its roll weight.</summary>
public readonly record struct SpawnEntry(string Name, int MaxCount, int Probability);

/// <summary>How many of one creature a ground holds at once, on the average.</summary>
public readonly record struct SpawnCount(string Creature, double Count);

public static class SpawnerSeeds
{
    /// <summary>An entry without a roll weight rolls as often as the engine's default one.</summary>
    public const int DefaultProbability = 100;

    /// <summary>A spawner that names no count keeps one of each entry up.</summary>
    public const int DefaultCount = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly string[] VendorNameTokens =
    [
        "Vendor",
        "Banker",
        "Healer",
        "InnKeeper",
        "Mage",
        "Blacksmith",
        "Tinker",
        "Tailor",
        "Provisioner",
        "Alchemist",
        "Herbalist",
        "Baker",
        "Butcher",
        "Barkeep",
        "AnimalTrainer",
        "Shipwright",
        "Architect",
        "Guildmaster",
        "HairStylist",
        "CustomHairstylist",
        "Bowyer",
        "Mapmaker",
        "Jeweler",
        "Scribe",
        "Tanner",
        "Armorer",
        "Armourer",
        "TavernKeeper",
        "Painter",
        "Beekeeper",
        "Cartographer",
        "Fletcher"
    ];

    public static IReadOnlyList<SpawnerSeed> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var records = JsonSerializer.Deserialize<List<SpawnerRecord>>(json, JsonOptions);

        if (records == null || records.Count == 0)
        {
            return [];
        }

        var seeds = new List<SpawnerSeed>(records.Count);

        for (var i = 0; i < records.Count; i++)
        {
            if (TryCreateSeed(records[i], out var seed))
            {
                seeds.Add(seed);
            }
        }

        return seeds;
    }

    public static IReadOnlyList<SpawnerSeed> LoadDirectory(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        var files = Directory.GetFiles(root, WorldSetupRules.SpawnFilePattern, SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);

        var seeds = new List<SpawnerSeed>();

        for (var i = 0; i < files.Length; i++)
        {
            seeds.AddRange(Parse(File.ReadAllText(files[i])));
        }

        return seeds;
    }

    public static bool IsVendorName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        for (var i = 0; i < VendorNameTokens.Length; i++)
        {
            if (name.Contains(VendorNameTokens[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryCreateSeed(SpawnerRecord record, out SpawnerSeed seed)
    {
        seed = default;

        if (record?.Location == null || record.Location.Length < WorldDataSeeds.LocationLength)
        {
            return false;
        }

        seed = new SpawnerSeed(
            record.Map ?? string.Empty,
            record.Location[WorldDataSeeds.XIndex],
            record.Location[WorldDataSeeds.YIndex],
            record.Location[WorldDataSeeds.ZIndex],
            record.HomeRange,
            CollectNames(record.Entries),
            record.Count,
            CollectEntries(record.Entries)
        );
        return true;
    }

    /// <summary>
    /// What a full spawner keeps up of each entry, on the average: its count shared out by the
    /// entries' roll weights, each capped at the entry's own most, and the share a capped entry
    /// cannot take goes to the others, as the engine rolls again among the entries still short.
    /// A mixed spawner of ten that rolls among a dozen creatures keeps less than one of each; a
    /// Destard spawner of three with one dragon and two drakes keeps exactly that. An entry
    /// with no cap is capped by the spawner's count.
    /// </summary>
    public static List<SpawnCount> Standing(SpawnerSeed spawner)
    {
        var entries = spawner.Entries;
        var standing = new List<SpawnCount>(entries?.Count ?? 0);

        if (entries == null || entries.Count == 0)
        {
            return standing;
        }

        var count = spawner.Count > 0 ? spawner.Count : DefaultCount;
        var kept = new double[entries.Count];
        var full = new bool[entries.Count];
        var left = (double)count;

        // Each round shares what is left among the entries still short; one that fills drops out.
        for (var round = 0; round < entries.Count && left > 0; round++)
        {
            var weights = 0.0;

            for (var i = 0; i < entries.Count; i++)
            {
                weights += full[i] ? 0 : WeightOf(entries[i]);
            }

            var share = left;
            var filled = false;

            for (var i = 0; i < entries.Count; i++)
            {
                if (full[i])
                {
                    continue;
                }

                var cap = CapOf(entries[i], count);
                var add = share * WeightOf(entries[i]) / weights;

                if (kept[i] + add >= cap)
                {
                    left -= cap - kept[i];
                    kept[i] = cap;
                    full[i] = true;
                    filled = true;
                }
            }

            if (filled)
            {
                continue;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                kept[i] += full[i] ? 0 : share * WeightOf(entries[i]) / weights;
            }

            left = 0;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            standing.Add(new SpawnCount(entries[i].Name, kept[i]));
        }

        return standing;
    }

    private static int CapOf(SpawnEntry entry, int count) => entry.MaxCount > 0 ? Math.Min(entry.MaxCount, count) : count;

    private static double WeightOf(SpawnEntry entry) => entry.Probability > 0 ? entry.Probability : DefaultProbability;

    private static IReadOnlyList<SpawnEntry> CollectEntries(List<SpawnerEntryRecord> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return [];
        }

        var collected = new List<SpawnEntry>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            if (!string.IsNullOrWhiteSpace(entry?.Name))
            {
                collected.Add(new SpawnEntry(entry.Name, entry.MaxCount, entry.Probability));
            }
        }

        return collected;
    }

    private static IReadOnlyList<string> CollectNames(List<SpawnerEntryRecord> entries)
    {
        if (entries == null || entries.Count == 0)
        {
            return [];
        }

        var names = new List<string>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            var name = entries[i]?.Name;

            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private sealed class SpawnerRecord
    {
        [JsonPropertyName("map")]
        public string Map { get; set; }

        [JsonPropertyName("location")]
        public int[] Location { get; set; }

        [JsonPropertyName("homeRange")]
        public int HomeRange { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("entries")]
        public List<SpawnerEntryRecord> Entries { get; set; }
    }

    private sealed class SpawnerEntryRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("maxCount")]
        public int MaxCount { get; set; }

        [JsonPropertyName("probability")]
        public int Probability { get; set; }
    }
}
