using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;
using SosariaAI.Configuration;
using SosariaAI.Logging;

namespace SosariaAI.Navigation;

/// <summary>
/// Counts walk stalls by the tile the walker stood on and the leg it could not take, and
/// every <see cref="CensusText.CensusMinutes"/> minutes writes one activity line naming the worst
/// tiles. A thousand stall lines hid that three doorways made most of them. World thread only.
/// </summary>
public static class StallTally
{
    public const int TopTiles = 5;

    private static readonly ILogger logger = SosariaLog.For(typeof(StallTally));
    private static readonly Dictionary<(Point3D At, Point3D Leg), int> Counts = new();
    private static int _minutes;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    /// <summary>One stall: the walker stood at <paramref name="at"/> and could not take the leg to <paramref name="leg"/>.</summary>
    public static void Note(Point3D at, Point3D leg) =>
        Counts[(at, leg)] = Counts.GetValueOrDefault((at, leg)) + 1;

    /// <summary>
    /// The summary line for a window's counts: the total and the <see cref="TopTiles"/>
    /// worst stall spots, most first. Null when nothing stalled.
    /// </summary>
    public static string Line(IReadOnlyDictionary<(Point3D At, Point3D Leg), int> counts, int minutes)
    {
        if (counts == null || counts.Count == 0)
        {
            return null;
        }

        var worst = counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key.At.X)
            .ThenBy(pair => pair.Key.At.Y)
            .Take(TopTiles)
            .Select(pair => $"{pair.Value} at {pair.Key.At} on leg {pair.Key.Leg}");

        return $"Walk stalls in the last {minutes} minutes: {counts.Values.Sum()}; worst: {string.Join("; ", worst)}";
    }

    private static void OnMinute()
    {
        if (!CensusText.Due(++_minutes))
        {
            return;
        }

        if (SosariaSettings.LogActivity && Line(Counts, CensusText.CensusMinutes) is { } line)
        {
            logger.Information(line);
        }

        Counts.Clear();
    }
}
