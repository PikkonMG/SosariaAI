using System;
using System.Collections.Generic;
using System.IO;

namespace SosariaAI.Configuration;

/// <summary>
/// The persona part pools under persona-parts/. The default library ships inside the DLL,
/// one embedded JSON file per pool; first boot copies a pool out only when its file is
/// missing, so operator edits stay.
/// </summary>
public static class PersonaPartsFile
{
    public const string PoolOrigin = "origin";
    public const string PoolHabit = "habit";
    public const string PoolWant = "want";
    public const string PoolVoice = "voice";
    public const string PoolLikes = "likes";
    public const string PoolDislikes = "dislikes";
    public const string PoolIdle = "idle";
    public const string PoolGreeting = "greeting";
    public const string PoolReturn = "return";
    public const string PoolCombat = "combat";
    public const string PoolLoot = "loot";

    /// <summary>Matches the LogicalName of the embedded pools in SosariaAI.csproj.</summary>
    private const string ResourcePrefix = "SosariaAI.Defaults.PersonaParts.";

    /// <summary>Every pool the library ships, in the order a person is composed.</summary>
    public static readonly string[] Pools =
    [
        PoolOrigin,
        PoolHabit,
        PoolWant,
        PoolVoice,
        PoolLikes,
        PoolDislikes,
        PoolIdle,
        PoolGreeting,
        PoolReturn,
        PoolCombat,
        PoolLoot
    ];

    public const string FolderName = "persona-parts";

    public static string DefaultDirectory => ConfigFile.PathIn(FolderName);

    internal static string ResourceName(string pool) => ResourcePrefix + pool + ConfigFile.FileExtension;

    public static PersonaPartCatalog LoadOrCreate(string directory)
    {
        Directory.CreateDirectory(directory);

        foreach (var pool in Pools)
        {
            WriteMissingDefault(Path.Combine(directory, pool + ConfigFile.FileExtension), pool);
        }

        var byPool = new Dictionary<string, List<PersonaPart>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Directory.GetFiles(directory, ConfigFile.JsonSearchPattern))
        {
            var file = ConfigFile.LoadOrDefault<PersonaPartPool>(path, null, () => null);

            if (file == null || string.IsNullOrWhiteSpace(file.Pool) || file.Parts == null)
            {
                continue;
            }

            if (!byPool.TryGetValue(file.Pool, out var list))
            {
                list = [];
                byPool[file.Pool] = list;
            }

            for (var i = 0; i < file.Parts.Count; i++)
            {
                var part = file.Parts[i];

                if (part == null || string.IsNullOrWhiteSpace(part.Id) || string.IsNullOrWhiteSpace(part.Text))
                {
                    continue;
                }

                list.Add(part);
            }
        }

        return new PersonaPartCatalog(byPool);
    }

    /// <summary>Copies the embedded pool out as written, so the operator edits the shipped text.</summary>
    private static void WriteMissingDefault(string path, string pool)
    {
        if (File.Exists(path))
        {
            return;
        }

        using var source = OpenDefault(pool);
        using var target = File.Create(path);
        source.CopyTo(target);
    }

    private static Stream OpenDefault(string pool) =>
        typeof(PersonaPartsFile).Assembly.GetManifestResourceStream(ResourceName(pool)) ??
        throw new InvalidOperationException($"The persona part pool {pool} is not built into the DLL.");
}
