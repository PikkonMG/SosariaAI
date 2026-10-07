using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class ItemIdRulesTests
{
    private const double NoSkill = 0;
    private const double Grandmaster = 100;
    private const int NoGold = 0;

    [Fact]
    public void IdentifiesOwn_FromTheSelfLine()
    {
        Assert.False(ItemIdRules.IdentifiesOwn(ItemIdRules.SelfIdMinSkill - 1));
        Assert.True(ItemIdRules.IdentifiesOwn(ItemIdRules.SelfIdMinSkill));
    }

    [Fact]
    public void OffersService_OnlyAboveThePeopleWhoDoTheirOwn()
    {
        Assert.False(ItemIdRules.OffersService(ItemIdRules.ServiceMinSkill - 1));
        Assert.True(ItemIdRules.OffersService(ItemIdRules.ServiceMinSkill));
        Assert.True(ItemIdRules.ServiceMinSkill >= ItemIdRules.SelfIdMinSkill);
    }

    [Theory]
    [InlineData(false, NoSkill, true, ItemIdRules.IdFee, true)]
    [InlineData(true, NoSkill, true, ItemIdRules.IdFee, false)]
    [InlineData(false, Grandmaster, true, ItemIdRules.IdFee, false)]
    [InlineData(false, NoSkill, false, ItemIdRules.IdFee, false)]
    [InlineData(false, NoSkill, true, ItemIdRules.IdFee - 1, false)]
    [InlineData(false, NoSkill, true, NoGold, false)]
    public void IsCustomer_SomeoneElseWithoutTheSkill_AnUnnamedPiece_AndTheFee(
        bool isServer,
        double customerSkill,
        bool hasUnidentified,
        int purse,
        bool customer
    ) =>
        Assert.Equal(customer, ItemIdRules.IsCustomer(isServer, customerSkill, hasUnidentified, purse));

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ShowsUnidentified_MagicNobodyNamed(bool magic, bool identified, bool shows) =>
        Assert.Equal(shows, ItemIdRules.ShowsUnidentified(magic, identified));
}
