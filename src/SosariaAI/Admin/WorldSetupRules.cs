using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;

namespace SosariaAI.Admin;

/// <summary>One engine command the first time setup runs, with its argument line.</summary>
public readonly record struct SetupStep(string Command, string Arguments);

/// <summary>
/// The world a fresh save lacks: decorations, doors, signs, teleporters, moongates, then the
/// engine's own monster and vendor spawners of the era's spawn sets for each enabled facet. Pure.
/// </summary>
public static class WorldSetupRules
{
    public const string Decorate = "Decorate";
    public const string DoorGen = "DoorGen";
    public const string SignGen = "SignGen";
    public const string TelGen = "TelGen";
    public const string MoonGen = "MoonGen";

    /// <summary>The engine's champion spawns in Felucca's dungeons and the Lost Lands.</summary>
    public const string GenChamps = "GenChamps";
    public const string ImportSpawners = "ImportSpawners";

    public const string SharedSpawnSet = "shared";
    public const string UomlSpawnSet = "uoml";
    public const string PostUomlSpawnSet = "post-uoml";

    /// <summary>The Second Age world: the spawns every era shares, nothing later.</summary>
    public static readonly string[] T2ASpawnSets = [SharedSpawnSet];

    /// <summary>Age of Shadows to Mondain's Legacy: the shared spawns and the ML-era set.</summary>
    public static readonly string[] MLSpawnSets = [SharedSpawnSet, UomlSpawnSet];

    /// <summary>Stygian Abyss on: the shared spawns and the post-ML set.</summary>
    public static readonly string[] ModernSpawnSets = [SharedSpawnSet, PostUomlSpawnSet];

    public const string SpawnRoot = "Data/Spawns";
    public const string SpawnFilePattern = "*.json";

    /// <summary>Spawn file folders under Distribution/Data/Spawns for an era, in load order.</summary>
    public static string[] SpawnSets(EraBand band) =>
        band switch
        {
            EraBand.T2A => T2ASpawnSets,
            EraBand.ML => MLSpawnSets,
            _ => ModernSpawnSets
        };

    public static List<SetupStep> Steps(EraBand band, IEnumerable<string> enabledFacets, Func<string, bool> spawnFolderExists)
    {
        var sets = SpawnSets(band);
        var steps = new List<SetupStep>
        {
            new(Decorate, string.Empty),
            new(DoorGen, string.Empty),
            new(SignGen, string.Empty),
            new(TelGen, string.Empty),
            new(MoonGen, string.Empty)
        };

        if (BuildsChampions(band))
        {
            steps.Add(new SetupStep(GenChamps, string.Empty));
        }

        foreach (var facet in enabledFacets)
        {
            if (string.IsNullOrWhiteSpace(facet))
            {
                continue;
            }

            for (var i = 0; i < sets.Length; i++)
            {
                var folder = SpawnFolder(sets[i], facet);

                if (spawnFolderExists(folder))
                {
                    steps.Add(new SetupStep(ImportSpawners, $"{folder}/{SpawnFilePattern}"));
                }
            }
        }

        return steps;
    }

    /// <summary>
    /// Champion spawns, and the power scrolls they drop, from Age of Shadows on. A T2A world
    /// is the 1999 world. ModernUO itself would build them in any era.
    /// </summary>
    public static bool BuildsChampions(EraBand band) => band != EraBand.T2A;

    public static string SpawnFolder(string set, string facet) => $"{SpawnRoot}/{set}/{facet.ToLowerInvariant()}";

    /// <summary>A world with spawners or bankers in it is set up; a bare save is not.</summary>
    public static bool AwaitsSetup(bool worldHasSpawners, bool worldHasBankers) => !worldHasSpawners && !worldHasBankers;
}
