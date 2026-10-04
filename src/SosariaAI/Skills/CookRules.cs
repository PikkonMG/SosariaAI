using System;
using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A cooking: meat and fowl cooked at a fire or oven. The butcher sells the raw meat, the
/// stock a cook burns and buys off a hunter who carries some. The engine's heat check
/// (CraftItem.m_HeatSources) looks two tiles round the cook for an oven, fireplace,
/// campfire, firepit, heating stand, fire field, stove, brazier or forge.
/// Most inns have none, so a cook works at the nearest inn, bakery or smithy that has one,
/// or lights its own campfire from kindling.
/// </summary>
public static class CookRules
{
    public const string Kind = SkillKinds.Cook;
    public const string GoodsNoun = "food";
    public const string StockNoun = "raw meat";
    public const string ToolNoun = "skillet";

    public const int CampfireIdMin = 0xDE3;
    public const int CampfireIdMax = 0xDE9;
    public const int SandstoneOvenIdMin = 0x461;
    public const int SandstoneOvenIdMax = 0x48E;
    public const int StoneOvenIdMin = 0x92B;
    public const int StoneOvenIdMax = 0x96C;
    public const int FirepitItemId = 0xFAC;
    public const int HeatingStandLeftIdMin = 0x184A;
    public const int HeatingStandLeftIdMax = 0x184C;
    public const int HeatingStandRightIdMin = 0x184E;
    public const int HeatingStandRightIdMax = 0x1850;
    public const int FireFieldIdMin = 0x398C;
    public const int FireFieldIdMax = 0x399F;
    public const int ElvenStoveIdMin = 0x2DDB;
    public const int ElvenStoveIdMax = 0x2DDC;
    public const int BrazierIdMin = 0x19AA;
    public const int BrazierIdMax = 0x19BB;

    /// <summary>Kindling.OnDoubleClick checks Camping from zero: with none the fire never lights.</summary>
    public const double NoCamping = 0;

    // Britain tavern keeper (Felucca vendor spawner). Catalog alias "tavern".
    public static readonly Point3D BritainInn = new(1427, 1716, 20);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Cooking,
        () => DefCooking.CraftSystem,
        ShopFinder.TavernToken,
        BritainInn,
        CraftStation.Heat,
        GoodsNoun,
        ShopFinder.ButcherToken,
        [ShopFinder.BakerToken, ShopFinder.SmithToken],
        ShopFinder.TavernToken,
        [typeof(RawRibs), typeof(RawBird), typeof(RawLambLeg), typeof(RawChickenLeg)],
        StockNoun,
        ToolNoun
    );

    /// <summary>The engine's heat list, forges included.</summary>
    public static bool IsHeatSourceId(int itemId) =>
        itemId is >= CampfireIdMin and <= CampfireIdMax
            or >= SandstoneOvenIdMin and <= SandstoneOvenIdMax
            or >= StoneOvenIdMin and <= StoneOvenIdMax
            or FirepitItemId
            or >= HeatingStandLeftIdMin and <= HeatingStandLeftIdMax
            or >= HeatingStandRightIdMin and <= HeatingStandRightIdMax
            or >= FireFieldIdMin and <= FireFieldIdMax
            or >= ElvenStoveIdMin and <= ElvenStoveIdMax
            or >= BrazierIdMin and <= BrazierIdMax ||
        SmeltRules.IsForgeId(itemId);

    public static bool IsHeatSource(Item item)
    {
        if (item is not { Deleted: false })
        {
            return false;
        }

        if (item is Campfire or StoneOvenEastAddon or StoneOvenSouthAddon)
        {
            return true;
        }

        return IsHeatSourceId(item.ItemID);
    }

    /// <summary>
    /// A cook needs a pan, meat and heat. Only the engine's cook sells a skillet, and it works
    /// in the taverns, beside the tavern keeper; the tavern keeper, the bakery and the smithy a
    /// cook heats its pan at sell none. Raw meat comes from the butcher, and Magincia has none:
    /// its cooks walked to the inn and failed "could not get materials" again and again. Each
    /// need counts when carried or on a live vendor's shelf in reach.
    /// </summary>
    public static bool CanCook(SosariaCharacter cook) =>
        MayCook(
            CraftStationSkill.FindTool(cook, Trade.System) != null,
            () => CraftStations.SellerInReach(cook, Trade, needTool: true),
            CraftStations.CarriesStock(cook, Trade),
            () => CraftStations.SellerInReach(cook, Trade, needTool: false),
            () => CraftStations.CanLightFire(cook) || CraftStations.ShopFor(cook, Trade) != null
        );

    /// <summary>
    /// True when the pan and the meat are each carried or for sale in reach, and there is heat.
    /// The pack is looked in first: a shelf or a heat source is looked for only when the answer
    /// still turns on it, since each look walks the vendors and the shop list, for every cook
    /// on every score.
    /// </summary>
    public static bool MayCook(bool hasPan, Func<bool> panForSale, bool hasMeat, Func<bool> meatForSale, Func<bool> hasHeat) =>
        (hasPan || panForSale()) && (hasMeat || meatForSale()) && hasHeat();

    /// <summary>
    /// A cook with no heat in reach lights a campfire, as a player does, when it carries
    /// kindling and has any camping. Kindling burns in every era; the engine refuses a
    /// fire in a dungeon.
    /// </summary>
    public static bool MayLightFire(bool hasKindling, double camping, bool inDungeon) =>
        hasKindling && camping > NoCamping && !inDungeon;
}
