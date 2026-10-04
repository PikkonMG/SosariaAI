using System;
using System.Collections.Generic;
using Server;
using Server.Multis;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// When a character may buy the allowed small house, and when it goes back to look in on
/// it. Free. No model. Britain town ends at x 1740. The plot sits east of that so housing
/// can pass.
/// </summary>
public static class HouseRules
{
    public const int ArchitectPrice = 43800;
    public const int DefaultPlotX = 1755;
    // Connected outdoor nav node east of Britain, north of TownRegion y 1498.
    public const int DefaultPlotY = 1474;
    public const int DefaultPlotZ = 0;
    public const int PlaceSearchRadius = 12;
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

    public static string BoughtLine(string place) =>
        string.IsNullOrWhiteSpace(place)
            ? "I bought a small house."
            : $"I placed a small house at {place}.";

    /// <summary>What an owner keeps in mind when it finds its house gone.</summary>
    public const string FallenLine = "My house fell down while I was away.";

    public static bool Stands(int houseSerial, bool atFeet, bool serialLive) =>
        houseSerial > NoHouseSerial && (atFeet || serialLive);

    public static BaseHouse BySerial(int houseSerial) =>
        houseSerial > NoHouseSerial ? World.FindItem((Serial)(uint)houseSerial) as BaseHouse : null;

    public static Point3D PlacementCenter(Point3D click, Point3D offset) =>
        new(click.X - offset.X, click.Y - offset.Y, click.Z - offset.Z);

    public static IEnumerable<Point3D> CandidateSpots(Point3D here, Point3D plot)
    {
        if (OutsideTown(here.X))
        {
            yield return here;
        }

        if (OutsideTown(plot.X) && here != plot)
        {
            yield return plot;
        }

        for (var dx = -PlaceSearchRadius; dx <= PlaceSearchRadius; dx += PlaceSearchStep)
        {
            for (var dy = -PlaceSearchRadius; dy <= PlaceSearchRadius; dy += PlaceSearchStep)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                var spot = new Point3D(plot.X + dx, plot.Y + dy, plot.Z);

                if (!OutsideTown(spot.X))
                {
                    continue;
                }

                yield return spot;
            }
        }
    }
}
