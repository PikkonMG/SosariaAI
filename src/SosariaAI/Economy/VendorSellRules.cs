using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Economy;

/// <summary>What a seller does after a turn at a shop counter.</summary>
public enum CounterNext
{
    /// <summary>The counter took goods and more are left: offer them again.</summary>
    KeepSelling,

    /// <summary>The counter took nothing and goods are left: another shop may buy them.</summary>
    NextShop,

    /// <summary>Nothing is left to sell.</summary>
    Finish
}

/// <summary>
/// What a worker may sell to an NPC, and which shop to walk to. Pure. No world objects.
/// </summary>
public static class VendorSellRules
{
    public const int MaxStacks = 500;
    public const int TownVendorScanRange = 180;
    public const string FishermanToken = "fisherman";
    public const string CarpenterName = "Carpenter";
    public const string TinkerName = "Tinker";
    public const string FishermanName = "Fisherman";
    public const string BlacksmithName = "Blacksmith";
    public const string SmithName = "Smith";
    public const string ButcherName = "Butcher";
    public const string BakerName = "Baker";
    public const string ProvisionerName = "Provisioner";
    public const string TailorName = "Tailor";
    public const string TannerName = "Tanner";
    public const string WeaverName = "Weaver";
    public const string LeatherWorkerName = "LeatherWorker";
    public const string JewelerName = "Jeweler";
    public const string AlchemistName = "Alchemist";
    public const string HerbalistName = "Herbalist";
    public const string MageName = "Mage";
    public const string VarietyDealerName = "VarietyDealer";
    public const string LogType = "Log";
    public const string FishType = "Fish";
    public const string IronIngotType = "IronIngot";
    public const string IronOreType = "IronOre";

    /// <summary>The step a vendor takes about its shop while the seller walks the last tiles.</summary>
    public const int VendorStepTiles = 1;

    /// <summary>A walk to a vendor stops no farther out than this.</summary>
    public const int MinShopWalkRange = 1;

    /// <summary>Walks to one vendor in a trip: the first, and one more when it stepped off the stall.</summary>
    public const int MaxVendorApproaches = 2;

    /// <summary>
    /// What never goes on the counter: the working tools, the coin purse, the supplies the
    /// carrier burns, kit pieces, and the stock or spare weapon it keeps for itself.
    /// </summary>
    public static bool KeepItem(
        bool isTool,
        bool isGold,
        bool isSupply,
        bool isKitPiece,
        bool isSpare
    ) => isTool || isGold || isSupply || isKitPiece || isSpare;

    /// <summary>
    /// True for the harvest a town shop buys: plain logs, fish and iron ingots, and iron
    /// ore, which a smith's forge turns into iron ingots. Oak logs or copper ingots are not
    /// on any shop's list, so a worker banks them.
    /// </summary>
    public static bool IsSellableHarvest(string typeName) =>
        typeName is LogType or FishType or IronIngotType or IronOreType;

    /// <summary>
    /// What a seller does after a turn at the counter. One trip dumps the whole load: selling
    /// one stack and starting VendorSell again every second filled the log at the mine. A
    /// counter that took nothing this turn will take nothing next turn either: a seller who
    /// kept offering a provisioner the gems it would not buy wrote "found no goods" 317 times
    /// and never left, so it takes the rest to the next shop that buys it.
    /// </summary>
    public static CounterNext AfterCounter(bool soldThisTurn, bool goodsLeft)
    {
        if (!goodsLeft)
        {
            return CounterNext.Finish;
        }

        return soldThisTurn ? CounterNext.KeepSelling : CounterNext.NextShop;
    }

    /// <summary>
    /// A sale happens at the stall. A miner on the mountain sold to a smith forty tiles
    /// below, once a second, and never walked down.
    /// </summary>
    public static bool MaySellAt(int distance, int stallRange) => distance <= stallRange;

    /// <summary>
    /// A red out of the guards never walks to a counter under them. The planner counted every
    /// shop in the leash, so a red with loot picked a sale that the walk then refused: 52 of 56
    /// "no shop in reach buys the goods" ends of one run were reds turned back at the guards.
    /// </summary>
    public static bool MayWalkToCounter(bool keepsOffGuards, bool counterUnderGuards) =>
        !keepsOffGuards || !counterUnderGuards;

    /// <summary>
    /// A forge is walked to once a trip. The walk to it ended with the seller still out of its
    /// reach, so another walk to it ends at once where the seller stands: Minoc sellers began
    /// that walk every tick for ten minutes at the smithy and never smelted or sold. Two such
    /// forges would trade the walk between them, so every forge walked to counts.
    /// </summary>
    public static bool MayWalkToForge(Point3D forge, IReadOnlyCollection<Point3D> walkedTo) =>
        walkedTo?.Contains(forge) != true;

    /// <summary>
    /// How near to a vendor the walk to its stall ends: inside the stall by the tile the
    /// last leg may overshoot and the step the vendor may take. Sellers walked to the edge
    /// of an eight-tile stall, stood nine tiles off, and failed "no vendor within 8 tiles"
    /// 198 times in half an hour.
    /// </summary>
    public static int ShopWalkRange(int stallRange) =>
        Math.Max(MinShopWalkRange, stallRange - CharactersFile.DefaultGoToRange - VendorStepTiles);

    /// <summary>True while the seller may walk to the same vendor again after it stepped off the stall.</summary>
    public static bool MayApproachAgain(int approaches) => approaches < MaxVendorApproaches;

    public static string DestinationFor(bool hasOre, bool hasIngots, bool hasLogs, bool hasFish)
    {
        if (hasOre || hasIngots)
        {
            return ShopFinder.SmithToken;
        }

        if (hasLogs)
        {
            return ShopFinder.CarpenterToken;
        }

        if (hasFish)
        {
            return FishermanToken;
        }

        return null;
    }

    public static bool MatchesNeededShop(string vendorTypeName, string token)
    {
        if (string.IsNullOrWhiteSpace(vendorTypeName) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.Equals(ShopFinder.CarpenterToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, CarpenterName) || ContainsToken(vendorTypeName, TinkerName);
        }

        if (token.Equals(FishermanToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, FishermanName);
        }

        if (token.Equals(ShopFinder.SmithToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, BlacksmithName) || ContainsToken(vendorTypeName, SmithName);
        }

        if (token.Equals(ShopFinder.TailorToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, TailorName) ||
                   ContainsToken(vendorTypeName, TannerName) ||
                   ContainsToken(vendorTypeName, WeaverName) ||
                   ContainsToken(vendorTypeName, LeatherWorkerName);
        }

        if (token.Equals(ShopFinder.JewelerToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, JewelerName);
        }

        if (token.Equals(ShopFinder.AlchemistToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, AlchemistName) || ContainsToken(vendorTypeName, HerbalistName);
        }

        if (token.Equals(ShopFinder.MageToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, MageName);
        }

        if (token.Equals(ShopFinder.ProvisionerToken, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsToken(vendorTypeName, ProvisionerName) ||
                   ContainsToken(vendorTypeName, VarietyDealerName);
        }

        return false;
    }

    public static bool VendorInSameTown(bool characterInTown, bool vendorInTown) =>
        characterInTown && vendorInTown;

    public static bool IsHarvestShopName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return ContainsToken(name, CarpenterName) ||
               ContainsToken(name, FishermanName) ||
               ContainsToken(name, BlacksmithName) ||
               ContainsToken(name, SmithName) ||
               ContainsToken(name, ButcherName) ||
               ContainsToken(name, BakerName) ||
               ContainsToken(name, ProvisionerName);
    }

    private static bool ContainsToken(string name, string token) =>
        name.Contains(token, StringComparison.OrdinalIgnoreCase);
}
