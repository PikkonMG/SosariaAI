using System;
using System.Collections.Generic;

namespace SosariaAI.Spawning;

public readonly record struct SpawnPlanEntry(string UniqueId, string TemplateId);

/// <summary>
/// Walks a roster in order and repeats it until <paramref name="count"/> entries exist.
/// A template used once keeps its id. A template used more than once keeps the first
/// id plain and numbers later copies from 1.
/// </summary>
public static class SpawnPlan
{
    public const char DuplicateMark = '#';
    public const int FirstDuplicateNumber = 1;

    public static IReadOnlyList<SpawnPlanEntry> Build(IReadOnlyList<string> rosterIds, int count)
    {
        if (rosterIds == null || rosterIds.Count == 0 || count <= 0)
        {
            return [];
        }

        var templates = new string[count];

        for (var k = 0; k < count; k++)
        {
            templates[k] = rosterIds[k % rosterIds.Count];
        }

        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var k = 0; k < count; k++)
        {
            var id = templates[k];
            totals[id] = totals.GetValueOrDefault(id) + 1;
        }

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var entries = new SpawnPlanEntry[count];

        for (var k = 0; k < count; k++)
        {
            var template = templates[k];
            var n = seen.GetValueOrDefault(template) + 1;
            seen[template] = n;
            var unique = template;

            if (totals[template] > 1 && n > 1)
            {
                var number = FirstDuplicateNumber + n - 2;
                unique = $"{template}{DuplicateMark}{number}";
            }

            entries[k] = new SpawnPlanEntry(unique, template);
        }

        return entries;
    }
}
