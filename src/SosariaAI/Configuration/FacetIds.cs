using System;
using System.Collections.Generic;

namespace SosariaAI.Configuration;

/// <summary>
/// Qualifies character and party ids with a facet so two facets never share a key.
/// </summary>
public static class FacetIds
{
    public const char Separator = ':';

    public static string Prefix(string facet, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        if (string.IsNullOrWhiteSpace(facet) || HasFacetPrefix(id))
        {
            return id;
        }

        return $"{facet}{Separator}{id}";
    }

    public static bool Matches(string savedId, string facet, string localUniqueId)
    {
        if (string.IsNullOrEmpty(savedId) || string.IsNullOrEmpty(localUniqueId))
        {
            return false;
        }

        if (savedId.Equals(localUniqueId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var qualified = Prefix(facet, localUniqueId);
        return savedId.Equals(qualified, StringComparison.OrdinalIgnoreCase);
    }

    public static PartyDefinition QualifyParty(string facet, PartyDefinition source)
    {
        if (source == null)
        {
            return null;
        }

        var members = new List<string>(source.Members?.Count ?? 0);

        if (source.Members != null)
        {
            for (var i = 0; i < source.Members.Count; i++)
            {
                members.Add(Prefix(facet, source.Members[i]));
            }
        }

        return new PartyDefinition
        {
            Id = Prefix(facet, source.Id),
            Leader = Prefix(facet, source.Leader),
            Members = members,
            MeetAt = source.MeetAt
        };
    }

    private static bool HasFacetPrefix(string id)
    {
        var separator = id.IndexOf(Separator);
        if (separator <= 0)
        {
            return false;
        }

        return FacetNames.TryCanonical(id[..separator], out _);
    }
}
