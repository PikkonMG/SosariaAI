using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A tinkering: tools, lockpicks and small wares from ingots and boards, made with tinker
/// tools. The tinker sells the ingots, the boards and the tools; the blacksmith sells ingots
/// and the carpenter boards.
/// </summary>
public static class TinkerRules
{
    public const string Kind = SkillKinds.Tinker;
    public const string GoodsNoun = "tools";
    public const string StockNoun = "ingots";
    public const string ToolNoun = "tinker tools";

    // West Britain tinker guild shop (Felucca vendor spawner).
    public static readonly Point3D BritainTinker = new(1422, 1654, 10);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Tinkering,
        () => DefTinkering.CraftSystem,
        ShopFinder.TinkerToken,
        BritainTinker,
        CraftStation.None,
        GoodsNoun,
        otherStationShops: [ShopFinder.SmithToken, ShopFinder.CarpenterToken],
        stockTypes: [typeof(IronIngot), typeof(Board), typeof(Log)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        ownToolType: typeof(TinkerTools)
    );
}
