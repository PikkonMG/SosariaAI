using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A carpentry: furniture, staves and containers from wood, cut with a saw. The engine's
/// carpentry list takes logs, and its type table lets boards stand in for them, so a carpenter
/// burns either and asks for logs, what lumberjacks carry. The carpenter sells the boards and
/// the saw; the tinker sells both too, so a town with no carpenter works wood at the tinker's.
/// </summary>
public static class CarpentryRules
{
    public const string Kind = SkillKinds.Carpentry;
    public const string GoodsNoun = "furniture";
    public const string StockNoun = "logs";
    public const string ToolNoun = "saw";

    // West Britain carpenter, architect, and real estate broker shop (Felucca vendor spawner).
    public static readonly Point3D BritainCarpenter = new(1430, 1597, 20);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Carpentry,
        () => DefCarpentry.CraftSystem,
        ShopFinder.CarpenterToken,
        BritainCarpenter,
        CraftStation.None,
        GoodsNoun,
        otherStationShops: [ShopFinder.TinkerToken],
        stockTypes: [typeof(Board), typeof(Log)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        gatherKind: SkillKinds.Lumberjack
    );
}
