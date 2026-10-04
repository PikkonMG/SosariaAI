using System;
using System.Collections.Generic;

namespace SosariaAI.Memory;

/// <summary>A person who was in an adventure, and what that person did in it (see <see cref="AdventureRoles"/>).</summary>
public readonly record struct AdventureMember(PersonRef Person, string Role);

/// <summary>
/// One shared outing or big moment. One row of the <c>adventures</c> table and its
/// <c>adventure_members</c> rows. <see cref="MemoryStore.Record"/> sets the id and the weight.
/// </summary>
public sealed record Adventure
{
    public long Id { get; init; }

    /// <summary>One of <see cref="AdventureKinds"/>.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Where it happened, from <c>PlaceNames.Of</c>.</summary>
    public string Place { get; init; } = string.Empty;

    public string Map { get; init; } = string.Empty;

    public int X { get; init; }

    public int Y { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime EndedAt { get; init; }

    public int Kills { get; init; }

    public int Deaths { get; init; }

    /// <summary>One line, for talk and the staff command.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>The summary, or the kind when a row has no summary.</summary>
    public string Headline => string.IsNullOrWhiteSpace(Summary) ? Kind : Summary;

    /// <summary>How big the moment was (see <see cref="MemoryFade.WeightOf"/>).</summary>
    public int Weight { get; init; }

    /// <summary>How many times characters told it.</summary>
    public int ToldCount { get; init; }

    public IReadOnlyList<AdventureMember> Members { get; init; } = [];

    /// <summary>The member with this person id, or null.</summary>
    public AdventureMember? MemberOf(string personId)
    {
        for (var i = 0; i < Members.Count; i++)
        {
            if (string.Equals(Members[i].Person.Id, personId, StringComparison.Ordinal))
            {
                return Members[i];
            }
        }

        return null;
    }
}
