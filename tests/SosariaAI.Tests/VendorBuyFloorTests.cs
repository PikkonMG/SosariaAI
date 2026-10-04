using SosariaAI.Economy;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class VendorBuyFloorTests
{
    private const int Short = 50;
    private const int BandagePrice = 5;
    private const int NotStocked = 0;
    private const int BigBank = 10000;

    [Fact]
    public void AsksTheFloor_OnlyASupplyErrandAtABareShelfThatIsStillShort()
    {
        Assert.True(VendorBuySkill.AsksTheFloor(suppliesWanted: true, vendorAtShop: true, Short));
        Assert.False(VendorBuySkill.AsksTheFloor(suppliesWanted: false, vendorAtShop: true, Short));
        Assert.False(VendorBuySkill.AsksTheFloor(suppliesWanted: true, vendorAtShop: false, Short));
        Assert.False(VendorBuySkill.AsksTheFloor(suppliesWanted: true, vendorAtShop: true, shortUnits: 0));
    }

    [Theory]
    [InlineData(BandagePrice, BandagePrice, 0, true)]
    [InlineData(BandagePrice, BandagePrice - 1, 0, false)]
    [InlineData(BandagePrice, BandagePrice - 1, BigBank, false)]
    [InlineData(NotStocked, BigBank, BigBank, false)]
    [InlineData(PackFunds.VendorBankPayMin, 0, PackFunds.VendorBankPayMin, true)]
    public void PaysForOne_OnlyWhenTheVendorCanTakeOneUnitsPrice(int cheapestPrice, int packGold, int bankGold, bool pays) =>
        // Connor, with 4 gold, walked to the healer for 5-gold bandages and failed at the counter.
        Assert.Equal(pays, VendorBuySkill.PaysForOne(cheapestPrice, packGold, bankGold));
}
