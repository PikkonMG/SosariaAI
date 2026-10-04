using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A bowcraft: shafts and bows from logs (the engine's type table takes boards for them too),
/// arrows and bolts from shafts and feathers, made with fletcher's tools. A bowyer asks for logs,
/// what lumberjacks carry. The bowyer sells only the tools; no vendor sells
/// shafts or feathers, so a bowyer buys boards at the carpenter or the tinker, or works the
/// logs it cut, and fletches at the bowyer's or the carpenter's.
/// </summary>
public static class FletchRules
{
    public const string Kind = SkillKinds.Fletch;
    public const string GoodsNoun = "bows and arrows";
    public const string StockNoun = "logs";
    public const string ToolNoun = "fletcher tools";

    // Britain bowyer shop (Felucca vendor spawner).
    public static readonly Point3D BritainBowyer = new(1470, 1578, 20);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Fletching,
        () => DefBowFletching.CraftSystem,
        ShopFinder.BowyerToken,
        BritainBowyer,
        CraftStation.None,
        GoodsNoun,
        materialShopToken: ShopFinder.CarpenterToken,
        otherStationShops: [ShopFinder.CarpenterToken, ShopFinder.TinkerToken],
        toolShopToken: ShopFinder.BowyerToken,
        stockTypes: [typeof(Board), typeof(Log), typeof(Shaft), typeof(Feather)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        gatherKind: SkillKinds.Lumberjack
    );
}
