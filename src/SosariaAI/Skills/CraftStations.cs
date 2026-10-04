using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Finds the places a crafter works. A smith stands between an anvil and a forge, a cook
/// beside anything on the engine's heat list, and every other trade across the counter
/// from the vendor of its shop. Anvils, forges and ovens are world items in most shops and
/// map statics in a few, so both are read, and every anvil of the world is a place to look
/// as well as the smith shops of the destination list: the Britain smithy's forge is up on
/// a raised floor eleven tiles in from the blacksmith's marker, and Vesper's forges stand in
/// armourers' shops. A crafter picks the nearest place in reach that is not already full;
/// a cook with none in reach lights a campfire from its own kindling.
/// </summary>
public static class CraftStations
{
    public const int ClassicAnvilEastId = 0xFAF;
    public const int ClassicAnvilSouthId = 0xFB0;
    public const int ElvenAnvilEastId = 0x2DD5;
    public const int ElvenAnvilSouthId = 0x2DD6;

    /// <summary>How many shops of each kind, nearest first, are checked for a station.</summary>
    public const int ShopsPerKind = 3;

    /// <summary>
    /// How far from its station a crafter looks for a vendor that sells what it lacks before
    /// it walks to a shop of the destination list: the Britain blacksmith stands ten tiles
    /// from the anvil.
    /// </summary>
    public const int SupplySearchRadius = 24;

    /// <summary>How long a shop's station survey, a place list and the anvil list are trusted.</summary>
    public static readonly TimeSpan SurveyLife = TimeSpan.FromMinutes(10);

    /// <summary>Characters on one cell of 16 by 16 tiles share a place list.</summary>
    private const int PickCellShift = 4;

    /// <summary>Expired entries are swept once a cache holds this many.</summary>
    private const int CacheSweepSize = 4096;

    private static readonly Dictionary<(Map Map, Point3D Shop, CraftStation Station), (StandSpot? Spot, DateTime Until)>
        _surveys = new();

    private static readonly Dictionary<(Map Map, string Facet, string Trade, int CellX, int CellY), (List<StationShop> Places, DateTime Until)>
        _places = new();

    private static readonly Dictionary<Map, (List<Point3D> Anvils, DateTime Until)> _anvils = new();

    /// <summary>
    /// Gold that pays any shelf price: a walk to the shop to sell what the station made looks for
    /// the trade's shop, not for stock the crafter can pay for.
    /// </summary>
    public const int AnyGold = int.MaxValue;

    /// <summary>No place tried yet: a readiness check looks at every seller in reach.</summary>
    private static readonly HashSet<Point3D> NoneTried = [];

    /// <summary>
    /// The spot to stand at for <paramref name="station"/> within the search radius of
    /// <paramref name="around"/>, nearest to it, or null when there is none. Read fresh:
    /// a campfire burns out.
    /// </summary>
    public static StandSpot? Find(Map map, Point3D around, CraftStation station)
    {
        if (map == null || map == Map.Internal || station == CraftStation.None)
        {
            return null;
        }

        var first = new List<Point3D>();
        var second = new List<Point3D>();
        Collect(map, around, station, CraftStationRules.SearchRadius, first, second);
        return StandOf(first, second, around, station);
    }

    /// <summary>True when the engine lets the crafter work its station from where it stands.</summary>
    public static bool WorksHere(Mobile crafter, CraftStation station)
    {
        if (station == CraftStation.None)
        {
            return true;
        }

        if (!People.InWorld(crafter))
        {
            return false;
        }

        var first = new List<Point3D>();
        var second = new List<Point3D>();
        Collect(crafter.Map, crafter.Location, station, CraftStationRules.EngineReach, first, second);
        return CraftStationRules.CanWorkAt(crafter.Location, first, second, station);
    }

    /// <summary>
    /// The nearest tile the crafter can work <paramref name="stand"/>'s station from that
    /// nobody else stands on, has a floor on the station's level and is not in
    /// <paramref name="tried"/>, or null when every one is taken.
    /// </summary>
    public static Point3D? FreeWorkTile(Mobile crafter, StandSpot stand, CraftStation station, ISet<Point3D> tried)
    {
        var map = crafter?.Map;

        if (map == null || map == Map.Internal || station == CraftStation.None)
        {
            return null;
        }

        var first = new List<Point3D>();
        var second = new List<Point3D>();
        Collect(map, stand.Spot, station, CraftStationRules.WorkTileSurvey, first, second);
        var free = new List<Point3D>();

        foreach (var tile in CraftStationRules.WorkTiles(stand, first, second, station))
        {
            if (Standable.TryFind(map, tile.X, tile.Y, tile.Z, out var z) && NavMetric.SameFloor(z, tile.Z) &&
                new Point3D(tile.X, tile.Y, z) is var floor && !tried.Contains(floor) && IsFree(map, floor, crafter))
            {
                free.Add(floor);
            }
        }

        return CraftStationRules.NearestTile(free, crafter.Location);
    }

    /// <summary>
    /// Where the character works its trade now: the nearest place in reach whose station is
    /// really there, counting the crafters already standing at each against it, or null when
    /// there is none or every one is full.
    /// </summary>
    public static StationShop? ShopFor(SosariaCharacter character, CraftTrade trade)
    {
        if (!People.InWorld(character) || trade == null)
        {
            return null;
        }

        var places = Places(character, trade);
        var crowds = new List<int>(places.Count);

        for (var i = 0; i < places.Count; i++)
        {
            crowds.Add(CrowdAt(character, places[i].Station));
        }

        return CraftStationRules.PickPlace(places, crowds, character.Location);
    }

    /// <summary>
    /// Where to buy what the crafter lacks: the nearest vendor within
    /// <see cref="SupplySearchRadius"/> of <paramref name="station"/> whose shelves answer the
    /// need (<see cref="Supplies"/>), else the nearest shop in reach of the first kind on the
    /// trade's supply list where a live vendor answers it: the butcher before the inn for a
    /// cook's meat, the carpenter before the bowyer for a fletcher's boards. A shop marker only
    /// says where the shop was drawn, so the counter is looked at: Trinsic's tinker marker holds
    /// a guildmaster who sells nothing, and a tinker sent there for tools came back empty. The
    /// tool shop is skipped while the crafter holds its tool. A shelf of stock counts only when
    /// <paramref name="gold"/> pays for a unit of it past the tool reserve, and a shelf of tools
    /// only when <paramref name="gold"/> pays for the tool. Places in
    /// <paramref name="tried"/> are skipped. Null when nothing is left to try.
    /// </summary>
    public static Point3D? SupplyFor(
        SosariaCharacter character, CraftTrade trade, Point3D station, bool needTool, ISet<Point3D> tried, int gold
    )
    {
        if (!People.InWorld(character) || trade == null)
        {
            return null;
        }

        var tools = CraftStationSkill.CountTools(character, trade.System);

        foreach (var vendor in VendorDeal.VendorsNear(character.Map, station, SupplySearchRadius))
        {
            if (!tried.Contains(vendor.Location) && Supplies(vendor, trade, needTool, gold, tools))
            {
                return vendor.Location;
            }
        }

        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var graph = NavWorld.GraphFor(character.HomeFacet);
        var reach = HomeLeash.ConfiguredRadius();

        for (var i = 0; i < trade.SupplyShopTokens.Count && catalog != null; i++)
        {
            var token = trade.SupplyShopTokens[i];

            if (!needTool && token == trade.ToolShopToken)
            {
                continue;
            }

            var found = catalog.NearestFirst(token, character.Location, ShopFinder.StockedLooks);

            for (var d = 0; d < found.Count; d++)
            {
                var marker = found[d].Arrival;

                if (marker == Point3D.Zero || HomeLeash.BeyondLeash(marker, character.Location, reach) ||
                    found[d].ApproachPoint(graph) is var shop && tried.Contains(shop))
                {
                    continue;
                }

                if (VendorDeal.VendorsNear(character.Map, marker, VendorDeal.CounterRange)
                    .Exists(vendor => Supplies(vendor, trade, needTool, gold, tools)))
                {
                    return shop;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// True when a live vendor in reach sells the trade's tool at a price the pack pays, or, when
    /// <paramref name="needTool"/> is false, its stock at a price the pack pays for a unit of.
    /// </summary>
    public static bool SellerInReach(SosariaCharacter character, CraftTrade trade, bool needTool) =>
        character != null &&
        SupplyFor(character, trade, character.Location, needTool, NoneTried, PackGold(character)) != null;

    /// <summary>The gold in the crafter's pack, what a counter takes payment from.</summary>
    public static int PackGold(Mobile crafter) => crafter?.Backpack?.GetAmount(typeof(Gold)) ?? 0;

    /// <summary>
    /// True when the crafter holds a tool of its trade or can pay for one in reach: without either
    /// a session only walks to the shop and fails.
    /// </summary>
    public static bool CanGetTool(SosariaCharacter crafter, CraftTrade trade) =>
        trade != null && (CraftStationSkill.FindTool(crafter, trade.System) != null || SellerInReach(crafter, trade, needTool: true));

    /// <summary>
    /// True when the crafter carries the makings of a piece it can work (<see cref="CarriesMakings"/>),
    /// or has coin for its stock and a shelf in reach still holds some. Without either a session
    /// waits out its dry spell and fails: the shelves sold out of cloth, and over a hundred tailor
    /// sessions in half an hour ended "could not get materials".
    /// </summary>
    public static bool CanGetStock(SosariaCharacter crafter, CraftTrade trade) =>
        trade != null && (CarriesMakings(crafter, trade) || CanBuyStock(crafter, trade));

    /// <summary>
    /// True when the pack holds every material of one piece on the trade's craft list that the
    /// crafter works at even odds or better: the test the station runs before its first try.
    /// A few scraps of stock are not a session: a Jhelom tailor with leftover cloth walked to its
    /// station, stood out of cloth for three minutes, and did it again four times in an hour.
    /// </summary>
    public static bool CarriesMakings(Mobile crafter, CraftTrade trade)
    {
        var system = trade?.System;

        if (system == null || !CarriesStock(crafter, trade))
        {
            return false;
        }

        for (var i = 0; i < system.CraftItems.Count; i++)
        {
            var item = system.CraftItems[i];

            if (CraftStationSkill.IsCraftable(item) && CraftStationSkill.CoversOnePiece(crafter, item) &&
                CraftStationSkill.WorksAtUsefulOdds(crafter, system, item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when a shelf in reach sells stock the pack pays for, or a gatherer holding stock up
    /// at a bank in reach sells a lot the purse, bank balance counted, pays for
    /// (<see cref="BankStock.OfferedInReach"/>).
    /// </summary>
    public static bool CanBuyStock(SosariaCharacter crafter, CraftTrade trade) =>
        trade != null && (SellerInReach(crafter, trade, needTool: false) || BankStock.OfferedInReach(crafter, trade));

    /// <summary>
    /// True when the crafter digs its own stock and still carries ore: the selling walk smelts it
    /// at a forge first (<see cref="VendorSellSkill"/>). A smith back from the mine with a pack of
    /// ore would otherwise buy shelf ingots at the counter for more than its goods fetch.
    /// </summary>
    public static bool SmeltsFirst(Mobile crafter, CraftTrade trade) =>
        trade?.GatherKind == SkillKinds.Mine && crafter?.Backpack?.FindItemByType<BaseOre>() != null;

    /// <summary>True when the crafter carries some of the stock its trade burns.</summary>
    public static bool CarriesStock(Mobile crafter, CraftTrade trade) => StockCarried(crafter, trade) > 0;

    /// <summary>Units of the stock its trade burns the crafter carries, every stock type counted.</summary>
    public static int StockCarried(Mobile crafter, CraftTrade trade)
    {
        var pack = crafter?.Backpack;
        var carried = 0;

        for (var i = 0; pack != null && i < (trade?.StockTypes.Count ?? 0); i++)
        {
            carried += pack.GetAmount(trade.StockTypes[i]);
        }

        return carried;
    }

    /// <summary>
    /// The vendor of a station-less trade's shop to stand beside: the nearest one within the
    /// search radius of <paramref name="shop"/> that sells the trade's tool or stock, or null.
    /// </summary>
    public static BaseVendor CounterVendor(Map map, Point3D shop, CraftTrade trade)
    {
        foreach (var vendor in VendorDeal.VendorsNear(map, shop, CraftStationRules.SearchRadius))
        {
            if (ShelvesStock(vendor, trade) || SellsTool(vendor, trade.System))
            {
                return vendor;
            }
        }

        return null;
    }

    /// <summary>
    /// True when the vendor answers what the crafter lacks (<see cref="CraftTradeRules.MeetsNeed"/>):
    /// its tool at a price <paramref name="gold"/> pays (<see cref="CraftTradeRules.PaysForTool"/>),
    /// or its stock at a price <paramref name="gold"/> pays for a unit of, past the reserve a
    /// crafter with <paramref name="toolsCarried"/> tools keeps.
    /// </summary>
    public static bool Supplies(BaseVendor vendor, CraftTrade trade, bool needTool, int gold, int toolsCarried) =>
        CraftTradeRules.MeetsNeed(
            VendorDeal.ShelfLine(vendor, trade.StockTypes) is { } line && CraftTradeRules.PaysForUnit(gold, line.Price, toolsCarried),
            ToolLine(vendor, trade.System) is { } tool && CraftTradeRules.PaysForTool(gold, tool.Price),
            needTool
        );

    /// <summary>True when the vendor's shelves hold some of the trade's stock.</summary>
    public static bool ShelvesStock(BaseVendor vendor, CraftTrade trade) => VendorDeal.ShelfLine(vendor, trade.StockTypes) != null;

    /// <summary>True when one of the vendor's shelf lines still holds a tool of the craft system.</summary>
    public static bool SellsTool(BaseVendor vendor, CraftSystem system) => ToolLine(vendor, system) != null;

    /// <summary>The first shelf line of the vendor that still holds a tool of the craft system, or null.</summary>
    private static GenericBuyInfo ToolLine(BaseVendor vendor, CraftSystem system) =>
        VendorDeal.ShelfLine(vendor, line => line.GetDisplayEntity() is BaseTool shown && shown.CraftSystem == system);

    /// <summary>The cook carries kindling, has some camping and stands outside a dungeon.</summary>
    public static bool CanLightFire(Mobile cook) =>
        cook?.Backpack != null &&
        CookRules.MayLightFire(
            cook.Backpack.FindItemByType<Kindling>() is { Deleted: false },
            cook.Skills.Camping.Value,
            cook.Region?.IsPartOf<DungeonRegion>() == true
        );

    /// <summary>
    /// Uses a kindling from the pack as a player would. The engine places the fire beside
    /// the cook when the camping check passes.
    /// </summary>
    public static void LightFire(Mobile cook)
    {
        if (CanLightFire(cook))
        {
            cook.Backpack.FindItemByType<Kindling>().OnDoubleClick(cook);
        }
    }

    public static bool IsAnvilId(int itemId) =>
        (itemId & SmeltRules.ItemIdMask) is ClassicAnvilEastId or ClassicAnvilSouthId or ElvenAnvilEastId
            or ElvenAnvilSouthId;

    // Places are cached per cell of the map: shops and stations stay where they are, and the
    // crowd at each is counted fresh on every pick.
    private static List<StationShop> Places(SosariaCharacter character, CraftTrade trade)
    {
        var now = Core.Now;
        var from = character.Location;
        var key = (character.Map, character.HomeFacet, trade.Kind, from.X >> PickCellShift, from.Y >> PickCellShift);

        if (_places.TryGetValue(key, out var cached) && cached.Until > now)
        {
            return cached.Places;
        }

        var places = CraftStationRules.Distinct(Survey(character, trade));
        Sweep(_places, entry => entry.Until, now);
        _places[key] = (places, now + SurveyLife);
        return places;
    }

    private static List<StationShop> Survey(SosariaCharacter character, CraftTrade trade)
    {
        var shops = new List<StationShop>();
        var map = character.Map;
        var from = character.Location;
        var reach = HomeLeash.ConfiguredRadius();
        var catalog = NavWorld.DestinationsFor(character.HomeFacet);
        var graph = NavWorld.GraphFor(character.HomeFacet);

        for (var t = 0; t < trade.StationShopTokens.Count; t++)
        {
            var found = catalog?.NearestFirst(trade.StationShopTokens[t], from, ShopsPerKind) ?? [];

            for (var d = 0; d < found.Count; d++)
            {
                if (found[d].Arrival != Point3D.Zero && !HomeLeash.BeyondLeash(found[d].Arrival, from, reach))
                {
                    AddPlace(shops, map, found[d].ApproachPoint(graph), trade.Station);
                }
            }
        }

        if (trade.Station == CraftStation.AnvilAndForge)
        {
            var anvils = Anvils(map);

            for (var i = 0; i < anvils.Count; i++)
            {
                if (!HomeLeash.BeyondLeash(anvils[i], from, reach) && Survey(map, anvils[i], trade.Station) is { } stand)
                {
                    shops.Add(new StationShop(stand.Spot, stand.Spot));
                }
            }
        }

        if (trade.FallbackShop != Point3D.Zero && !HomeLeash.BeyondLeash(trade.FallbackShop, from, reach))
        {
            AddPlace(shops, map, trade.FallbackShop, trade.Station);
        }

        return shops;
    }

    private static void AddPlace(List<StationShop> shops, Map map, Point3D shop, CraftStation station)
    {
        if (station == CraftStation.None)
        {
            shops.Add(new StationShop(shop, shop));
        }
        else if (Survey(map, shop, station) is { } stand)
        {
            shops.Add(new StationShop(shop, stand.Spot));
        }
    }

    private static int CrowdAt(SosariaCharacter character, Point3D station)
    {
        var crowd = 0;

        foreach (var mobile in character.Map.GetMobilesInRange(station, CraftStationRules.CrowdRadius))
        {
            if (mobile is SosariaCharacter { Deleted: false } other && other != character)
            {
                crowd++;
            }
        }

        return crowd;
    }

    // Every anvil standing loose in the world on this map: decoration anvils are world items.
    private static List<Point3D> Anvils(Map map)
    {
        var now = Core.Now;

        if (_anvils.TryGetValue(map, out var cached) && cached.Until > now)
        {
            return cached.Anvils;
        }

        var anvils = new List<Point3D>();

        foreach (var item in World.Items.Values)
        {
            if (item is { Deleted: false, Parent: null } && item.Map == map && IsAnvil(item))
            {
                anvils.Add(item.Location);
            }
        }

        _anvils[map] = (anvils, now + SurveyLife);
        return anvils;
    }

    // Ovens and forges stay where they are, so a shop's survey is kept a while instead of
    // reading a thousand tiles of statics on every pick.
    private static StandSpot? Survey(Map map, Point3D shop, CraftStation station)
    {
        var now = Core.Now;
        var key = (map, shop, station);

        if (_surveys.TryGetValue(key, out var cached) && cached.Until > now)
        {
            return cached.Spot;
        }

        var spot = Find(map, shop, station);
        Sweep(_surveys, entry => entry.Until, now);
        _surveys[key] = (spot, now + SurveyLife);
        return spot;
    }

    private static void Sweep<TKey, TValue>(Dictionary<TKey, TValue> cache, Func<TValue, DateTime> until, DateTime now)
    {
        if (cache.Count < CacheSweepSize)
        {
            return;
        }

        var expired = new List<TKey>();

        foreach (var (key, value) in cache)
        {
            if (until(value) <= now)
            {
                expired.Add(key);
            }
        }

        for (var i = 0; i < expired.Count; i++)
        {
            cache.Remove(expired[i]);
        }
    }

    private static bool IsAnvil(Item item) =>
        item.GetType().IsDefined(typeof(AnvilAttribute), inherit: false) || IsAnvilId(item.ItemID);

    private static StandSpot? StandOf(List<Point3D> first, List<Point3D> second, Point3D around, CraftStation station) =>
        station == CraftStation.AnvilAndForge
            ? CraftStationRules.PairSpot(first, second, around)
            : CraftStationRules.NearestSpot(first, around);

    // Nobody but the crafter itself stands on the tile.
    private static bool IsFree(Map map, Point3D tile, Mobile crafter)
    {
        foreach (var mobile in map.GetMobilesAt(tile))
        {
            if (mobile != crafter && !mobile.Deleted)
            {
                return false;
            }
        }

        return true;
    }

    // For a smith the first list holds anvils and the second forges; for a cook the first
    // list holds heat sources.
    private static void Collect(Map map, Point3D from, CraftStation station, int radius, List<Point3D> first, List<Point3D> second)
    {
        foreach (var item in map.GetItemsInRange(from, radius))
        {
            if (item is not { Deleted: false })
            {
                continue;
            }

            Sort(station, item.Location, IsAnvil(item), SmeltRules.IsForge(item), CookRules.IsHeatSource(item), first, second);
        }

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                foreach (var tile in map.Tiles.GetStaticAndMultiTiles(from.X + dx, from.Y + dy))
                {
                    var at = new Point3D(from.X + dx, from.Y + dy, tile.Z);
                    Sort(
                        station,
                        at,
                        IsAnvilId(tile.ID),
                        SmeltRules.IsForgeId(tile.ID),
                        CookRules.IsHeatSourceId(tile.ID),
                        first,
                        second
                    );
                }
            }
        }
    }

    private static void Sort(
        CraftStation station,
        Point3D at,
        bool anvil,
        bool forge,
        bool heat,
        List<Point3D> first,
        List<Point3D> second
    )
    {
        if (station == CraftStation.AnvilAndForge)
        {
            AddIf(anvil, at, first);
            AddIf(forge, at, second);
            return;
        }

        AddIf(heat, at, first);
    }

    private static void AddIf(bool matches, Point3D at, List<Point3D> list)
    {
        if (matches && !list.Contains(at))
        {
            list.Add(at);
        }
    }
}
