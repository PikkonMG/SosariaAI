using SosariaAI.Configuration;
using SosariaAI.Deliberation;
using Xunit;

namespace SosariaAI.Tests;

public class DecisionEventsTests
{
    [Fact]
    public void Qualifies_NamedEventsOnly()
    {
        Assert.True(DecisionEvents.Qualifies(BrainEventKind.Plan, speakerIsPlayer: false));
        Assert.True(DecisionEvents.Qualifies(BrainEventKind.DungeonEnded, speakerIsPlayer: false));
        Assert.True(DecisionEvents.Qualifies(BrainEventKind.PlayerNoticed, speakerIsPlayer: true));
        Assert.True(DecisionEvents.Qualifies(BrainEventKind.Attacked, speakerIsPlayer: true));
    }

    [Fact]
    public void Qualifies_MonsterAttack_DoesNot()
    {
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.Attacked, speakerIsPlayer: false));
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.Spoken, speakerIsPlayer: true));
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.GoalEnded, speakerIsPlayer: true));
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.Decide, speakerIsPlayer: false));
        Assert.False(DecisionEvents.Qualifies(BrainEventKind.Died, speakerIsPlayer: false));
        Assert.True(DecisionEvents.IsChat(BrainEventKind.Spoken));
        Assert.False(DecisionEvents.IsChat(BrainEventKind.PlayerNoticed));
    }

    [Fact]
    public void MayReplaceJob_PlayerNoticed_DoesNotDropDungeonOrHunt()
    {
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Dungeon));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Hunt));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Follow));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.GoTo));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Boat));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Smith));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Tame));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Snoop));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.GoHome));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.VendorSell));
    }

    [Fact]
    public void MayReplaceJob_PlayerNoticed_MayLeaveIdle()
    {
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.IdleWander));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Loiter));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, null));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.DungeonEnded, SkillKinds.Dungeon));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.Attacked, SkillKinds.GoTo));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.Plan, SkillKinds.Hunt));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.Plan, SkillKinds.Mine));
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.Plan, SkillKinds.Flee));
    }

    [Fact]
    public void MaySpeakChoice_OnlyWhenTheChosenJobIsTheOneApplied()
    {
        Assert.True(DecisionEvents.MaySpeakChoice("hunt", choiceApplied: true, "off to the graveyard"));
        Assert.False(DecisionEvents.MaySpeakChoice("hunt", choiceApplied: false, "off to the graveyard"));
        Assert.True(DecisionEvents.MaySpeakChoice(string.Empty, choiceApplied: false, "busy here"));
        Assert.True(DecisionEvents.MaySpeakChoice(null, choiceApplied: false, "busy here"));
    }

    [Theory]
    [InlineData("travel_minoc", true, "heading to minoc, u coming or what")]
    [InlineData("hunt", true, "join me at the graveyard")]
    [InlineData(null, false, "come with me")]
    [InlineData("", false, "meet me at the bank later")]
    public void MaySpeakChoice_NeverSpeaksAnInvite(string choose, bool choiceApplied, string say)
    {
        // A decide never forms a group with the person it noticed, so an invite is a promise nobody keeps.
        Assert.False(DecisionEvents.MaySpeakChoice(choose, choiceApplied, say));
    }

    [Fact]
    public void MaySpeakChoice_KeepsATripClaimTheAppliedChoiceMakes()
    {
        Assert.True(DecisionEvents.MaySpeakChoice("travel_minoc", choiceApplied: true, "heading to minoc"));
        Assert.False(DecisionEvents.MaySpeakChoice("travel_minoc", choiceApplied: false, "heading to minoc"));
    }

    [Theory]
    [InlineData("heading to minoc, u coming or what", false)]
    [InlineData("on my way to the mines", false)]
    [InlineData("meet me at the bank", false)]
    [InlineData("anyone wanna hunt", false)]
    [InlineData("need better tools, time to dig some ore", true)]
    [InlineData("", true)]
    public void MaySpeakPlan_DropsAnyPromise(string say, bool expected)
    {
        Assert.Equal(expected, DecisionEvents.MaySpeakPlan(say));
    }

    [Fact]
    public void ActKeptChoice_OnlyWhenTheActLeftTheJob()
    {
        Assert.True(DecisionEvents.ActKeptChoice("travel:GoTo:minoc", "travel:GoTo:minoc"));
        Assert.True(DecisionEvents.ActKeptChoice("travel:GoTo:minoc", "TRAVEL:GoTo:Minoc"));
        Assert.True(DecisionEvents.ActKeptChoice(null, null));
        Assert.False(DecisionEvents.ActKeptChoice("travel:GoTo:minoc", "hunt:Hunt:graveyard"));
        Assert.False(DecisionEvents.ActKeptChoice(null, "hunt:Hunt:graveyard"));
    }

    [Fact]
    public void MayReplaceJob_PlayerNoticed_DoesNotPullARedOffItsConflictRun()
    {
        // A red camping Destard is there for the players it notices.
        Assert.False(DecisionEvents.MayReplaceJob(BrainEventKind.PlayerNoticed, SkillKinds.Conflict));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.Attacked, SkillKinds.Conflict));
        Assert.True(DecisionEvents.MayReplaceJob(BrainEventKind.Plan, SkillKinds.Conflict));
    }
}
