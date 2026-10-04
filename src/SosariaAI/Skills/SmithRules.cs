using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A blacksmithy: weapons and armour hammered from iron ingots between an anvil and a
/// forge. The blacksmith sells the ingots and the tongs; the tinker sells both too. Britain's
/// west smithy and Vesper's forges stand in armourers' shops, so an armourer's shop is a
/// station shop as well.
/// </summary>
public static class SmithRules
{
    public const string GoodsNoun = "arms";
    public const string StockNoun = "ingots";
    public const string ToolNoun = "smith hammer";

    public static readonly CraftTrade Trade = new(
        SkillKinds.Smith,
        SkillName.Blacksmith,
        () => DefBlacksmithy.CraftSystem,
        ShopFinder.SmithToken,
        CharactersFile.HalBlacksmith,
        CraftStation.AnvilAndForge,
        GoodsNoun,
        otherStationShops: [ShopFinder.ArmorerToken],
        toolShopToken: ShopFinder.TinkerToken,
        stockTypes: [typeof(IronIngot)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        gatherKind: SkillKinds.Mine
    );
}
