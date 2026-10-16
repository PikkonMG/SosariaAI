using SosariaAI.Combat;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RecallOutRulesTests
{
    // MagerySpell.GetCastDelay before AOS: 0.5 + 0.25 * (4 - 1) seconds for the fourth circle.
    private const int PreAosRecallWordsMs = 1250;

    // MagerySpell.CastDelayBase from AOS on: (3 + 4 - 1) * 0.25 seconds for the fourth circle.
    private const int AosRecallWordsMs = 1500;

    [Fact]
    public void ContactBroken_WhenNoBlowCanLandBeforeTheWordsEnd()
    {
        Assert.True(RecallOutRules.ContactBroken(CastTiming.NoBlowMs, aos: false, slowedByProtection: false));
        Assert.True(RecallOutRules.ContactBroken(PreAosRecallWordsMs + CastTiming.BlowMarginMs, aos: false, slowedByProtection: false));
        Assert.False(RecallOutRules.ContactBroken(PreAosRecallWordsMs + CastTiming.BlowMarginMs - 1, aos: false, slowedByProtection: false));
    }

    [Fact]
    public void ContactBroken_AosWordsTakeATickLonger()
    {
        var preAosClear = PreAosRecallWordsMs + CastTiming.BlowMarginMs;

        Assert.False(RecallOutRules.ContactBroken(preAosClear, aos: true, slowedByProtection: false));
        Assert.True(RecallOutRules.ContactBroken(AosRecallWordsMs + CastTiming.BlowMarginMs, aos: true, slowedByProtection: false));
    }

    [Fact]
    public void ContactBroken_ProtectionSlowsTheWordsOnlyFromAos()
    {
        var aosClear = AosRecallWordsMs + CastTiming.BlowMarginMs;
        var preAosClear = PreAosRecallWordsMs + CastTiming.BlowMarginMs;

        Assert.False(RecallOutRules.ContactBroken(aosClear, aos: true, slowedByProtection: true));
        Assert.True(RecallOutRules.ContactBroken(preAosClear, aos: false, slowedByProtection: true));
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
