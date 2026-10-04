using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Regions;
using SosariaAI.Logging;

namespace SosariaAI.Behaviour;

/// <summary>
/// The Felucca public moongates as they played in 1999: most had no guards, so a red took
/// them and the Yew gate was a PK spot, while the Britain, Moonglow, and Jhelom gates stood
/// under guards. The engine data puts every Felucca pad in one guarded region named "Moongates".
/// At boot the plugin turns that region's guards off, then lays a guarded area over each pad
/// named in characters.json feluccaGuardedMoongates. Every guard check (paths, gate travel,
/// recall landings, hot spots) then reads each pad as the era had it. Town guards are not
/// touched.
/// </summary>
public static class MoongateGuards
{
    public const string MoongateRegionName = "Moongates";
    public const string FeluccaName = "Felucca";

    /// <summary>Above the shared moongate region, so a named pad's own area wins the region lookup.</summary>
    public const int GuardedPadPriority = 51;

    /// <summary>The gates the era kept under guards.</summary>
    public static readonly string[] DefaultGuardedGates = ["Britain", "Moonglow", "Jhelom"];

    // The town each Felucca pad serves, by the engine's own gate names (PMList.Felucca).
    private static readonly IReadOnlyDictionary<int, string> GateTowns = new Dictionary<int, string>
    {
        [1012003] = "Moonglow",
        [1012004] = "Britain",
        [1012005] = "Jhelom",
        [1012006] = "Yew",
        [1012007] = "Minoc",
        [1012008] = "Trinsic",
        [1012009] = "Skara Brae",
        [1012010] = "Magincia"
    };

    private static readonly ILogger console = SosariaLog.Console(typeof(MoongateGuards));

    /// <summary>True when this region is the Felucca moongate region whose shared guards come off at boot.</summary>
    public static bool IsSharedGateRegion(string mapName, string regionName) =>
        string.Equals(mapName, FeluccaName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(regionName, MoongateRegionName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the named town's gate keeps its guards.</summary>
    public static bool KeepsGuards(string town, IReadOnlyCollection<string> guardedGates)
    {
        if (string.IsNullOrWhiteSpace(town) || guardedGates == null)
        {
            return false;
        }

        foreach (var gate in guardedGates)
        {
            if (string.Equals(gate?.Trim(), town, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The town a Felucca gate serves, or null for a gate the table does not name.</summary>
    public static string TownOf(int gateNumber) => GateTowns.TryGetValue(gateNumber, out var town) ? town : null;

    /// <summary>Applies the setting to the loaded regions and says so on the console.</summary>
    public static void Apply(IReadOnlyCollection<string> guardedGates)
    {
        GuardedRegion shared = null;

        foreach (var region in Region.Regions)
        {
            if (region is GuardedRegion guarded && IsSharedGateRegion(region.Map?.Name, region.Name))
            {
                guarded.GuardsDisabled = true;
                shared = guarded;
            }
        }

        if (shared == null)
        {
            return;
        }

        var kept = new List<string>();

        foreach (var entry in PMList.Felucca.Entries)
        {
            var town = TownOf(entry.Number);

            if (!KeepsGuards(town, guardedGates) || PadArea(shared, entry.Location) is not { } area)
            {
                continue;
            }

            new GuardedRegion($"{town} {MoongateRegionName}", shared.Map, GuardedPadPriority, area).Register();
            kept.Add(town);
        }

        console.Information(
            "Felucca moongates have no guards except {Kept}: reds may use the others",
            kept.Count == 0 ? "none" : string.Join(", ", kept)
        );
    }

    private static Rectangle3D? PadArea(Region shared, Point3D pad)
    {
        foreach (var area in shared.Area)
        {
            if (area.Contains(pad))
            {
                return area;
            }
        }

        return null;
    }
}
