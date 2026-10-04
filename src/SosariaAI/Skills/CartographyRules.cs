using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A cartography: local maps, city maps, sea charts and world maps drawn on blank maps with
/// a mapmaker's pen through the engine craft system, and treasure maps decoded and dug up.
/// The mapmaker sells the blank maps and the pens and buys the finished maps.
/// </summary>
public static class CartographyRules
{
    public const string Kind = SkillKinds.Cartography;
    public const string GoodsNoun = "maps";
    public const string StockNoun = "blank maps";
    public const string ToolNoun = "mapmaker pen";

    // Britain docks mapmaker and shipwright shop (Felucca vendor spawner).
    public static readonly Point3D BritainMapmaker = new(1416, 1754, 10);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Cartography,
        () => DefCartography.CraftSystem,
        ShopFinder.MapmakerToken,
        BritainMapmaker,
        CraftStation.None,
        GoodsNoun,
        stockTypes: [typeof(BlankMap)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        peopleBringStock: false
    );
}
