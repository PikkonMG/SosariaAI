using Server;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Navigation;
using Xunit;

namespace SosariaAI.Tests;

public class VendorSellRulesTests
{
    [Fact]
    public void KeepItem_ToolsGoldSuppliesKitAndSpares_AreKept()
    {
        Assert.True(VendorSellRules.KeepItem(isTool: true, isGold: false, isSupply: false, isKitPiece: false, isSpare: false));
        Assert.True(VendorSellRules.KeepItem(isTool: false, isGold: true, isSupply: false, isKitPiece: false, isSpare: false));
        Assert.True(VendorSellRules.KeepItem(isTool: false, isGold: false, isSupply: true, isKitPiece: false, isSpare: false));
        Assert.True(VendorSellRules.KeepItem(isTool: false, isGold: false, isSupply: false, isKitPiece: true, isSpare: false));
        Assert.True(VendorSellRules.KeepItem(isTool: false, isGold: false, isSupply: false, isKitPiece: false, isSpare: true));
        Assert.False(VendorSellRules.KeepItem(isTool: false, isGold: false, isSupply: false, isKitPiece: false, isSpare: false));
    }

    [Fact]
    public void DestinationFor_PicksTheShopThatBuysTheGoods()
    {
        Assert.Equal(
            ShopFinder.SmithToken,
            VendorSellRules.DestinationFor(hasOre: true, hasIngots: false, hasLogs: true, hasFish: true)
        );
        Assert.Equal(
            ShopFinder.CarpenterToken,
            VendorSellRules.DestinationFor(hasOre: false, hasIngots: false, hasLogs: true, hasFish: true)
        );
        Assert.Equal(
            VendorSellRules.FishermanToken,
            VendorSellRules.DestinationFor(hasOre: false, hasIngots: false, hasLogs: false, hasFish: true)
        );
        Assert.Equal(
            ShopFinder.SmithToken,
            VendorSellRules.DestinationFor(hasOre: true, hasIngots: false, hasLogs: false, hasFish: false)
        );
        Assert.Equal(
            ShopFinder.CarpenterToken,
            VendorSellRules.DestinationFor(hasOre: false, hasIngots: false, hasLogs: true, hasFish: false)
        );
        Assert.Null(VendorSellRules.DestinationFor(false, false, false, false));
    }

    [Fact]
    public void AfterCounter_KeepsSellingOnlyWhileTheCounterTakesGoods()
    {
        // A seller kept offering a provisioner gems it would not buy and wrote "found no
        // goods" 317 times: a counter that took nothing sends the rest to the next shop.
        Assert.Equal(CounterNext.KeepSelling, VendorSellRules.AfterCounter(soldThisTurn: true, goodsLeft: true));
        Assert.Equal(CounterNext.NextShop, VendorSellRules.AfterCounter(soldThisTurn: false, goodsLeft: true));
        Assert.Equal(CounterNext.Finish, VendorSellRules.AfterCounter(soldThisTurn: true, goodsLeft: false));
        Assert.Equal(CounterNext.Finish, VendorSellRules.AfterCounter(soldThisTurn: false, goodsLeft: false));
    }

    [Fact]
    public void MaySellAt_OnlyWithinStallRange()
    {
        // A miner on the mountain sold to a smith 40 tiles below, once a second, for
        // fifteen minutes. A sale needs the seller at the stall.
        var stall = CareerSettings.DefaultVendorSearchRange;
        Assert.True(VendorSellRules.MaySellAt(0, stall));
        Assert.True(VendorSellRules.MaySellAt(stall, stall));
        Assert.False(VendorSellRules.MaySellAt(stall + 1, stall));
        Assert.False(VendorSellRules.MaySellAt(VendorSellRules.TownVendorScanRange, stall));
    }

    [Fact]
    public void MayWalkToForge_OnceATrip()
    {
        // A walk to a forge behind the Minoc smithy counter ended out of its reach, and the
        // next walk to it ended at once, every tick, for ten minutes.
        var forge = new Point3D(2530, 570, 0);
        var other = new Point3D(2560, 530, 15);

        Assert.True(VendorSellRules.MayWalkToForge(forge, []));
        Assert.False(VendorSellRules.MayWalkToForge(forge, [forge]));
        Assert.True(VendorSellRules.MayWalkToForge(other, [forge]));
        Assert.False(VendorSellRules.MayWalkToForge(forge, [other, forge]));
    }

    [Fact]
    public void VendorRules_SameTownAndHarvestShops()
    {
        Assert.True(VendorSellRules.TownVendorScanRange > CareerSettings.DefaultVendorSearchRange);
        Assert.True(VendorSellRules.VendorInSameTown(true, true));
        Assert.False(VendorSellRules.VendorInSameTown(true, false));
        Assert.False(VendorSellRules.VendorInSameTown(false, true));
        Assert.True(VendorSellRules.IsHarvestShopName(VendorSellRules.CarpenterName));
        Assert.True(VendorSellRules.IsHarvestShopName(VendorSellRules.FishermanName));
        Assert.True(VendorSellRules.IsHarvestShopName(VendorSellRules.BlacksmithName));
        Assert.True(VendorSellRules.IsHarvestShopName(VendorSellRules.BakerName));
        Assert.False(VendorSellRules.IsHarvestShopName("Troll"));
    }

    [Fact]
    public void MatchesNeededShop_BakerIsNotEnoughForLogs()
    {
        var token = VendorSellRules.DestinationFor(hasOre: false, hasIngots: false, hasLogs: true, hasFish: false);
        Assert.Equal(ShopFinder.CarpenterToken, token);
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.CarpenterName, token));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.TinkerName, token));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.BakerName, token));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.ButcherName, token));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.ProvisionerName, token));
        Assert.False(VendorSellRules.MatchesNeededShop(null, token));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.CarpenterName, null));
    }

    [Fact]
    public void MatchesNeededShop_SmithAndFisherman()
    {
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.BlacksmithName, ShopFinder.SmithToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.SmithName, ShopFinder.SmithToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.FishermanName, VendorSellRules.FishermanToken));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.CarpenterName, ShopFinder.SmithToken));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.BakerName, VendorSellRules.FishermanToken));
        Assert.False(VendorSellRules.MatchesNeededShop("Troll", ShopFinder.CarpenterToken));
    }

    [Fact]
    public void MatchesNeededShop_PawnShops()
    {
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.TailorName, ShopFinder.TailorToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.TannerName, ShopFinder.TailorToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.WeaverName, ShopFinder.TailorToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.JewelerName, ShopFinder.JewelerToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.AlchemistName, ShopFinder.AlchemistToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.HerbalistName, ShopFinder.AlchemistToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.MageName, ShopFinder.MageToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.ProvisionerName, ShopFinder.ProvisionerToken));
        Assert.True(VendorSellRules.MatchesNeededShop(VendorSellRules.VarietyDealerName, ShopFinder.ProvisionerToken));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.JewelerName, ShopFinder.TailorToken));
        Assert.False(VendorSellRules.MatchesNeededShop(VendorSellRules.BakerName, ShopFinder.JewelerToken));
    }

    [Fact]
    public void IsSellableHarvest_OnlyWhatAShopBuys()
    {
        // The carpenter buys plain logs only. Oak logs were offered for sale three times,
        // refused, and carried around all day.
        Assert.True(VendorSellRules.IsSellableHarvest(VendorSellRules.LogType));
        Assert.True(VendorSellRules.IsSellableHarvest(VendorSellRules.FishType));
        Assert.True(VendorSellRules.IsSellableHarvest(VendorSellRules.IronIngotType));
        Assert.True(VendorSellRules.IsSellableHarvest(VendorSellRules.IronOreType));
        Assert.False(VendorSellRules.IsSellableHarvest("OakLog"));
        Assert.False(VendorSellRules.IsSellableHarvest("DullCopperIngot"));
        Assert.False(VendorSellRules.IsSellableHarvest(null));
    }


    [Fact]
    public void ShopWalkRange_EndsTheWalkInsideTheStallWithRoomForTheLegSlackAndAVendorStep()
    {
        // Sellers walked to eight tiles of a vendor, the last leg stopped one tile past its
        // mark, and "no vendor within 8 tiles" came nine tiles from every shop, 198 times.
        var stall = CareerSettings.DefaultVendorSearchRange;
        var walk = VendorSellRules.ShopWalkRange(stall);

        Assert.True(walk + CharactersFile.DefaultGoToRange + VendorSellRules.VendorStepTiles <= stall);
        Assert.Equal(NavLimits.ShopArrivalRange, walk);
    }

    [Fact]
    public void ShopWalkRange_NeverDropsBelowOneTile()
    {
        Assert.Equal(VendorSellRules.MinShopWalkRange, VendorSellRules.ShopWalkRange(1));
        Assert.Equal(VendorSellRules.MinShopWalkRange, VendorSellRules.ShopWalkRange(0));
    }

    [Fact]
    public void MayWalkToCounter_ARedOutOfTheGuardsSkipsAGuardedCounter()
    {
        Assert.False(VendorSellRules.MayWalkToCounter(keepsOffGuards: true, counterUnderGuards: true));
        Assert.True(VendorSellRules.MayWalkToCounter(keepsOffGuards: true, counterUnderGuards: false));
        Assert.True(VendorSellRules.MayWalkToCounter(keepsOffGuards: false, counterUnderGuards: true));
    }

    [Fact]
    public void MayApproachAgain_GivesAWanderingVendorOneMoreWalk()
    {
        Assert.True(VendorSellRules.MayApproachAgain(0));
        Assert.True(VendorSellRules.MayApproachAgain(VendorSellRules.MaxVendorApproaches - 1));
        Assert.False(VendorSellRules.MayApproachAgain(VendorSellRules.MaxVendorApproaches));
    }
}
