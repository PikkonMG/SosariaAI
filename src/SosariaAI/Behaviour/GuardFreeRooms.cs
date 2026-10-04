using System.Collections.Generic;
using Server;
using Server.Logging;
using Server.Regions;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

/// <summary>
/// The rooms of a town with no guards have none either, as Buccaneer's Den played in 1999.
/// The engine data gives the Den's inn rooms a region of their own under the Den, and a
/// region's guards are on unless its own entry turns them off. A red's tavern trip to the
/// Den inn at (2680,2238) was refused as a walk "into the guards", and a red that walked
/// in anyway met them. At boot the plugin turns off the guards of every guarded region
/// whose nearest guarded parent has none.
/// </summary>
public static class GuardFreeRooms
{
    private static readonly ILogger console = SosariaLog.Console(typeof(GuardFreeRooms));

    /// <summary>True when a room keeps guards its guard-free town does not have.</summary>
    public static bool TurnsOff(bool ownGuardsOff, bool townGuardsOff) => !ownGuardsOff && townGuardsOff;

    /// <summary>Turns the guards off in the rooms of guard-free towns among <paramref name="regions"/>; returns how many.</summary>
    public static int Apply(IEnumerable<Region> regions)
    {
        var turnedOff = 0;

        foreach (var region in regions)
        {
            if (region is GuardedRegion room && TurnsOff(room.GuardsDisabled, TownGuardsOff(room)))
            {
                room.GuardsDisabled = true;
                turnedOff++;
            }
        }

        return turnedOff;
    }

    /// <summary>Applies the rule to the loaded regions and says so on the console.</summary>
    public static void Apply()
    {
        var turnedOff = Apply(Region.Regions);

        if (turnedOff > 0)
        {
            console.Information("{Count} rooms of guard-free towns have no guards", turnedOff);
        }
    }

    /// <summary>The nearest guarded region above the room has its guards off, itself or through its own town.</summary>
    private static bool TownGuardsOff(GuardedRegion room) =>
        room.Parent?.GetRegion<GuardedRegion>() is { } town && (town.GuardsDisabled || TownGuardsOff(town));
}
