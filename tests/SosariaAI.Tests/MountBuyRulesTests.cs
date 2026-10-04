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
}
