using Server;
using Server.Engines.Craft;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// T2A inscription: spell scrolls written on blank scrolls with reagents and mana, for
/// spells the scribe's own spellbook holds. The mage shop sells the blank scrolls, the
/// reagents and the scribe's pen; the scribe sells the scrolls and the pen.
/// </summary>
public static class InscriptionRules
{
    public const string Kind = SkillKinds.Inscription;
    public const string GoodsNoun = "scrolls";
    public const string StockNoun = "blank scrolls";
    public const string ToolNoun = "scribe pen";

    /// <summary>
    /// Britain mage shop (Mage, Alchemist, MageGuildmaster). Sells scribe pens.
    /// </summary>
    public static readonly Point3D BritainMageShop = new(1485, 1550, 30);

    public static readonly CraftTrade Trade = new(
        Kind,
        SkillName.Inscribe,
        () => DefInscription.CraftSystem,
        ShopFinder.MageToken,
        BritainMageShop,
        CraftStation.None,
        GoodsNoun,
        otherStationShops: [ShopFinder.ScribeToken],
        stockTypes: [typeof(BlankScroll)],
        stockNoun: StockNoun,
        toolNoun: ToolNoun,
        peopleBringStock: false
    );
}
