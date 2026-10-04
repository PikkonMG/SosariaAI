using System;
using System.Collections.Generic;
using SosariaAI.Common;

namespace SosariaAI.Behaviour;

public enum PartyWaitResult
{
    Waiting,
    Formed,
    TimedOut
}

public static class PartyWaitRules
{
    public const int MeetRange = 4;
    public const int LagTiles = 12;
    public const double RestHitsFraction = 0.4;
    public static readonly TimeSpan WaitLimit = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan FollowLimit = TimeSpan.FromMinutes(3);

    public static PartyWaitResult WaitForMembers(
        DateTime now,
        DateTime waitStarted,
        IReadOnlyList<(string Id, int Distance, bool Alive, bool IsLeader)> members,
        int meetRange,
        TimeSpan waitLimit
    )
    {
        if (members == null || members.Count == 0)
        {
            return PartyWaitResult.Formed;
        }

        var allPresent = true;

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            if (!member.Alive)
            {
                continue;
            }

            if (!AtMeet(member.Distance, meetRange))
            {
                allPresent = false;
                break;
            }
        }

        if (allPresent)
        {
            return PartyWaitResult.Formed;
        }

        return now - waitStarted >= waitLimit ? PartyWaitResult.TimedOut : PartyWaitResult.Waiting;
    }

    public static bool ShouldWaitForLag(bool inCombat, int farthestLivingMemberDistance, int lagTiles) =>
        !inCombat && farthestLivingMemberDistance > lagTiles;

    public static bool ShouldRest(double hitsFraction, double restHitsFraction) =>
        hitsFraction < restHitsFraction;

    public static bool IsGathering(bool tripActive, bool disbanded, DateTime waitStarted) =>
        !tripActive && !disbanded && waitStarted != default;

    public static bool AtMeet(int distance, int meetRange) =>
        distance >= 0 && distance <= meetRange;

    public static bool GiveUpWait(
        DateTime now,
        DateTime waitStarted,
        bool tripActive,
        bool gathering,
        TimeSpan waitLimit
    ) =>
        !tripActive && !gathering && TimeRules.Passed(waitStarted, now, waitLimit);
}
