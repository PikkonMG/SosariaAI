using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PartyLeadTests
{
    private const string LeaderId = "bran";
    private const string FirstOtherId = "sela";
    private const string SecondOtherId = "tam";
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(PartyScale.FallenGraceMinutes);

    [Fact]
    public void Promote_LeaderAlive_Unchanged() =>
        Assert.Equal(
            LeaderId,
            PartyLead.Promote(
                LeaderId,
                [(LeaderId, true), (FirstOtherId, true), (SecondOtherId, true)],
                Now,
                default,
                Grace
            )
        );

    [Fact]
    public void Promote_DeadWithinGrace_Unchanged() =>
        Assert.Equal(
            LeaderId,
            PartyLead.Promote(
                LeaderId,
                [(LeaderId, false), (FirstOtherId, true)],
                Now,
                Now - Grace + TimeSpan.FromSeconds(1),
                Grace
            )
        );

    [Fact]
    public void Promote_DeadAfterGrace_PromotesFirstOtherAlive() =>
        Assert.Equal(
            FirstOtherId,
            PartyLead.Promote(
                LeaderId,
                [(LeaderId, false), (FirstOtherId, true), (SecondOtherId, true)],
                Now,
                Now - Grace,
                Grace
            )
        );

    [Fact]
    public void Promote_AllDead_KeepsCurrent() =>
        Assert.Equal(
            LeaderId,
            PartyLead.Promote(
                LeaderId,
                [(LeaderId, false), (FirstOtherId, false), (SecondOtherId, false)],
                Now,
                Now - Grace,
                Grace
            )
        );

    [Fact]
    public void Promote_SkipsDeadOthers() =>
        Assert.Equal(
            SecondOtherId,
            PartyLead.Promote(
                LeaderId,
                [(LeaderId, false), (FirstOtherId, false), (SecondOtherId, true)],
                Now,
                Now - Grace,
                Grace
            )
        );

    [Fact]
    public void Promote_EmptyOrNullMembers_KeepsCurrent()
    {
        Assert.Equal(LeaderId, PartyLead.Promote(LeaderId, [], Now, default, Grace));
        Assert.Equal(LeaderId, PartyLead.Promote(LeaderId, null, Now, default, Grace));
    }

    [Fact]
    public void FollowIndex_LivingLeaderIsFollowed() =>
        Assert.Equal(0, PartyLead.FollowIndex(self: 2, leader: 0, [true, true, true]));

    [Fact]
    public void FollowIndex_DeadLeaderHandsOffToTheFirstLivingMember()
    {
        Assert.Equal(1, PartyLead.FollowIndex(self: 2, leader: 0, [false, true, true]));
        Assert.Equal(PartyLead.NoOne, PartyLead.FollowIndex(self: 1, leader: 0, [false, true, true]));
    }

    [Fact]
    public void FollowIndex_TheLeaderFollowsNoOne()
    {
        Assert.Equal(PartyLead.NoOne, PartyLead.FollowIndex(self: 0, leader: 0, [true, true]));
        Assert.Equal(PartyLead.NoOne, PartyLead.FollowIndex(self: 0, leader: 0, null));
    }

    [Fact]
    public void Successor_IsTheFirstLivingMemberOtherThanTheLeader()
    {
        Assert.Equal(1, PartyLead.Successor(leader: 0, [false, true, true]));
        Assert.Equal(2, PartyLead.Successor(leader: 1, [false, true, true]));
        Assert.Equal(0, PartyLead.Successor(leader: 2, [true, false, true]));
        Assert.Equal(PartyLead.NoOne, PartyLead.Successor(leader: 0, [false, false]));
        Assert.Equal(PartyLead.NoOne, PartyLead.Successor(leader: 0, null));
    }
}
