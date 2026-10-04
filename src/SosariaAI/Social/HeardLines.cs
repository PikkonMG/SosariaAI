using System;
using System.Collections.Generic;

namespace SosariaAI.Social;

/// <summary>
/// Lines said aloud in the last few minutes, by map and <see cref="HeardLineRules.CellSize"/>
/// cell. A character asks before an idle line whether someone in earshot just said it.
/// Game thread only.
/// </summary>
public sealed class HeardLines
{
    /// <summary>Records between sweeps of cells that went quiet.</summary>
    public const int SweepEvery = 512;

    private readonly Dictionary<(int Map, int CellX, int CellY), List<Heard>> _cells = new();
    private int _recordsSinceSweep;

    public static HeardLines Shared { get; } = new();

    /// <summary>True when someone within earshot of (x, y) said these words in the window.</summary>
    public bool HeardNear(int map, int x, int y, string line, DateTime now)
    {
        var key = HeardLineRules.KeyOf(line);

        if (key.Length == 0)
        {
            return false;
        }

        var fromX = HeardLineRules.CellOf(x - HeardLineRules.EarshotTiles);
        var toX = HeardLineRules.CellOf(x + HeardLineRules.EarshotTiles);
        var fromY = HeardLineRules.CellOf(y - HeardLineRules.EarshotTiles);
        var toY = HeardLineRules.CellOf(y + HeardLineRules.EarshotTiles);

        for (var cellX = fromX; cellX <= toX; cellX++)
        {
            for (var cellY = fromY; cellY <= toY; cellY++)
            {
                if (!_cells.TryGetValue((map, cellX, cellY), out var heard))
                {
                    continue;
                }

                for (var i = 0; i < heard.Count; i++)
                {
                    var entry = heard[i];

                    if (HeardLineRules.IsFresh(entry.At, now) &&
                        HeardLineRules.InEarshot(x, y, entry.X, entry.Y) &&
                        string.Equals(entry.Key, key, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public void Record(int map, int x, int y, string line, DateTime now)
    {
        var key = HeardLineRules.KeyOf(line);

        if (key.Length == 0)
        {
            return;
        }

        var cell = (map, HeardLineRules.CellOf(x), HeardLineRules.CellOf(y));

        if (!_cells.TryGetValue(cell, out var heard))
        {
            heard = [];
            _cells[cell] = heard;
        }

        Prune(heard, now);

        if (heard.Count >= HeardLineRules.MaxLinesPerCell)
        {
            heard.RemoveAt(0);
        }

        heard.Add(new Heard(key, x, y, now));

        if (++_recordsSinceSweep >= SweepEvery)
        {
            Sweep(now);
        }
    }

    private void Sweep(DateTime now)
    {
        _recordsSinceSweep = 0;
        var quiet = new List<(int, int, int)>();

        foreach (var (cell, heard) in _cells)
        {
            Prune(heard, now);

            if (heard.Count == 0)
            {
                quiet.Add(cell);
            }
        }

        for (var i = 0; i < quiet.Count; i++)
        {
            _cells.Remove(quiet[i]);
        }
    }

    // Lines are added in time order, so the stale ones are at the front.
    private static void Prune(List<Heard> heard, DateTime now)
    {
        var stale = 0;

        while (stale < heard.Count && !HeardLineRules.IsFresh(heard[stale].At, now))
        {
            stale++;
        }

        if (stale > 0)
        {
            heard.RemoveRange(0, stale);
        }
    }

    private readonly record struct Heard(string Key, int X, int Y, DateTime At);
}
