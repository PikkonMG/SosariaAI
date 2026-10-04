using Server;
using Server.Items;
using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class AppraisalTests
{
    private const int LowRoll = 0;
    private const int VanquishingLevel = 5;
    private const int MandrakeCount = 200;

    static AppraisalTests() => Timer.Init(0);

    public AppraisalTests() => TestMap.EnsureInternal();

    [Fact]
    public void TryRead_EraShorthandAndMakersMark()
    {
        Assert.True(Appraisal.TryRead(TradeParser.Words("wts GM hally 5k"), out var claim, out _));

        Assert.Equal("polearm", claim.Row.Key);
        Assert.True(claim.Exceptional);
        Assert.Equal(Appraisal.NoMagic, claim.MagicLevel);
    }

    [Fact]
    public void TryRead_MagicLevelFromTheWords()
    {
        Assert.True(Appraisal.TryRead(TradeParser.Words("wts vanq katana"), out var weapon, out _));
        Assert.True(Appraisal.TryRead(TradeParser.Words("invul plate chest"), out var armor, out _));

        Assert.Equal(VanquishingLevel, weapon.MagicLevel);
        Assert.Equal(VanquishingLevel, armor.MagicLevel);
        Assert.Equal("plate", armor.Row.Key);
    }

    [Fact]
    public void TryRead_ACountBeforeTheGoods()
    {
        Assert.True(Appraisal.TryRead(TradeParser.Words("wts 200 mandrake 900"), out var claim, out _));

        Assert.Equal(Appraisal.ReagentsKey, claim.Row.Key);
        Assert.Equal(MandrakeCount, claim.Amount);
        Assert.Equal(MandrakeCount, claim.Lot);
    }

    [Theory]
    [InlineData("wts heavy xbow", "bow")]
    [InlineData("wts bolt of cloth", "cloth")]
    [InlineData("wts bolts", Appraisal.BoltsKey)]
    [InlineData("wts bm", Appraisal.ReagentsKey)]
    [InlineData("wts ringmail tunic", "ring")]
    [InlineData("wts kryss", "fencing")]
    public void TryRead_TheLongestPhraseWins(string line, string key)
    {
        Assert.True(Appraisal.TryRead(TradeParser.Words(line), out var claim, out _));
        Assert.Equal(key, claim.Row.Key);
    }

    [Fact]
    public void TryRead_NothingKnown() => Assert.False(Appraisal.TryRead(TradeParser.Words("hello there"), out _, out _));

    [Fact]
    public void Value_MarkAndMagicClimbAndStacksScale()
    {
        var row = Appraisal.RowByKey("polearm");
        var plain = Appraisal.Value(row, 1, false, Appraisal.NoMagic, LowRoll);
        var exceptional = Appraisal.Value(row, 1, true, Appraisal.NoMagic, LowRoll);
        var vanquishing = Appraisal.Value(row, 1, false, VanquishingLevel, LowRoll);
        var regs = Appraisal.RowByKey(Appraisal.ReagentsKey);

        Assert.True(exceptional > plain);
        Assert.True(vanquishing > exceptional);
        Assert.True(Appraisal.Value(regs, 100, false, Appraisal.NoMagic, LowRoll) >
                    Appraisal.Value(regs, 10, false, Appraisal.NoMagic, LowRoll));
    }

    [Fact]
    public void RowOf_RealItems()
    {
        Assert.Equal("polearm", Appraisal.RowOf(new Halberd((Serial)0x7E80)).Key);
        Assert.Equal("fencing", Appraisal.RowOf(new Kryss((Serial)0x7E81)).Key);
        Assert.Equal("shield", Appraisal.RowOf(new HeaterShield((Serial)0x7E82)).Key);
        Assert.Equal("plate", Appraisal.RowOf(new PlateChest((Serial)0x7E83)).Key);
        Assert.Equal(Appraisal.ReagentsKey, Appraisal.RowOf(new BlackPearl((Serial)0x7E84)).Key);
        Assert.Same(Appraisal.Other, Appraisal.RowOf(new Candle((Serial)0x7E85)));
    }

    [Fact]
    public void NounOf_SaysTheMakersMarkAndMagic()
    {
        var gm = new Halberd((Serial)0x7E86) { Quality = WeaponQuality.Exceptional, Amount = 1 };
        var vanq = new Katana((Serial)0x7E87) { DamageLevel = WeaponDamageLevel.Vanq, Amount = 1 };

        Assert.Equal("GM halberd", Appraisal.NounOf(gm));
        Assert.Equal("vanq katana", Appraisal.NounOf(vanq));
    }

    [Fact]
    public void Matches_TheItemMustBeWhatWasDescribed()
    {
        Assert.True(Appraisal.TryRead(TradeParser.Words("wts gm halberd"), out var gmClaim, out _));
        var plain = new Halberd((Serial)0x7E88) { Amount = 1 };
        var gm = new Halberd((Serial)0x7E89) { Quality = WeaponQuality.Exceptional, Amount = 1 };
        var katana = new Katana((Serial)0x7E8A) { Quality = WeaponQuality.Exceptional, Amount = 1 };

        Assert.False(gmClaim.Matches(plain));
        Assert.True(gmClaim.Matches(gm));
        Assert.False(gmClaim.Matches(katana));
    }

    [Fact]
    public void Matches_AStackMustBeTheAgreedCount()
    {
        var claim = new GoodsClaim(Appraisal.RowByKey(Appraisal.ReagentsKey), 100, false, Appraisal.NoMagic);

        Assert.True(claim.Matches(new BlackPearl((Serial)0x7E8B) { Amount = 100 }));
        Assert.False(claim.Matches(new BlackPearl((Serial)0x7E8C) { Amount = 60 }));
    }
}
