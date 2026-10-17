using System;
using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Multis;
using Server.Multis.Deeds;
using SosariaAI.Behaviour;
using SosariaAI.Configuration;
using SosariaAI.Economy;
using SosariaAI.Logging;
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
    private static readonly ILogger logger = SosariaLog.For(typeof(HousePlots));

    /// <summary>The first minute after a boot takes down the characters' houses that stand on no plot.</summary>
    public static void Initialize() => ActivityPulse.MinutePassed += TakeDownStrays;

    /// <summary>
    /// Takes down every character's house that stands on no listed plot: a house put up by an
    /// older build by a hot spot, or on a plot the operator took off the list. Runs once a boot.
    /// </summary>
    public static void TakeDownStrays()
    {
        ActivityPulse.MinutePassed -= TakeDownStrays;
        var strays = new List<BaseHouse>();

        for (var i = 0; i < BaseHouse.AllHouses.Count; i++)
        {
            var house = BaseHouse.AllHouses[i];

            if (house is { Deleted: false, Owner: SosariaCharacter } && !OnAPlot(house))
            {
                strays.Add(house);
            }
        }

        foreach (var house in strays)
        {
            var owner = (SosariaCharacter)house.Owner;
            logger.Information("{Owner}'s house at {Location} stands on no house plot and is taken down", owner.Name, house.Location);
            house.RemoveKeys(owner);
            house.Delete();

            // The owner did not lose it to decay: it keeps no "my house fell" memory.
            if (owner.HouseSerial == (int)house.Serial.Value)
            {
                owner.HouseSerial = HouseRules.NoHouseSerial;
                owner.HouseLocation = Point3D.Zero;
            }
        }
    }

    // The house takes one of the plots listed for its map, or the default plot when none is listed.
    private static bool OnAPlot(BaseHouse house)
    {
        var plots = SosariaSettings.Characters?.HousePlots;

        if (plots == null || plots.Count == 0)
        {
            return HouseRules.TakesPlot(house.Location, HouseRules.DefaultPlot(), HouseRules.PlaceSearchRadius);
        }

        for (var i = 0; i < plots.Count; i++)
        {
            if (plots[i] is { } plot &&
                (string.IsNullOrWhiteSpace(plot.Map) || plot.Map.Equals(house.Map?.Name, StringComparison.OrdinalIgnoreCase)) &&
                HouseRules.TakesPlot(house.Location, plot.Location, plot.Reach))
            {
                return true;
            }
        }

        return false;
    }

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
