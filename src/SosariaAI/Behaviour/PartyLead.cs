using System;
using System.Collections.Generic;

namespace SosariaAI.Behaviour;

public static class PartyLead
{
    public const int NoOne = -1;

    /// <summary>
    /// Whom member <paramref name="self"/> of a real party follows: the leader while it
    /// lives, else the first living member in the party's order. <see cref="NoOne"/> when
    /// that is the member itself: it leads now.
    /// </summary>
    public static int FollowIndex(int self, int leader, IReadOnlyList<bool> alive)
    {
        if (alive == null || self < 0 || self >= alive.Count)
        {
            return NoOne;
        }

        var lead = leader >= 0 && leader < alive.Count && alive[leader] ? leader : Successor(leader, alive);
        return lead == self ? NoOne : lead;
    }

    /// <summary>
    /// Who leads on when the leader falls: the first living member in the party's order
    /// other than the leader. <see cref="NoOne"/> when nobody else lives.
    /// </summary>
    public static int Successor(int leader, IReadOnlyList<bool> alive)
    {
        for (var i = 0; i < (alive?.Count ?? 0); i++)
        {
            if (i != leader && alive[i])
            {
                return i;
            }
        }

        return NoOne;
    }

    public static string Promote(
        string currentLeader,
        IReadOnlyList<(string Id, bool Alive)> members,
        DateTime now,
        DateTime leaderDiedAt,
        TimeSpan grace
    )
    {
        if (string.IsNullOrWhiteSpace(currentLeader) || members == null || members.Count == 0)
        {
            return currentLeader;
        }

        var leaderAlive = false;

        for (var i = 0; i < members.Count; i++)
        {
            if (string.Equals(members[i].Id, currentLeader, StringComparison.OrdinalIgnoreCase))
            {
                leaderAlive = members[i].Alive;
                break;
            }
        }

        if (leaderAlive)
        {
            return currentLeader;
        }

        if (leaderDiedAt != default && now - leaderDiedAt < grace)
        {
            return currentLeader;
        }

        for (var i = 0; i < members.Count; i++)
        {
            if (members[i].Alive &&
                !string.Equals(members[i].Id, currentLeader, StringComparison.OrdinalIgnoreCase))
            {
                return members[i].Id;
            }
        }

        return currentLeader;
    }
}
