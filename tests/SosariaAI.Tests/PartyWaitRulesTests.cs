using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PartyWaitRulesTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 12, 0, 0);

    [Fact]
    public void WaitForMembers_AllPresent_Formed()
    {
        var result = PartyWaitRules.WaitForMembers(
            Start,
            Start,
            [("bran", 0, true, true), ("sela", 2, true, false), ("tam", 3, true, false)],
            PartyWaitRules.MeetRange,
            PartyWaitRules.WaitLimit
        );
        Assert.Equal(PartyWaitResult.Formed, result);
    }

    [Fact]
    public void WaitForMembers_LeaderNotAtMeet_WaitingThenTimedOut()
    {
        var members = new[] { ("bran", PartyWaitRules.MeetRange + 1, true, true), ("sela", 0, true, false) };
        Assert.Equal(
            PartyWaitResult.Waiting,
            PartyWaitRules.WaitForMembers(Start, Start, members, PartyWaitRules.MeetRange, PartyWaitRules.WaitLimit)
        );
        Assert.Equal(
            PartyWaitResult.TimedOut,
            PartyWaitRules.WaitForMembers(
                Start + PartyWaitRules.WaitLimit,
                Start,
                members,
                PartyWaitRules.MeetRange,
                PartyWaitRules.WaitLimit
            )
        );
    }

    [Fact]
    public void WaitForMembers_LateMember_WaitingThenTimedOut()
    {
        var members = new[] { ("bran", 0, true, true), ("sela", 20, true, false) };
        Assert.Equal(
            PartyWaitResult.Waiting,
            PartyWaitRules.WaitForMembers(Start, Start, members, PartyWaitRules.MeetRange, PartyWaitRules.WaitLimit)
        );
        Assert.Equal(
            PartyWaitResult.TimedOut,
            PartyWaitRules.WaitForMembers(
                Start + PartyWaitRules.WaitLimit,
                Start,
                members,
                PartyWaitRules.MeetRange,
                PartyWaitRules.WaitLimit
            )
        );
    }

    [Fact]
    public void ShouldWaitForLag_OnlyOutOfCombat()
    {
        Assert.False(PartyWaitRules.ShouldWaitForLag(true, 40, PartyWaitRules.LagTiles));
        Assert.True(PartyWaitRules.ShouldWaitForLag(false, 40, PartyWaitRules.LagTiles));
        Assert.False(PartyWaitRules.ShouldWaitForLag(false, 2, PartyWaitRules.LagTiles));
    }

    [Fact]
    public void ShouldRest_UsesFraction()
    {
        Assert.True(PartyWaitRules.ShouldRest(0.3, PartyWaitRules.RestHitsFraction));
        Assert.False(PartyWaitRules.ShouldRest(0.8, PartyWaitRules.RestHitsFraction));
    }

    [Fact]
    public void IsGathering_OnlyWhileTheLeaderWaits()
    {
        Assert.False(PartyWaitRules.IsGathering(tripActive: false, disbanded: false, waitStarted: default));
        Assert.True(PartyWaitRules.IsGathering(tripActive: false, disbanded: false, waitStarted: Start));
        Assert.False(PartyWaitRules.IsGathering(tripActive: true, disbanded: false, waitStarted: Start));
        Assert.False(PartyWaitRules.IsGathering(tripActive: false, disbanded: true, waitStarted: Start));
    }

    [Fact]
    public void AtMeet_WithinMeetRange()
    {
        Assert.False(PartyWaitRules.AtMeet(PartyWaitRules.MeetRange + 1, PartyWaitRules.MeetRange));
        Assert.True(PartyWaitRules.AtMeet(PartyWaitRules.MeetRange, PartyWaitRules.MeetRange));
    }

    [Fact]
    public void GiveUpWait_OnlyWhenNoTripHasStarted()
    {
        Assert.False(
            PartyWaitRules.GiveUpWait(Start + PartyWaitRules.WaitLimit, Start, tripActive: true, gathering: false, PartyWaitRules.WaitLimit)
        );
        Assert.False(
            PartyWaitRules.GiveUpWait(Start + PartyWaitRules.WaitLimit, Start, tripActive: false, gathering: true, PartyWaitRules.WaitLimit)
        );
        Assert.False(
            PartyWaitRules.GiveUpWait(Start, Start, tripActive: false, gathering: false, PartyWaitRules.WaitLimit)
        );
        Assert.True(
            PartyWaitRules.GiveUpWait(
                Start + PartyWaitRules.WaitLimit,
                Start,
                tripActive: false,
                gathering: false,
                PartyWaitRules.WaitLimit
            )
        );
    }

    [Fact]
    public void FollowLimit_GreaterThanWaitLimit() =>
        Assert.True(PartyWaitRules.FollowLimit > PartyWaitRules.WaitLimit);
}
