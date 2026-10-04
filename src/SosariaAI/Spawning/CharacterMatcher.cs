using System.Collections.Generic;
using SosariaAI.Configuration;

namespace SosariaAI.Spawning;

/// <summary>
/// Matches a saved character to a spawn-plan entry by its full unique id.
/// </summary>
public static class CharacterMatcher
{
    public static SpawnPlanEntry? TakeMatch(string characterId, string facet, List<SpawnPlanEntry> remaining)
    {
        if (remaining == null || string.IsNullOrEmpty(characterId))
        {
            return null;
        }

        for (var i = 0; i < remaining.Count; i++)
        {
            if (FacetIds.Matches(characterId, facet, remaining[i].UniqueId))
            {
                var match = remaining[i];
                remaining.RemoveAt(i);
                return match;
            }
        }

        return null;
    }
}
