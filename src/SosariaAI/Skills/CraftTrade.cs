using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;

namespace SosariaAI.Skills;

/// <summary>
/// One crafting trade: the skill, the engine craft system, the shop the crafter works at,
/// what it must stand beside, a second shop for materials the work shop does not stock, a
/// shop for the tool when the work shop sells none, the raw stock it burns, what its stock and
/// tool are called aloud, the tool it makes for itself when its craft list holds one, the
/// harvest that yields its stock when nobody sells any, and whether people ever bring its stock.
/// The craft system is read lazily: it exists once the engine has configured crafting.
/// </summary>
public sealed class CraftTrade
{
    private readonly Func<CraftSystem> _system;

    public CraftTrade(
        string kind,
        SkillName skill,
        Func<CraftSystem> system,
        string shopToken,
        Point3D fallbackShop,
        CraftStation station,
        string goodsNoun,
        string materialShopToken = null,
        IReadOnlyList<string> otherStationShops = null,
        string toolShopToken = null,
        IReadOnlyList<Type> stockTypes = null,
        string stockNoun = null,
        string toolNoun = null,
        Type ownToolType = null,
        string gatherKind = null,
        bool peopleBringStock = true
    )
    {
        Kind = kind;
        Skill = skill;
        _system = system;
        ShopToken = shopToken;
        FallbackShop = fallbackShop;
        Station = station;
        GoodsNoun = goodsNoun;
        ToolShopToken = toolShopToken;
        StationShopTokens = [shopToken, .. otherStationShops ?? []];
        SupplyShopTokens = SupplyOrder(toolShopToken, materialShopToken, StationShopTokens);
        StockTypes = stockTypes ?? [];
        StockNoun = stockNoun;
        ToolNoun = toolNoun;
        OwnToolType = ownToolType;
        GatherKind = gatherKind;
        PeopleBringStock = peopleBringStock;
    }

    public string Kind { get; }

    public SkillName Skill { get; }

    public CraftSystem System => _system?.Invoke();

    public string ShopToken { get; }

    public Point3D FallbackShop { get; }

    public CraftStation Station { get; }

    /// <summary>What the trade's goods are called in a log line: "arms", "clothes".</summary>
    public string GoodsNoun { get; }

    /// <summary>A shop to buy the tool at when the work shop's vendors sell none, or null.</summary>
    public string ToolShopToken { get; }

    /// <summary>
    /// Every kind of shop that can hold the station, the work shop first. A crafter works
    /// at the nearest one whose station is really there.
    /// </summary>
    public IReadOnlyList<string> StationShopTokens { get; }

    /// <summary>
    /// The shops a crafter walks to for its tool and materials, in the order it tries them:
    /// the tool shop, the material shop, then the shops it works at.
    /// </summary>
    public IReadOnlyList<string> SupplyShopTokens { get; }

    /// <summary>
    /// The raw stock the trade burns and buys by the stack from gatherers: iron ingots at the
    /// forge, logs and boards at the bench, cloth and leather at the tailor. A crafter never
    /// hawks its own stock.
    /// </summary>
    public IReadOnlyList<Type> StockTypes { get; }

    /// <summary>What a crafter short of stock asks for aloud: "ingots", "raw meat".</summary>
    public string StockNoun { get; }

    /// <summary>What a crafter without its tool asks for aloud: "tinker tools", "skillet".</summary>
    public string ToolNoun { get; }

    /// <summary>
    /// The tool of the trade its own craft list makes, or null. A tinker makes its spare tinker
    /// tools from two ingots, as players kept a spare so a broken tool never ended the evening.
    /// </summary>
    public Type OwnToolType { get; }

    /// <summary>
    /// The harvest a crafter of the trade works for its own stock when it cannot buy any, or
    /// null: a 1999 smith with no ingots for sale dug and smelted its own ore, and a carpenter or
    /// bowyer cut its own logs.
    /// </summary>
    public string GatherKind { get; }

    /// <summary>
    /// False for stock only a shop sells: blank maps, blank scrolls, empty bottles. Nobody comes
    /// by the station or the bank with it, so a dry crafter does not wait for a seller: mapmakers
    /// out of blank maps waited three minutes 142 times in one evening for nobody.
    /// </summary>
    public bool PeopleBringStock { get; }

    /// <summary>True when <paramref name="type"/> is one of the trade's raw stock types exactly.</summary>
    public bool BurnsStock(Type type)
    {
        for (var i = 0; i < StockTypes.Count; i++)
        {
            if (StockTypes[i] == type)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> SupplyOrder(string tool, string material, IReadOnlyList<string> stations)
    {
        var order = new List<string>();
        Add(order, tool);
        Add(order, material);

        for (var i = 0; i < stations.Count; i++)
        {
            Add(order, stations[i]);
        }

        return order;
    }

    private static void Add(List<string> order, string token)
    {
        if (!string.IsNullOrWhiteSpace(token) && !order.Contains(token))
        {
            order.Add(token);
        }
    }
}
