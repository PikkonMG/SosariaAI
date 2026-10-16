using System;
using Server;
using Server.Multis;
using Server.Multis.Deeds;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// Where a character builds the house it wants and which deed it buys. Characters build only
/// on the plots <c>housePlots</c> lists, one house to a plot, so they never take the spots
/// players want; a vendor plot goes to a crafter first. The house is the one the person's
/// seed wants (<see cref="HouseWantRules"/>).
/// </summary>
public static class HousePlots
{
    public static int Seed(SosariaCharacter character) => unchecked((int)character.Serial.Value);

    public static int SmallPrice => SosariaSettings.Characters?.Career?.HouseGold ?? CareerSettings.DefaultHouseGold;

    /// <summary>The gold the house the character wants costs.</summary>
    public static int PriceFor(SosariaCharacter character) =>
        HouseWantRules.Price(HouseWantRules.SizeFor(Seed(character)), SmallPrice);

    /// <summary>The deed to the house the character wants; a small house is one of the architect's small deeds.</summary>
    public static HouseDeed DeedFor(SosariaCharacter character)
    {
        var seed = Seed(character);

        return HouseWantRules.SizeFor(seed) switch
        {
            HouseSize.Tower => new TowerDeed(),
            HouseSize.TwoStory => new TwoStoryWoodPlasterHouseDeed(),
            _ => ((seed & int.MaxValue) / HouseWantRules.WantEvery % SmallDeedCount) switch
            {
                0 => new StonePlasterHouseDeed(),
                1 => new FieldStoneHouseDeed(),
                2 => new SmallBrickHouseDeed(),
                3 => new WoodHouseDeed(),
                4 => new WoodPlasterHouseDeed(),
                _ => new ThatchedRoofCottageDeed()
            }
        };
    }

    private const int SmallDeedCount = 6;

    /// <summary>
    /// The first free plot on the character's home facet it may use: a vendor plot first for a
    /// crafter. With no plot listed at all, the default plot east of Britain on a Britannia
    /// map. Null when every plot is taken.
    /// </summary>
    public static HousePlot FreeFor(SosariaCharacter character)
    {
        var plots = SosariaSettings.Characters?.HousePlots;

        if (plots == null || plots.Count == 0)
        {
            return DefaultOn(character) is { } fallback && Free(fallback, character.Map) ? fallback : null;
        }

        var crafter = CraftMarket.TradeOf(character) != null;
        HousePlot pick = null;

        for (var i = 0; i < plots.Count; i++)
        {
            var plot = plots[i];

            if (plot == null || !OnHomeFacet(plot, character) ||
                !HouseRules.MayUsePlot(plot.Vendor, crafter) || !Free(plot, character.Map))
            {
                continue;
            }

            if (plot.Vendor)
            {
                return plot;
            }

            pick ??= plot;
        }

        return pick;
    }

    private static bool OnHomeFacet(HousePlot plot, SosariaCharacter character) =>
        string.IsNullOrWhiteSpace(plot.Map) || plot.Map.Equals(character.HomeFacet, StringComparison.OrdinalIgnoreCase);

    // No house stands on the plot.
    private static bool Free(HousePlot plot, Map map)
    {
        for (var i = 0; i < BaseHouse.AllHouses.Count; i++)
        {
            var house = BaseHouse.AllHouses[i];

            if (house is { Deleted: false } && house.Map == map && HouseRules.TakesPlot(house.Location, plot.Location, plot.Reach))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The default plot is a Britannia spot east of Britain: on any other map there is no default,
    /// and a house waits for a <c>housePlots</c> entry.
    /// </summary>
    private static HousePlot DefaultOn(SosariaCharacter character) =>
        WorldAnchors.For(character.HomeFacet).IsStock
            ? new HousePlot
            {
                X = HouseRules.DefaultPlotX,
                Y = HouseRules.DefaultPlotY,
                Z = HouseRules.DefaultPlotZ
            }
            : null;
}
