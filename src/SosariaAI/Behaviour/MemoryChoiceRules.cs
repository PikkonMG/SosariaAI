using System;
using System.Collections.Generic;
using SosariaAI.Memory;

namespace SosariaAI.Behaviour;

/// <summary>
/// How long-term memory changes the next choice: the best friend to follow or visit, the enemy
/// to keep clear of, and the places where the character or a close friend died lately. Reads
/// the bonds and adventures in a <see cref="MemoryStore"/>. Free. No model.
/// </summary>
public static class MemoryChoiceRules
{
    public const double HuntCut = 0.2;
    public const double FriendFollowBoost = 3.0;
    public const double EnemyFollowCut = 0.05;

    /// <summary>The person the owner likes best, or null when nobody is liked more than a stranger.</summary>
    public static string FriendId(MemoryStore store, string ownerId)
    {
        var bonds = store?.BondsOf(ownerId) ?? [];
        return bonds.Count > 0 && bonds[0].Score > BondRules.NeutralScore ? bonds[0].OtherId : null;
    }

    /// <summary>The name of <see cref="FriendId"/>, or null.</summary>
    public static string FriendName(MemoryStore store, string ownerId) =>
        Recall.NameOf(store, FriendId(store, ownerId));

    /// <summary>The name of the person the owner dislikes most, or null when no bond is cold.</summary>
    public static string EnemyName(MemoryStore store, string ownerId)
    {
        var bonds = store?.BondsOf(ownerId) ?? [];
        return bonds.Count > 0 && BondRules.IsCold(bonds[^1].Score) ? Recall.NameOf(store, bonds[^1].OtherId) : null;
    }

    /// <summary>True when the owner's bond to the other person is cold.</summary>
    public static bool Avoids(MemoryStore store, string ownerId, string otherId) =>
        store?.BondOf(ownerId, otherId) is { } bond && BondRules.IsCold(bond.Score);

    /// <summary>
    /// Where the owner's close (warm) friends fell inside the span: a hunter keeps off those
    /// places for a while.
    /// </summary>
    public static IReadOnlyList<string> FriendDeathPlaces(MemoryStore store, string ownerId, DateTime now, TimeSpan span)
    {
        var places = new List<string>();
        var bonds = store?.BondsOf(ownerId) ?? [];
        var cutoff = now - span;

        for (var i = 0; i < bonds.Count && BondRules.IsWarm(bonds[i].Score); i++)
        {
            var friendId = bonds[i].OtherId;
            var adventures = store.AdventuresOf(friendId);

            for (var a = 0; a < adventures.Count && adventures[a].EndedAt >= cutoff; a++)
            {
                if (adventures[a].MemberOf(friendId)?.Role == AdventureRoles.Fallen &&
                    !string.IsNullOrWhiteSpace(adventures[a].Place) &&
                    !places.Contains(adventures[a].Place))
                {
                    places.Add(adventures[a].Place);
                }
            }
        }

        return places;
    }
}
