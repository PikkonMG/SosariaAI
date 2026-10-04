using System;
using System.Collections.Generic;
using SosariaAI.Behaviour;

namespace SosariaAI.Configuration;

public sealed class PersonaPartCatalog
{
    public static PersonaPartCatalog Empty { get; } = new(new Dictionary<string, List<PersonaPart>>(StringComparer.OrdinalIgnoreCase));

    private readonly Dictionary<string, List<PersonaPart>> _byPool;

    public PersonaPartCatalog(Dictionary<string, List<PersonaPart>> byPool) =>
        _byPool = byPool ?? new Dictionary<string, List<PersonaPart>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The parts of <paramref name="pool"/> that fit the job and the era band.</summary>
    public IReadOnlyList<PersonaPart> For(string pool, string job, EraBand band)
    {
        if (!_byPool.TryGetValue(pool, out var parts) || parts == null)
        {
            return [];
        }

        var found = new List<PersonaPart>();

        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];

            if (part != null && Fits(part, job) && PersonaEras.Fits(part, band))
            {
                found.Add(part);
            }
        }

        found.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return found;
    }

    public static bool Fits(PersonaPart part, string job)
    {
        if (part?.Jobs == null || string.IsNullOrEmpty(job))
        {
            return false;
        }

        for (var i = 0; i < part.Jobs.Count; i++)
        {
            if (string.Equals(part.Jobs[i], job, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
