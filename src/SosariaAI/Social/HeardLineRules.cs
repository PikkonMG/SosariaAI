using System;
using Server;
using SosariaAI.Deliberation;
using SosariaAI.Navigation;

namespace SosariaAI.Social;

/// <summary>
/// When a line counts as just heard. Lines are kept by map cell; a line said by anyone
/// within earshot in the last few minutes is a repeat. Two strangers who say the same
/// idle line on one street make the whole crowd sound like one script.
/// </summary>
public static class HeardLineRules
{
    public const int CellSize = 32;
    public const int EarshotTiles = 18;
    public const int WindowMinutes = 5;
    public const int MaxLinesPerCell = 48;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(WindowMinutes);

    /// <summary>The cell index of one coordinate. Floor division, so a negative coordinate stays in its own cell.</summary>
    public static int CellOf(int coordinate) => (int)Math.Floor((double)coordinate / CellSize);

    public static bool InEarshot(int x, int y, int otherX, int otherY) =>
        NavMetric.Chebyshev(new Point2D(x, y), new Point2D(otherX, otherY)) <= EarshotTiles;

    public static bool IsFresh(DateTime saidAt, DateTime now) => now - saidAt < Window;

    /// <summary>The form two lines share when they are the same words: case and punctuation do not count.</summary>
    public static string KeyOf(string line) => SpokenRepeat.Normalize(line);
}
