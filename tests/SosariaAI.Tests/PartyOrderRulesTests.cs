using System;
using SosariaAI.Behaviour;
using Xunit;

namespace SosariaAI.Tests;

public class PartyOrderRulesTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0);

    [Theory]
    [InlineData("follow me", PartyOrderKind.Follow, null)]
    [InlineData("On me!", PartyOrderKind.Follow, null)]
    [InlineData("ok lets go", PartyOrderKind.Follow, null)]
    [InlineData("wait here", PartyOrderKind.Hold, null)]
    [InlineData("hold", PartyOrderKind.Hold, null)]
    [InlineData("stop", PartyOrderKind.Hold, null)]
    [InlineData("stay", PartyOrderKind.Hold, null)]
    [InlineData("go to despise", PartyOrderKind.GoTo, "despise")]
    [InlineData("lets go to the britain bank", PartyOrderKind.GoTo, "the britain bank")]
    [InlineData("take me to Minoc", PartyOrderKind.GoTo, "minoc")]
    [InlineData("attack", PartyOrderKind.Attack, "")]
    [InlineData("attack the troll", PartyOrderKind.Attack, "the troll")]
    [InlineData("kill it", PartyOrderKind.Attack, "")]
    [InlineData("fall back", PartyOrderKind.Back, null)]
    [InlineData("retreat!", PartyOrderKind.Back, null)]
    [InlineData("regroup", PartyOrderKind.Regroup, null)]
    [InlineData("to me", PartyOrderKind.Regroup, null)]
    [InlineData("come to me", PartyOrderKind.Regroup, null)]
    [InlineData("everyone to me!", PartyOrderKind.Regroup, null)]
    [InlineData("get over here", PartyOrderKind.Regroup, null)]
    [InlineData("come to the bank", PartyOrderKind.GoTo, "the bank")]
    public void ReadOrder_PlacesTheLeadersWords(string text, PartyOrderKind kind, string remainder)
    {
        Assert.Equal(kind, PartyOrderRules.ReadOrder(text, out var rest));
        Assert.Equal(remainder, rest);
    }

    [Theory]
    [InlineData("I will follow you later")]
    [InlineData("heal")]
    [InlineData("pull")]
    [InlineData("nice weather today")]
    [InlineData("")]
    [InlineData(null)]
    public void ReadOrder_OtherTalkIsNoOrder(string text)
    {
        Assert.Equal(PartyOrderKind.None, PartyOrderRules.ReadOrder(text, out var rest));
        Assert.Null(rest);
    }

    [Theory]
    [InlineData("heal me", PartyHelpKind.HealMe)]
    [InlineData("need heal pls", PartyHelpKind.HealMe)]
    [InlineData("wait for me", PartyHelpKind.WaitForMe)]
    [InlineData("wait up!", PartyHelpKind.WaitForMe)]
    [InlineData("hold on", PartyHelpKind.WaitForMe)]
    [InlineData("im stuck", PartyHelpKind.Stuck)]
    [InlineData("stuck", PartyHelpKind.Stuck)]
    [InlineData("cant get there", PartyHelpKind.Stuck)]
    [InlineData("help", PartyHelpKind.Backup)]
    [InlineData("need backup!", PartyHelpKind.Backup)]
    [InlineData("help me pls", PartyHelpKind.Backup)]
    [InlineData("help im stuck", PartyHelpKind.Stuck)]
    [InlineData("help, heal me", PartyHelpKind.HealMe)]
    [InlineData("where are you?", PartyHelpKind.Where)]
    [InlineData("bran where r u", PartyHelpKind.Where)]
    [InlineData("where is everyone", PartyHelpKind.Where)]
    [InlineData("wya", PartyHelpKind.Where)]
    [InlineData("where is the bank", PartyHelpKind.None)]
    [InlineData("heal", PartyHelpKind.None)]
    [InlineData("follow me", PartyHelpKind.None)]
    [InlineData("wait here", PartyHelpKind.None)]
    [InlineData("", PartyHelpKind.None)]
    public void ReadHelp_PlacesAMembersCall(string text, PartyHelpKind kind) =>
        Assert.Equal(kind, PartyOrderRules.ReadHelp(text));

    [Fact]
    public void MayOrder_TheLeaderOrdersAnything_AMemberOnlyARegroupOnItself()
    {
        Assert.True(PartyOrderRules.MayOrder(PartyOrderKind.Follow, speakerLeads: true, speakerIsHuman: true));
        Assert.True(PartyOrderRules.MayOrder(PartyOrderKind.Regroup, speakerLeads: false, speakerIsHuman: true));
        Assert.False(PartyOrderRules.MayOrder(PartyOrderKind.Follow, speakerLeads: false, speakerIsHuman: true));
        Assert.False(PartyOrderRules.MayOrder(PartyOrderKind.Regroup, speakerLeads: true, speakerIsHuman: false));
        Assert.False(PartyOrderRules.MayOrder(PartyOrderKind.Follow, speakerLeads: true, speakerIsHuman: false));
    }

    [Theory]
    [InlineData(true, 0.45, 1, true)]
    [InlineData(true, 0.90, 2, true)]
    [InlineData(true, 0.90, 1, false)]
    [InlineData(true, 0.50, 1, false)]
    [InlineData(false, 0.10, 3, false)]
    public void CallsBackup_HurtOrOutnumberedInAFight(bool inFight, double hits, int attackers, bool calls) =>
        Assert.Equal(calls, PartyOrderRules.CallsBackup(inFight, hits, attackers));

    [Fact]
    public void TakesBackup_NotWhenSentBackOrRunning()
    {
        Assert.True(PartyOrderRules.TakesBackup(PartyOrderKind.Follow, fleeing: false));
        Assert.True(PartyOrderRules.TakesBackup(PartyOrderKind.None, fleeing: false));
        Assert.False(PartyOrderRules.TakesBackup(PartyOrderKind.Back, fleeing: false));
        Assert.False(PartyOrderRules.TakesBackup(PartyOrderKind.Follow, fleeing: true));
    }

    [Fact]
    public void AnswersWhere_NamedOrNobodyNamed()
    {
        Assert.True(PartyOrderRules.AnswersWhere(namesListener: true, namesAnother: true));
        Assert.True(PartyOrderRules.AnswersWhere(namesListener: false, namesAnother: false));
        Assert.False(PartyOrderRules.AnswersWhere(namesListener: false, namesAnother: true));
    }

    [Fact]
    public void Bearing_BesideByCompassOrAnotherMap()
    {
        Assert.Equal("right next to u", PartyOrderRules.Bearing(sameMap: true, PartyOrderRules.BesideTiles, "north"));
        Assert.Equal("12 tiles north of u", PartyOrderRules.Bearing(sameMap: true, 12, "north"));
        Assert.Equal("not on ur map", PartyOrderRules.Bearing(sameMap: false, 3, "north"));
    }

    [Fact]
    public void RegroupDone_BesideTheCallerOnTheSameMap()
    {
        Assert.True(PartyOrderRules.RegroupDone(sameMap: true, PartyOrderRules.BesideTiles));
        Assert.False(PartyOrderRules.RegroupDone(sameMap: true, PartyOrderRules.BesideTiles + 1));
        Assert.False(PartyOrderRules.RegroupDone(sameMap: false, 0));
    }

    [Fact]
    public void PlaceTokens_TriesTheWholeThenEachWord()
    {
        Assert.Equal(["britain bank", "britain", "bank"], PartyOrderRules.PlaceTokens("the britain bank"));
        Assert.Equal(["despise"], PartyOrderRules.PlaceTokens("to despise!"));
        Assert.Equal(["despise"], PartyOrderRules.PlaceTokens("Despise"));
        Assert.Empty(PartyOrderRules.PlaceTokens(""));
        Assert.Empty(PartyOrderRules.PlaceTokens("  . "));
    }

    [Fact]
    public void Expired_HoldAndBackRunOut_FollowNever()
    {
        Assert.True(PartyOrderRules.Expired(PartyOrderKind.Hold, Now - PartyOrderRules.HoldLimit, Now));
        Assert.False(PartyOrderRules.Expired(PartyOrderKind.Hold, Now - PartyOrderRules.HoldLimit + TimeSpan.FromSeconds(1), Now));
        Assert.True(PartyOrderRules.Expired(PartyOrderKind.Back, Now - PartyOrderRules.BackHold, Now));
        Assert.False(PartyOrderRules.Expired(PartyOrderKind.Back, Now - TimeSpan.FromSeconds(10), Now));
        Assert.False(PartyOrderRules.Expired(PartyOrderKind.Follow, Now - TimeSpan.FromHours(1), Now));
        Assert.False(PartyOrderRules.Expired(PartyOrderKind.GoTo, Now - TimeSpan.FromHours(1), Now));
    }

    [Fact]
    public void AfterDone_EveryOrderSettlesToFollow()
    {
        Assert.Equal(PartyOrderKind.Follow, PartyOrderRules.AfterDone(PartyOrderKind.Hold));
        Assert.Equal(PartyOrderKind.Follow, PartyOrderRules.AfterDone(PartyOrderKind.Attack));
        Assert.Equal(PartyOrderKind.Follow, PartyOrderRules.AfterDone(PartyOrderKind.Follow));
        Assert.Equal(PartyOrderKind.None, PartyOrderRules.AfterDone(PartyOrderKind.None));
    }

    [Fact]
    public void Lag_IsPastTwelveTilesOnTheSameMap()
    {
        Assert.True(PartyOrderRules.LagsBehind(sameMap: true, PartyOrderRules.LagTiles + 1));
        Assert.False(PartyOrderRules.LagsBehind(sameMap: true, PartyOrderRules.LagTiles));
        Assert.False(PartyOrderRules.LagsBehind(sameMap: false, 100));
        Assert.True(PartyOrderRules.CountsForLag(PartyOrderKind.Follow));
        Assert.False(PartyOrderRules.CountsForLag(PartyOrderKind.GoTo));
        Assert.False(PartyOrderRules.CountsForLag(PartyOrderKind.Hold));
        Assert.True(PartyOrderRules.PausesWalk(leaderPresent: true, sameMap: true, PartyOrderRules.LagTiles + 1));
        Assert.False(PartyOrderRules.PausesWalk(leaderPresent: false, sameMap: true, 100));
    }

    [Fact]
    public void NeedsBack_OnlyAHurtFighter()
    {
        Assert.True(PartyOrderRules.NeedsBack(inFight: true, 0.39));
        Assert.False(PartyOrderRules.NeedsBack(inFight: true, PartyOrderRules.BackBelowHitsFraction));
        Assert.False(PartyOrderRules.NeedsBack(inFight: false, 0.1));
    }

    [Fact]
    public void FreeToHeal_OnlyAHealerOutOfDanger()
    {
        Assert.True(PartyOrderRules.FreeToHeal(canHeal: true, inFight: false, mustRun: false));
        Assert.False(PartyOrderRules.FreeToHeal(canHeal: false, inFight: false, mustRun: false));
        Assert.False(PartyOrderRules.FreeToHeal(canHeal: true, inFight: true, mustRun: false));
        Assert.False(PartyOrderRules.FreeToHeal(canHeal: true, inFight: false, mustRun: true));
    }

    [Fact]
    public void WaitEnds_WhenTheCallerIsCloseGoneOrTooLate()
    {
        var fresh = Now - TimeSpan.FromSeconds(5);
        Assert.True(PartyOrderRules.WaitEnds(callerPresent: false, sameMap: false, 0, fresh, Now));
        Assert.True(PartyOrderRules.WaitEnds(callerPresent: true, sameMap: true, PartyOrderRules.CaughtUpTiles, fresh, Now));
        Assert.False(PartyOrderRules.WaitEnds(callerPresent: true, sameMap: true, PartyOrderRules.CaughtUpTiles + 1, fresh, Now));
        Assert.False(PartyOrderRules.WaitEnds(callerPresent: true, sameMap: true, PartyOrderRules.LagTiles, fresh, Now));
        Assert.False(PartyOrderRules.WaitEnds(callerPresent: true, sameMap: false, 0, fresh, Now));
        Assert.True(PartyOrderRules.WaitEnds(callerPresent: true, sameMap: true, 50, Now - PartyOrderRules.HelpWaitLimit, Now));
    }

    [Fact]
    public void WaitRested_AfterAGaveUpWait_TheLeaderWaitsAgainOnlyAfterTheLimit()
    {
        Assert.True(PartyOrderRules.WaitRested(default, Now));
        Assert.False(PartyOrderRules.WaitRested(Now - TimeSpan.FromSeconds(30), Now));
        Assert.True(PartyOrderRules.WaitRested(Now - PartyOrderRules.HelpWaitLimit, Now));
    }

    [Fact]
    public void Stuck_IsThirtySecondsStill()
    {
        Assert.True(PartyOrderRules.IsStuck(Now - PartyOrderRules.StuckAfter, Now));
        Assert.False(PartyOrderRules.IsStuck(Now - TimeSpan.FromSeconds(10), Now));
        Assert.False(PartyOrderRules.IsStuck(default, Now));
    }

    [Fact]
    public void AnyPersonInThePartyCallsForHelp()
    {
        Assert.True(PartyOrderRules.MayCallHelp(speakerIsHuman: true));
        Assert.False(PartyOrderRules.MayCallHelp(speakerIsHuman: false));
    }
}
