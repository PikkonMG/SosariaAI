using SosariaAI.Skills;
using Xunit;

namespace SosariaAI.Tests;

public class MountBuyRulesTests
{
    [Fact]
    public void MayBuy_RiderlessCharacterWithRoom_Buys() =>
        Assert.True(MountBuyRules.MayBuy(mounted: false, ownsMountNearby: false, followers: 0, controlSlots: 1, followersMax: 5));

    [Fact]
    public void MayBuy_AlreadyMounted_Skips() =>
        Assert.False(MountBuyRules.MayBuy(mounted: true, ownsMountNearby: false, followers: 0, controlSlots: 1, followersMax: 5));

    [Fact]
    public void MayBuy_MountStandingNearby_Skips() =>
        Assert.False(MountBuyRules.MayBuy(mounted: false, ownsMountNearby: true, followers: 0, controlSlots: 1, followersMax: 5));

    [Fact]
    public void MayBuy_FullFollowers_Skips() =>
        Assert.False(MountBuyRules.MayBuy(mounted: false, ownsMountNearby: false, followers: 5, controlSlots: 1, followersMax: 5));

    [Fact]
    public void MayBuy_LastOpenSlot_Buys() =>
        Assert.True(MountBuyRules.MayBuy(mounted: false, ownsMountNearby: false, followers: 4, controlSlots: 1, followersMax: 5));

    [Theory]
    [InlineData(MountBuyRules.HorsePrice, 0, false, true)]
    [InlineData(0, MountBuyRules.HorsePrice, false, true)]
    [InlineData(0, MountBuyRules.HorsePrice, true, false)]
    [InlineData(MountBuyRules.HorsePrice, 0, true, true)]
    [InlineData(MountBuyRules.HorsePrice - 1, 0, false, false)]
    public void CanFund_PackOrBankButARedOnlyFromItsPack(int pack, int bank, bool red, bool funds) =>
        Assert.Equal(funds, MountBuyRules.CanFund(pack, bank, red));
}
