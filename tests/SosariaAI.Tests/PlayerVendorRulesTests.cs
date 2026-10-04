using System;
using SosariaAI.Behaviour;
using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class PlayerVendorRulesTests
{
    [Fact]
    public void BeingAway_HouseStillStands() =>
        Assert.True(HouseRules.Stands(houseSerial: 12, atFeet: false, serialLive: true));

    [Fact]
    public void MayPlace_NeedsAHouseAndNoVendor()
    {
        Assert.True(PlayerVendorRules.MayPlace(houseSerial: 12, vendorCount: 0));
        Assert.False(PlayerVendorRules.MayPlace(HouseRules.NoHouseSerial, vendorCount: 0));
        Assert.False(PlayerVendorRules.MayPlace(houseSerial: 12, vendorCount: 1));
    }

    [Fact]
    public void OwnedVendor_ZeroSerial_IsNull() =>
        Assert.Null(PlayerVendorRules.OwnedVendor(PlayerVendorRules.NoVendorSerial));

    [Fact]
    public void Earnings_LeaveDaysOfUpkeepWithTheVendor()
    {
        const int Charge = 20;
        const int Held = 1000;

        Assert.Equal(Held - Charge * PlayerVendorRules.UpkeepDaysKept, PlayerVendorRules.Earnings(Held, Charge));
        Assert.Equal(0, PlayerVendorRules.Earnings(Charge, Charge));
    }

    [Fact]
    public void OnSale_APricedListingUpForAMinute()
    {
        var put = new DateTime(2026, 9, 28, 20, 0, 0, DateTimeKind.Utc);
        const int Price = 400;

        Assert.True(PlayerVendorRules.OnSale(valid: true, Price, put, put + PlayerVendorRules.FreshStockWait));
        Assert.False(PlayerVendorRules.OnSale(valid: true, Price, put, put));
        Assert.False(PlayerVendorRules.OnSale(valid: false, Price, put, put + PlayerVendorRules.FreshStockWait));
        Assert.False(PlayerVendorRules.OnSale(valid: true, price: 0, put, put + PlayerVendorRules.FreshStockWait));
    }

    [Fact]
    public void WouldPay_ThePriceWithinPurseAndCeiling()
    {
        const int Price = 400;

        Assert.True(PlayerVendorMall.WouldPay(Price, purse: Price, ceiling: Price));
        Assert.False(PlayerVendorMall.WouldPay(Price, purse: Price - 1, ceiling: Price));
        Assert.False(PlayerVendorMall.WouldPay(Price, purse: Price, ceiling: Price - 1));
    }

    [Fact]
    public void LooksAtVendors_HalfTheBrowses()
    {
        Assert.True(PlayerVendorMall.LooksAtVendors(PlayerVendorMall.BrowseVendorPercent - 1));
        Assert.False(PlayerVendorMall.LooksAtVendors(PlayerVendorMall.BrowseVendorPercent));
    }
}
