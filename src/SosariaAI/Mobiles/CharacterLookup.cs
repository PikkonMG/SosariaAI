using System;
using SosariaAI.Configuration;

namespace SosariaAI.Mobiles;

/// <summary>
/// Staff commands look up a character by the name it wears, by its persona key
/// (mira), or by its unique id (Felucca:mira). Pure. No world objects.
/// </summary>
public static class CharacterLookup
{
    public const char ReplicaMark = '#';
    public const int RankNone = 0;
    public const int RankPrefix = 1;
    public const int RankReplicaTemplate = 2;
    public const int RankPersona = 3;
    public const int RankFixtureTemplate = 4;
    public const int RankId = 5;
    public const int RankName = 6;

    public static string PersonaKey(string characterId, string personaId)
    {
        if (!string.IsNullOrWhiteSpace(personaId))
        {
            return personaId;
        }

        return LocalKey(characterId);
    }

    public static string LocalKey(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return characterId;
        }

        var local = ReplicaKey(characterId);
        var replica = local.IndexOf(ReplicaMark);

        return replica >= 0 ? local[..replica] : local;
    }

    public static string ReplicaKey(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return characterId;
        }

        var separator = characterId.LastIndexOf(FacetIds.Separator);
        return separator >= 0 ? characterId[(separator + 1)..] : characterId;
    }

    public static int ReplicaNumber(string characterId)
    {
        var local = ReplicaKey(characterId);

        if (string.IsNullOrWhiteSpace(local))
        {
            return 0;
        }

        var mark = local.LastIndexOf(ReplicaMark);

        if (mark < 0 || mark == local.Length - 1)
        {
            return 0;
        }

        return int.TryParse(local[(mark + 1)..], out var number) && number > 0 ? number : 0;
    }

    public static int MatchRank(string wanted, string name, string personaId, string characterId)
    {
        if (string.IsNullOrWhiteSpace(wanted))
        {
            return RankNone;
        }

        if (EqualsKey(name, wanted))
        {
            return RankName;
        }

        if (EqualsKey(characterId, wanted) || EqualsKey(ReplicaKey(characterId), wanted))
        {
            return RankId;
        }

        if (EqualsKey(personaId, wanted))
        {
            return RankPersona;
        }

        var local = LocalKey(characterId);

        if (EqualsKey(local, wanted))
        {
            return ReplicaNumber(characterId) > 0 ? RankReplicaTemplate : RankFixtureTemplate;
        }

        return StartsKey(name, wanted) ||
               StartsKey(personaId, wanted) ||
               StartsKey(local, wanted) ||
               StartsKey(ReplicaKey(characterId), wanted)
            ? RankPrefix
            : RankNone;
    }

    public static bool Better(int rank, int replica, int otherRank, int otherReplica)
    {
        if (rank != otherRank)
        {
            return rank > otherRank;
        }

        return replica < otherReplica;
    }

    public static bool PreferFacet(string staffFacet, string candidateFacet) =>
        !string.IsNullOrWhiteSpace(staffFacet) &&
        staffFacet.Equals(candidateFacet, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsKey(string value, string wanted) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Equals(wanted, StringComparison.OrdinalIgnoreCase);

    private static bool StartsKey(string value, string wanted) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.StartsWith(wanted, StringComparison.OrdinalIgnoreCase);
}
