using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using SosariaAI.Behaviour;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// A crafter's workshop at home. A crafter that owns a house works its trade there, as a
/// player smith worked at the anvil and forge in its own tower: a smith's house gets an anvil
/// and a small forge, a cook's a stone oven, bought from a carpenter with gold from the bank;
/// every other trade needs no station and works on the floor. The crafter goes to town only
/// for what it lacks and to sell.
/// </summary>
public static class HomeWorkshop
{
    /// <summary>What a player carpenter asked for an anvil deed.</summary>
    public const int AnvilPrice = 2000;

    /// <summary>What a player carpenter asked for a small forge deed.</summary>
    public const int ForgePrice = 2000;

    /// <summary>What a player carpenter asked for a stone oven deed.</summary>
    public const int OvenPrice = 3000;

    /// <summary>
    /// The place the crafter works its trade at home: inside its own house, at the station the
    /// house holds for the trade; null when it owns no house in reach or the house has no such station.
    /// </summary>
    public static StationShop? StationAtHome(SosariaCharacter crafter, CraftTrade trade)
    {
        if (trade == null || HouseVisit.OwnedHouse(crafter) is not { } house || house.Map != crafter.Map ||
            HomeLeash.BeyondLeash(house.Location, crafter.Location, HomeLeash.ConfiguredRadius()) ||
            HouseDoorway.Of(house) is not { } way)
        {
            return null;
        }

        if (trade.Station == CraftStation.None)
        {
            return new StationShop(way.Inside, way.Inside);
        }

        return CraftStations.Find(house.Map, house.Location, trade.Station) is { } stand &&
               house.IsInside(stand.Spot, PersonBody.Height)
            ? new StationShop(stand.Spot, stand.Spot)
            : null;
    }

    /// <summary>
    /// Sets up the station the owner's trade wants inside its house, paid from its bank. Owner
    /// inside its house, world thread. The number of pieces set up.
    /// </summary>
    public static int Furnish(SosariaCharacter owner, HouseDoorway way)
    {
        var trade = CraftMarket.TradeOf(owner);

        if (trade == null || trade.Station == CraftStation.None || StationAtHome(owner, trade) != null)
        {
            return 0;
        }

        return trade.Station == CraftStation.AnvilAndForge
            ? Place(owner, way, new AnvilEastAddon(), AnvilPrice) + Place(owner, way, new SmallForgeAddon(), ForgePrice)
            : Place(owner, way, new StoneOvenEastAddon(), OvenPrice);
    }

    // The first floor tile inside the house the engine lets the addon stand on; paid only when it stands.
    private static int Place(SosariaCharacter owner, HouseDoorway way, BaseAddon addon, int price)
    {
        foreach (var spot in FloorTiles(way))
        {
            BaseHouse house = null;

            if (addon.CouldFit(spot, way.House.Map, owner, ref house) == AddonFitResult.Valid && house == way.House &&
                Banker.Withdraw(owner, price))
            {
                addon.MoveToWorld(spot, way.House.Map);
                house.Addons.Add(addon);
                WorldPlay.Log($"{owner.Name} set up a {addon.GetType().Name} in its house");
                return 1;
            }
        }

        addon.Delete();
        return 0;
    }

    // The standing tiles of the house floor, around its middle.
    private static IEnumerable<Point3D> FloorTiles(HouseDoorway way)
    {
        var walker = Standable.Walker(way.House.Map);

        for (var dx = -HouseDoorway.FootprintTiles; dx <= HouseDoorway.FootprintTiles; dx++)
        {
            for (var dy = -HouseDoorway.FootprintTiles; dy <= HouseDoorway.FootprintTiles; dy++)
            {
                if (HouseDoorway.Standing(walker, new Point3D(way.House.X + dx, way.House.Y + dy, way.Inside.Z)) is { } tile &&
                    way.House.IsInside(tile, PersonBody.Height))
                {
                    yield return tile;
                }
            }
        }
    }
}
