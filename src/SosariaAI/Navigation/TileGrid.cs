using System;

namespace SosariaAI.Navigation;

/// <summary>
/// The tile grid every walker moves on: the eight neighbouring tiles.
/// </summary>
public static class TileGrid
{
    private static readonly (int X, int Y)[] Offsets =
    [
        (-1, -1), (0, -1), (1, -1),
        (-1, 0), (1, 0),
        (-1, 1), (0, 1), (1, 1)
    ];

    /// <summary>Offsets of the eight tiles around a tile.</summary>
    public static ReadOnlySpan<(int X, int Y)> Neighbours => Offsets;
}
