using SosariaAI.Behaviour;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class LawfulRulesTests
{
    [Fact]
    public void ShouldWarn_NeedsVictimName()
    {
        Assert.True(LawfulRules.ShouldWarn(DispositionKind.Lawful, true, true));
        Assert.False(LawfulRules.ShouldWarn(DispositionKind.Neutral, true, true));
        Assert.False(LawfulRules.ShouldWarn(DispositionKind.Lawful, false, true));
        Assert.False(LawfulRules.ShouldWarn(DispositionKind.Lawful, true, false));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    public void MayDrawInDen_OnlyRidingAgainstItOrForAFriend(bool inDen, bool ridesAgainstDen, bool outlawOnFriend, bool mayDraw) =>
        Assert.Equal(mayDraw, LawfulRules.MayDrawInDen(inDen, ridesAgainstDen, outlawOnFriend));

    [Theory]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    public void LeavesDen_ABlueWithNoFightThere(bool red, bool inDen, bool ridesAgainstDen, bool leaves) =>
        Assert.Equal(leaves, LawfulRules.LeavesDen(red, inDen, ridesAgainstDen));

    [Fact]
    public void HelpLine_IsAShortCall()
    {
        Assert.Equal("help", LawfulRules.HelpLine());
    }

    [Fact]
    public void IsOutlaw_RedAlwaysGrayOnlyWhileHurtingAPerson()
    {
        Assert.True(LawfulRules.IsOutlaw(isRed: true, isCriminal: false, attackingPerson: false));
        Assert.True(LawfulRules.IsOutlaw(isRed: false, isCriminal: true, attackingPerson: true));
        Assert.False(LawfulRules.IsOutlaw(isRed: false, isCriminal: true, attackingPerson: false));
        Assert.False(LawfulRules.IsOutlaw(isRed: false, isCriminal: false, attackingPerson: true));
    }

    [Fact]
    public void IsHunter_AdventurersAndCrewsButNeverOutlaws()
    {
        Assert.True(LawfulRules.IsHunter(fighter: true, DispositionKind.Lawful, inParty: false, hunting: false));
        Assert.True(LawfulRules.IsHunter(fighter: true, DispositionKind.Neutral, inParty: true, hunting: false));
        Assert.True(LawfulRules.IsHunter(fighter: true, DispositionKind.Neutral, inParty: false, hunting: true));
        Assert.False(LawfulRules.IsHunter(fighter: true, DispositionKind.Neutral, inParty: false, hunting: false));
        Assert.False(LawfulRules.IsHunter(fighter: true, DispositionKind.Outlaw, inParty: true, hunting: true));
        Assert.False(LawfulRules.IsHunter(fighter: false, DispositionKind.Lawful, inParty: true, hunting: true));
    }

    [Fact]
    public void ShouldHunt_OutsideGuardsWithOddsOrForAFriend()
    {
        Assert.True(LawfulRules.ShouldHunt(underGuards: false, sidePower: 90, outlawPower: 100, outlawOnFriend: false));
        Assert.False(LawfulRules.ShouldHunt(underGuards: false, sidePower: 50, outlawPower: 100, outlawOnFriend: false));
        Assert.True(LawfulRules.ShouldHunt(underGuards: false, sidePower: 50, outlawPower: 100, outlawOnFriend: true));
        Assert.False(LawfulRules.ShouldHunt(underGuards: true, sidePower: 500, outlawPower: 100, outlawOnFriend: true));
    }
}
