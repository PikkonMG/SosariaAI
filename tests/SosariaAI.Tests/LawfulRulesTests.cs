using SosariaAI.Behaviour;
using SosariaAI.Combat;
using Xunit;

namespace SosariaAI.Tests;

public class LawfulRulesTests
{
    [Fact]
    public void ShouldWarn_NeedsVictimName()
    {
        Assert.True(LawfulRules.ShouldWarn(DispositionKind.Lawful, victimNamed: true));
        Assert.False(LawfulRules.ShouldWarn(DispositionKind.Neutral, victimNamed: true));
        Assert.False(LawfulRules.ShouldWarn(DispositionKind.Lawful, victimNamed: false));
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
    public void ShouldHunt_WithOddsOrForAFriend()
    {
        Assert.True(LawfulRules.ShouldHunt(sidePower: 90, outlawPower: 100, outlawOnFriend: false, antiPk: true));
        Assert.False(LawfulRules.ShouldHunt(sidePower: 50, outlawPower: 100, outlawOnFriend: false, antiPk: true));
        Assert.True(LawfulRules.ShouldHunt(sidePower: 50, outlawPower: 100, outlawOnFriend: true, antiPk: false));
    }

    [Fact]
    public void ShouldHunt_AnyOtherBlueLetsARedPassUnlessFarStronger()
    {
        // Every passing blue drew on every red at 0.8: 1,879 reds that had hurt no one died in a night.
        Assert.False(LawfulRules.ShouldHunt(sidePower: 140, outlawPower: 100, outlawOnFriend: false, antiPk: false));
        Assert.True(LawfulRules.ShouldHunt(sidePower: 150, outlawPower: 100, outlawOnFriend: false, antiPk: false));
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    public void BecameCrime_OnlyALawfulCastThatTurnedCriminal(bool crimeAtCast, bool crimeNow, bool expected) =>
        Assert.Equal(expected, LawfulRules.BecameCrime(crimeAtCast, crimeNow));
}
