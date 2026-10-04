using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A alchemy: potions ground from reagents into empty bottles with a mortar and pestle.
/// The alchemist sells the reagents, the bottles and the mortar; the herbalist sells the
/// mortar and the bottles.
/// </summary>
public static class AlchemyRules
{
    public const string Name = SkillKinds.Alchemy;
    public const string GoodsNoun = "potions";
    public const string StockNoun = "empty bottles";
    public const string ToolNoun = "mortar";

    // East Britain herbalist and alchemist shop (Felucca vendor spawner).
    public static readonly Point3D BritainAlchemist = new(1498, 1659, 27);

    public static readonly CraftTrade Trade = new(
        Name,
        SkillName.Alchemy,
        () => DefAlchemy.CraftSystem,
        ShopFinder.AlchemistToken,
        BritainAlchemist,
        CraftStation.None,
        GoodsNoun,
        otherStationShops: [ShopFinder.HerbalistToken],
        stockTypes: [typeof(Bottle)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        peopleBringStock: false
    );
}
