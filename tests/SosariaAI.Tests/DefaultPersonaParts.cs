using System;
using System.Collections.Generic;
using System.Text.Json;
using Server.Json;
using SosariaAI.Configuration;

namespace SosariaAI.Tests;

/// <summary>The persona part library built into the DLL, for tests that read the default content.</summary>
internal static class DefaultPersonaParts
{
    public static IReadOnlyList<PersonaPartPool> Pools()
    {
        var pools = new PersonaPartPool[PersonaPartsFile.Pools.Length];

        for (var i = 0; i < pools.Length; i++)
        {
            using var stream = typeof(PersonaPartsFile).Assembly.GetManifestResourceStream(
                PersonaPartsFile.ResourceName(PersonaPartsFile.Pools[i])
            );
            pools[i] = JsonSerializer.Deserialize<PersonaPartPool>(stream!, JsonConfig.DefaultOptions);
        }

        return pools;
    }

    public static PersonaPartCatalog Catalog()
    {
        var byPool = new Dictionary<string, List<PersonaPart>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pool in Pools())
        {
            byPool[pool.Pool] = pool.Parts ?? [];
        }

        return new PersonaPartCatalog(byPool);
    }
}
