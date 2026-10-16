using System;
using System.Collections.Generic;
using Server;
using Server.Multis;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// When a character may buy the allowed small house, and when it goes back to look in on
/// it. Free. No model. Britain town ends at x 1740. The default plot sits east of that so
/// housing can pass, and the search round it stays east of it. A plot characters.json names
/// elsewhere, as on a map of the shard's own, is searched round as it is: the engine's own
/// housing rules turn down a spot in a town.
/// </summary>
public static class HouseRules
{
    public const int ArchitectPrice = 43800;
    public const int DefaultPlotX = 1755;
    // Connected outdoor nav node east of Britain, north of TownRegion y 1498.
    public const int DefaultPlotY = 1474;
    public const int DefaultPlotZ = 0;
    public const int PlaceSearchRadius = 12;

    // The Britain graveyard's middle, as the destination list marks it.
    public const int GraveyardPlotX = 1384;
    public const int GraveyardPlotY = 1497;
    public const int GraveyardPlotZ = 10;

    /// <summary>A house keeps this far from the graveyard's middle: outside its yard.</summary>
    public const int GraveyardClearTiles = 12;

    public const int GraveyardReachTiles = 24;

    /// <summary>A house standing this far past a plot's reach still takes the plot.</summary>
    public const int PlotTakenMargin = 8;
    public const int PlaceSearchStep = 2;
    public const int BritainTownMaxX = 1740;
    public const int PlaceArrivalTiles = 16;
    public const int NoHouseSerial = 0;

    /// <summary>An owner goes back once its sign reads this worn: a quarter of the way to collapse.</summary>
    public const DecayLevel VisitFromLevel = DecayLevel.Somewhat;

    /// <summary>The last level a visit still saves; past it the house falls.</summary>
    public const DecayLevel LastSavedLevel = DecayLevel.IDOC;

    public static bool CanBuy(int gold, int houseGold, bool alreadyHasHouse) =>
        !alreadyHasHouse && houseGold > 0 && gold >= houseGold;

    /// <summary>
    /// A house the owner must refresh by hand has worn far enough to want a visit. An ageless or
    /// self-refreshing house needs none, and no visit refreshes a condemned one.
    /// </summary>
    public static bool VisitDue(DecayType type, DecayLevel level) =>
        type == DecayType.ManualRefresh && level is >= VisitFromLevel and <= LastSavedLevel;

    public static Point3D DefaultPlot() => new(DefaultPlotX, DefaultPlotY, DefaultPlotZ);

    public static bool OutsideTown(int x) => x > BritainTownMaxX;

    public static bool NearPlot(Point3D here, Point3D plot, int tiles) =>
        NavMetric.Chebyshev(here, plot) <= tiles;

    public static string BoughtLine(string house, string place) =>
        string.IsNullOrWhiteSpace(place)
            ? $"I bought {house}."
            : $"I placed {house} at {place}.";

    /// <summary>What an owner keeps in mind when it finds its house gone.</summary>
    public const string FallenLine = "My house fell down while I was away.";

    public static bool Stands(int houseSerial, bool atFeet, bool serialLive) =>
        houseSerial > NoHouseSerial && (atFeet || serialLive);

    public static BaseHouse BySerial(int houseSerial) =>
        houseSerial > NoHouseSerial ? World.FindItem((Serial)(uint)houseSerial) as BaseHouse : null;

    public static Point3D PlacementCenter(Point3D click, Point3D offset) =>
        new(click.X - offset.X, click.Y - offset.Y, click.Z - offset.Z);

    /// <summary>
    /// The spots to try for a house on a plot: where the buyer stands, the plot's middle, then a
    /// grid out to <paramref name="reach"/>; each at least <paramref name="clear"/> tiles from the
    /// middle and, on the default plot, east of Britain town.
    /// </summary>
    public static IEnumerable<Point3D> CandidateSpots(Point3D here, Point3D plot, int clear, int reach)
    {
        var eastOfBritain = plot == DefaultPlot();

        bool Fits(Point3D spot) =>
            (!eastOfBritain || OutsideTown(spot.X)) && NavMetric.Chebyshev(spot, plot) is var tiles &&
            tiles >= clear && tiles <= reach;

        if (Fits(here))
        {
            yield return here;
        }

        if (here != plot && Fits(plot))
        {
            yield return plot;
        }

        for (var dx = -reach; dx <= reach; dx += PlaceSearchStep)
        {
            for (var dy = -reach; dy <= reach; dy += PlaceSearchStep)
            {
                var spot = new Point3D(plot.X + dx, plot.Y + dy, plot.Z);

                if ((dx != 0 || dy != 0) && Fits(spot))
                {
                    yield return spot;
                }
            }
        }
    }

    /// <summary>A house standing this close to a plot's middle takes the plot.</summary>
    public static bool TakesPlot(Point3D house, Point3D plot, int reach) =>
        NavMetric.Chebyshev(house, plot) <= reach + PlotTakenMargin;

    /// <summary>A vendor plot is for a crafter; any other plot for anyone.</summary>
    public static bool MayUsePlot(bool vendorPlot, bool crafter) => !vendorPlot || crafter;
}
