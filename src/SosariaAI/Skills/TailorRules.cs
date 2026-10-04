using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A tailoring: clothing from cloth and leather armour from leather, sewn with a sewing
/// kit. The tailor and the weaver sell the cloth, the tanner the leather, and the tailor and
/// the tinker the kit. Minoc and Yew have no tailor, so a tailor there sews at the tanner's.
/// </summary>
public static class TailorRules
{
    public const string Kind = SkillKinds.Tailor;
    public const string GoodsNoun = "clothes";
    public const string StockNoun = "cloth";
    public const string ToolNoun = "sewing kit";

    // East Britain tailor, weaver, and tailor guildmaster shop (Felucca vendor spawner).
    public static readonly Point3D BritainTailor = new(1547, 1659, 26);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Tailoring,
        () => DefTailoring.CraftSystem,
        ShopFinder.TailorToken,
        BritainTailor,
        CraftStation.None,
        GoodsNoun,
        otherStationShops: [ShopFinder.WeaverToken, ShopFinder.TannerToken],
        toolShopToken: ShopFinder.TinkerToken,
        stockTypes: [typeof(Cloth), typeof(UncutCloth), typeof(Leather), typeof(Hides)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun
    );
}
