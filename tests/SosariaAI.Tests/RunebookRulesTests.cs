using Server;
using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class RunebookRulesTests
{
    private const double Mage = 80;
    private const double Warrior = 0;
    private const int FullBook = 6;
    private const int RichTarget = SupplyRules.RichRecallTarget;

    [Fact]
    public void HowToRecall_AMageCastsTheSpellOnTheEntry()
    {
        Assert.Equal(BookCast.Spell, RunebookRules.HowToRecall(canCastSpell: true, charges: 0, canRecharge: false, Mage));
    }

    [Fact]
    public void HowToRecall_ASwordWithSomeMageryUsesACharge()
    {
        var magery = RunebookRules.ChargeMinMagery;

        Assert.Equal(BookCast.Charge, RunebookRules.HowToRecall(canCastSpell: false, charges: 1, canRecharge: false, magery));
        Assert.Equal(BookCast.Charge, RunebookRules.HowToRecall(canCastSpell: false, charges: 0, canRecharge: true, magery));
    }

    [Fact]
    public void HowToRecall_AShakyCasterUsesTheChargeOverTheSpell()
    {
        const double dexxer = 33;

        Assert.Equal(BookCast.Charge, RunebookRules.HowToRecall(canCastSpell: true, charges: 1, canRecharge: false, dexxer));
        Assert.Equal(BookCast.Spell, RunebookRules.HowToRecall(canCastSpell: true, charges: 0, canRecharge: false, dexxer));
        Assert.Equal(
            BookCast.Spell,
            RunebookRules.HowToRecall(canCastSpell: true, charges: 1, canRecharge: true, RunebookRules.ChargePreferredBelowMagery)
        );
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void WantsBook_ARecallerWithoutOne(bool travels, bool hasBook, bool wants) =>
        Assert.Equal(wants, RunebookRules.WantsBook(travels, hasBook));

    [Fact]
    public void HowToRecall_NoMageryOrNoChargeWalks()
    {
        Assert.Equal(BookCast.None, RunebookRules.HowToRecall(canCastSpell: false, charges: FullBook, canRecharge: true, Warrior));
        Assert.Equal(BookCast.None, RunebookRules.HowToRecall(canCastSpell: false, charges: 0, canRecharge: false, Mage));
    }

    [Fact]
    public void RechargesFirst_OnlyAnEmptyBookOnACharge()
    {
        Assert.True(RunebookRules.RechargesFirst(BookCast.Charge, 0));
        Assert.False(RunebookRules.RechargesFirst(BookCast.Charge, 1));
        Assert.False(RunebookRules.RechargesFirst(BookCast.Spell, 0));
    }

    [Fact]
    public void HasRoom_UpToSixteenEntries()
    {
        Assert.True(RunebookRules.HasRoom(RunebookRules.EntryCap - 1));
        Assert.False(RunebookRules.HasRoom(RunebookRules.EntryCap));
    }

    [Theory]
    [InlineData(10, FullBook, RichTarget, 6)]
    [InlineData(4, FullBook, RichTarget, 0)]
    [InlineData(5, FullBook, 0, 3)]
    [InlineData(RunebookRules.SpareRecallScrolls, 0, 0, 0)]
    [InlineData(0, FullBook, RichTarget, 0)]
    public void ScrollSurplus_BanksWhatTheBookAndTheSparesLeaveOver(int loose, int charges, int target, int surplus) =>
        Assert.Equal(surplus, RunebookRules.ScrollSurplus(loose, charges, target));

    [Theory]
    [InlineData(true, false, 0, true)]
    [InlineData(false, true, 2, true)]
    [InlineData(false, true, 0, false)]
    [InlineData(false, false, 3, false)]
    public void KeepsBook_ABookOrAnEstablishedTravelerWithRunes(bool hasBook, bool established, int loose, bool keeps) =>
        Assert.Equal(keeps, RunebookRules.KeepsBook(hasBook, established, loose));

    [Fact]
    public void RecalledFromBookLine_IsEasyToCount()
    {
        Assert.Equal(
            "recalled from runebook at (652, 820, 0) to (1434, 1699, 0)",
            RunebookRules.RecalledFromBookLine(new Point3D(652, 820, 0), new Point3D(1434, 1699, 0))
        );
    }
    [Theory]
    [InlineData(true, true, 0, Warrior + RunebookRules.ChargeMinMagery, true)]
    [InlineData(false, true, 0, Mage, false)]
    [InlineData(true, false, 0, Mage, false)]
    [InlineData(true, true, 1, Mage, false)]
    [InlineData(true, true, 0, RunebookRules.ChargeMinMagery - 1, false)]
    public void GetsFreeHomeCharge_ARaisedRedWithAnEmptyBookItCanUse(bool red, bool hasBook, int charges, double magery, bool gets) =>
        Assert.Equal(gets, RunebookRules.GetsFreeHomeCharge(red, hasBook, charges, magery));

    [Fact]
    public void FreeHomeCharges_AreEnoughForTheOneRecallHome()
    {
        Assert.InRange(RunebookRules.FreeHomeCharges, 1, FullBook);
    }
}
