using System;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class FloorTradeRulesTests
{
    private static readonly DateTime Start = new(2026, 9, 28, 20, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(SkillKinds.BankDeposit)]
    [InlineData(SkillKinds.BankCrowd)]
    [InlineData(SkillKinds.Loiter)]
    [InlineData(SkillKinds.Rest)]
    [InlineData(SkillKinds.Taste)]
    [InlineData(SkillKinds.Tavern)]
    public void IsIdleOnFloor_APersonStandingAboutAnswers(string skillKind) =>
        Assert.True(FloorTradeRules.IsIdleOnFloor(skillKind));

    [Theory]
    [InlineData(SkillKinds.Travel)]
    [InlineData(SkillKinds.Hunt)]
    [InlineData(SkillKinds.BankShop)]
    [InlineData(SkillKinds.VendorSell)]
    [InlineData(null)]
    public void IsIdleOnFloor_APersonOnItsWayOrShoppingItselfDoesNot(string skillKind) =>
        Assert.False(FloorTradeRules.IsIdleOnFloor(skillKind));

    [Fact]
    public void LookDue_OnceAGap()
    {
        Assert.False(FloorTradeRules.LookDue(Start + FloorTradeRules.AnswerGap - TimeSpan.FromSeconds(1), Start));
        Assert.True(FloorTradeRules.LookDue(Start + FloorTradeRules.AnswerGap, Start));
    }
}
