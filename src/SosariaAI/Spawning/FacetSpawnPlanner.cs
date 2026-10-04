using System.Collections.Generic;
using Server;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;

namespace SosariaAI.Spawning;

/// <summary>
/// A boot warning about one facet. <see cref="Count"/> and <see cref="Needed"/> fill the
/// templates that name numbers: the facet's count, then what it falls short of.
/// </summary>
public readonly record struct FacetPlanWarning(string Template, string Facet, int Count = 0, int Needed = 0);

public sealed class FacetSpawnRequest
{
    public string Facet { get; init; }

    public FacetContent Content { get; init; }

    public IReadOnlyList<SpawnPlanEntry> Entries { get; init; }
}

/// <summary>
/// Turns characters.json maps/facets into the spawn list for each enabled facet.
/// Unknown names and enabled facets with no roster become warnings, not spawns.
/// </summary>
public static class FacetSpawnPlanner
{
    public const string UnknownFacetTemplate = "Unknown facet name {Facet} was skipped";
    public const string MissingContentTemplate =
        "{Facet} is enabled but has no content block and will spawn nothing";
    public const string EraSkippedTemplate =
        "{Facet} is enabled but this expansion does not include that land";

    /// <summary>
    /// The plan walks the roster in order and stops at the count, so a count below the
    /// roster leaves its last people out. The count is the operator's; the plan keeps it.
    /// </summary>
    public const string ShortRosterTemplate =
        "{Facet} count {Count} is below its roster of {Roster}; the roster people past the count do not spawn";

    public static List<FacetSpawnRequest> Plan(
        CharactersConfiguration config,
        ICollection<FacetPlanWarning> warnings
    ) => Plan(config, warnings, Core.Expansion);

    public static List<FacetSpawnRequest> Plan(
        CharactersConfiguration config,
        ICollection<FacetPlanWarning> warnings,
        Expansion expansion
    )
    {
        var requests = new List<FacetSpawnRequest>();

        if (config?.Maps == null || config.Maps.Count == 0)
        {
            return requests;
        }

        foreach (var pair in config.Maps)
        {
            var toggle = pair.Value;

            if (toggle is not { Enabled: true })
            {
                continue;
            }

            if (!FacetNames.TryCanonical(pair.Key, out var facet))
            {
                warnings?.Add(new FacetPlanWarning(UnknownFacetTemplate, pair.Key));
                continue;
            }

            if (!EraRules.FacetAllowed(facet, expansion))
            {
                warnings?.Add(new FacetPlanWarning(EraSkippedTemplate, facet));
                continue;
            }

            if (config.Facets == null ||
                !config.Facets.TryGetValue(facet, out var content) ||
                content == null ||
                !content.HasRoster)
            {
                warnings?.Add(new FacetPlanWarning(MissingContentTemplate, facet));
                continue;
            }

            if (toggle.Count <= 0)
            {
                continue;
            }

            var rosterIds = new List<string>(content.Roster.Count);

            for (var i = 0; i < content.Roster.Count; i++)
            {
                var id = content.Roster[i]?.Id;

                if (!string.IsNullOrWhiteSpace(id))
                {
                    rosterIds.Add(id);
                }
            }

            if (toggle.Count < rosterIds.Count)
            {
                warnings?.Add(new FacetPlanWarning(ShortRosterTemplate, facet, toggle.Count, rosterIds.Count));
            }

            requests.Add(
                new FacetSpawnRequest
                {
                    Facet = facet,
                    Content = content,
                    Entries = SpawnPlan.Build(rosterIds, toggle.Count)
                }
            );
        }

        return requests;
    }
}
