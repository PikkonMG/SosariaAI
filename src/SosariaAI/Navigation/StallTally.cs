using System;
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
/// tiles. A thousand stall lines hid that three doorways made most of them. A one-tile step
/// that keeps stalling walkers is closed to the live walker for a while, so routes go round
/// it: three diagonal steps inside Covetous and Deceit stalled walkers 200 times in one
/// evening while the planner kept routing over them. World thread only.
/// </summary>
public static class StallTally
{
    public const int TopTiles = 5;

    /// <summary>A one-tile step that stalled walkers this many times is closed to the live walker.</summary>
    public const int BlockAfterStalls = 3;

    /// <summary>How long a closed step stays closed before walkers try it again.</summary>
    public static readonly TimeSpan BlockSpan = TimeSpan.FromMinutes(30);

    private static readonly Dictionary<(Map Map, int FromX, int FromY, int ToX, int ToY), (int Stalls, DateTime Until)> Steps = new();

    private static readonly ILogger logger = SosariaLog.For(typeof(StallTally));
    private static readonly Dictionary<(Point3D At, Point3D Leg), int> Counts = new();
    private static int _minutes;

    public static void Initialize() => ActivityPulse.MinutePassed += OnMinute;

    /// <summary>
    /// One stall: the walker stood at <paramref name="at"/> on <paramref name="map"/> and could
    /// not take the leg to <paramref name="leg"/>. A one-tile leg that stalls
    /// <see cref="BlockAfterStalls"/> times is closed for <see cref="BlockSpan"/>.
    /// </summary>
    public static void Note(Map map, Point3D at, Point3D leg, DateTime now)
    {
        Counts[(at, leg)] = Counts.GetValueOrDefault((at, leg)) + 1;

        if (map == null || !IsStep(at, leg))
        {
            return;
        }

        var key = (map, at.X, at.Y, leg.X, leg.Y);
        var stalls = Steps.TryGetValue(key, out var held) && !Expired(held.Until, now) ? held.Stalls + 1 : 1;
        Steps[key] = (stalls, ShouldBlock(stalls) ? now + BlockSpan : default);
    }

    /// <summary>True while the step from one tile to the next is closed on <paramref name="map"/>.</summary>
    public static bool Blocks(Map map, int fromX, int fromY, int toX, int toY, DateTime now) =>
        Steps.TryGetValue((map, fromX, fromY, toX, toY), out var held) && held.Until != default && !Expired(held.Until, now);

    /// <summary>A leg of one tile, the step a walker takes; a longer leg is a road the planner breaks up.</summary>
    public static bool IsStep(Point3D at, Point3D leg) => NavMetric.Chebyshev(at, leg) == 1;

    public static bool ShouldBlock(int stalls) => stalls >= BlockAfterStalls;

    private static bool Expired(DateTime until, DateTime now) => until != default && now >= until;

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
        var now = Core.Now;
        var open = new List<(Map, int, int, int, int)>();

        foreach (var (key, held) in Steps)
        {
            if (Expired(held.Until, now))
            {
                open.Add(key);
            }
        }

        foreach (var key in open)
        {
            Steps.Remove(key);
        }
    }
}
