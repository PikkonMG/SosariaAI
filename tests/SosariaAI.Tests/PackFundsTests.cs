using SosariaAI.Economy;
using Xunit;

namespace SosariaAI.Tests;

public class PackFundsTests
{
    private const int SmallOrder = 300;
    private const int RichBank = 100000;

    [Fact]
    public void MayPay_SmallOrderComesFromThePackOnly()
    {
        Assert.True(PackFunds.MayPay(SmallOrder, 0, SmallOrder));
        Assert.False(PackFunds.MayPay(SmallOrder - 1, RichBank, SmallOrder));
    }

    [Fact]
    public void MayPay_LargeOrderMayComeFromTheBank()
    {
        Assert.True(PackFunds.MayPay(0, RichBank, PackFunds.VendorBankPayMin));
        Assert.False(PackFunds.MayPay(0, PackFunds.VendorBankPayMin - 1, PackFunds.VendorBankPayMin));
    }

    [Fact]
    public void MayPay_NothingForAFreeOrder() =>
        Assert.False(PackFunds.MayPay(RichBank, RichBank, 0));
}
