using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RecallOutRulesTests
{
    private static readonly int RecallWordsMs = CastTiming.CastDelayMs(RecallOutRules.RecallCircle, slowedByProtection: false);

    [Fact]
    public void ContactBroken_WhenNoBlowCanLandBeforeTheWordsEnd()
    {
        Assert.True(RecallOutRules.ContactBroken(CastTiming.NoBlowMs, slowedByProtection: false));
        Assert.True(RecallOutRules.ContactBroken(RecallWordsMs + CastTiming.BlowMarginMs, slowedByProtection: false));
        Assert.False(RecallOutRules.ContactBroken(RecallWordsMs, slowedByProtection: false));
    }

    [Fact]
    public void ContactBroken_ProtectionSlowsTheWords()
    {
        var soonestBlowMs = RecallWordsMs + CastTiming.BlowMarginMs;

        Assert.False(RecallOutRules.ContactBroken(soonestBlowMs, slowedByProtection: true));
    }

    [Theory]
    [InlineData(TravelSpells.HeatWhy, true)]
    [InlineData(TravelSpells.FightingWhy, true)]
    [InlineData(TravelSpells.ManaWhy, true)]
    [InlineData(TravelSpells.NoMeansWhy, false)]
    [InlineData(RecallRules.NoRuneWhy, false)]
    [InlineData(null, false)]
    public void TriesAgain_OnlyAfterARefusalThatPasses(string whyNot, bool again) =>
        Assert.Equal(again, RecallOutRules.TriesAgain(whyNot));

    [Fact]
    public void HidesOut_OnlyWithHidingEnoughToStayHidden()
    {
        Assert.True(RecallOutRules.HidesOut(RecallOutRules.HideOutMinHiding));
        Assert.False(RecallOutRules.HidesOut(RecallOutRules.HideOutMinHiding - 1));
    }
}
