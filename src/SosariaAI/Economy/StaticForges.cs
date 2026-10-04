using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;

namespace SosariaAI.Economy;

/// <summary>
/// Forges that are part of the map itself, not items placed on it. On this shard's data the
/// Magincia, Nujel'm, Skara Brae and Jhelom smithies have map-art forges that no item scan
/// finds. The map never changes while the shard runs, so each cell of it is read once and
/// kept. A cell is sixty-four static blocks, and a seller's first search for a forge to walk
/// to, 180 tiles round, wanted forty-nine cells at once, some three thousand reads on the
/// world thread: one call reads at most <see cref="UnreadCellsPerCall"/> unread cells,
/// nearest first, and the rest are read a slice at a time on a timer. Main thread.
/// </summary>
public static class StaticForges
{
    /// <summary>Tiles on a side of one cell the scan reads and keeps.</summary>
    public const int CellTiles = 64;

    /// <summary>The engine keeps statics in blocks of eight tiles a side.</summary>
    public const int BlockShift = 3;

    public const int BlockTiles = 1 << BlockShift;

    /// <summary>
    /// Unread cells one call, or one timer slice, reads: enough that the smelt check's reach
    /// round a forge is always read whole at once.
    /// </summary>
    public const int UnreadCellsPerCall = 4;

    /// <summary>The wait between two slices of cells left for the timer.</summary>
    public static readonly TimeSpan SliceGap = TimeSpan.FromMilliseconds(250);

    private static readonly Dictionary<(int Map, int CellX, int CellY), List<Point3D>> Cells = new();
    private static readonly Queue<(Map Map, int CellX, int CellY)> Unread = new();
    private static readonly HashSet<(int Map, int CellX, int CellY)> Queued = new();
    private static bool _slicing;

    /// <summary>
    /// Map-art forges within <paramref name="range"/> of <paramref name="from"/> in the cells
    /// read so far. Cells past this call's share are read by the timer, so a search again a
    /// moment later sees them.
    /// </summary>
    public static List<Point3D> Near(Map map, Point3D from, int range)
    {
        var found = new List<Point3D>();

        if (map == null || map == Map.Internal || range < 0)
        {
            return found;
        }

        var (firstX, lastX) = CellSpan(from.X, range, map.Width);
        var (firstY, lastY) = CellSpan(from.Y, range, map.Height);
        var unread = new List<(int CellX, int CellY)>();

        for (var cellX = firstX; cellX <= lastX; cellX++)
        {
            for (var cellY = firstY; cellY <= lastY; cellY++)
            {
                if (Cells.TryGetValue((map.MapID, cellX, cellY), out var forges))
                {
                    AddInRange(found, forges, from, range);
                }
                else
                {
                    unread.Add((cellX, cellY));
                }
            }
        }

        unread.Sort((a, b) => CellDistance(from, a.CellX, a.CellY).CompareTo(CellDistance(from, b.CellX, b.CellY)));

        for (var i = 0; i < unread.Count; i++)
        {
            if (i < UnreadCellsPerCall)
            {
                AddInRange(found, CellForges(map, unread[i].CellX, unread[i].CellY), from, range);
            }
            else
            {
                LeaveForTimer(map, unread[i].CellX, unread[i].CellY);
            }
        }

        return found;
    }

    /// <summary>Whole cells between the cell <paramref name="from"/> stands in and another. Pure.</summary>
    public static int CellDistance(Point3D from, int cellX, int cellY) =>
        Math.Max(Math.Abs(from.X / CellTiles - cellX), Math.Abs(from.Y / CellTiles - cellY));

    /// <summary>
    /// The first and last cell along one axis that tiles within <paramref name="range"/> of
    /// <paramref name="at"/> fall in, kept inside a map <paramref name="size"/> tiles across.
    /// Pure.
    /// </summary>
    public static (int First, int Last) CellSpan(int at, int range, int size)
    {
        var lastCell = (size - 1) / CellTiles;
        var first = (at - range) / CellTiles;
        var last = (at + range) / CellTiles;
        return (first < 0 ? 0 : first, last > lastCell ? lastCell : last);
    }

    private static void AddInRange(List<Point3D> found, List<Point3D> forges, Point3D from, int range)
    {
        for (var i = 0; i < forges.Count; i++)
        {
            if (NavMetric.Chebyshev(from, forges[i]) <= range)
            {
                found.Add(forges[i]);
            }
        }
    }

    private static void LeaveForTimer(Map map, int cellX, int cellY)
    {
        if (Queued.Add((map.MapID, cellX, cellY)))
        {
            Unread.Enqueue((map, cellX, cellY));
        }

        if (!_slicing)
        {
            _slicing = true;
            Timer.StartTimer(SliceGap, ReadSlice);
        }
    }

    private static void ReadSlice()
    {
        for (var read = 0; read < UnreadCellsPerCall && Unread.TryDequeue(out var cell); read++)
        {
            Queued.Remove((cell.Map.MapID, cell.CellX, cell.CellY));
            CellForges(cell.Map, cell.CellX, cell.CellY);
        }

        _slicing = Unread.Count > 0;

        if (_slicing)
        {
            Timer.StartTimer(SliceGap, ReadSlice);
        }
    }

    private static List<Point3D> CellForges(Map map, int cellX, int cellY)
    {
        var key = (map.MapID, cellX, cellY);

        if (!Cells.TryGetValue(key, out var forges))
        {
            forges = Scan(map, cellX, cellY);
            Cells[key] = forges;
        }

        return forges;
    }

    private static List<Point3D> Scan(Map map, int cellX, int cellY)
    {
        var forges = new List<Point3D>();
        var firstBlockX = cellX * CellTiles >> BlockShift;
        var firstBlockY = cellY * CellTiles >> BlockShift;
        var blocksPerCell = CellTiles >> BlockShift;

        for (var blockX = firstBlockX; blockX < firstBlockX + blocksPerCell; blockX++)
        {
            for (var blockY = firstBlockY; blockY < firstBlockY + blocksPerCell; blockY++)
            {
                var block = map.Tiles.GetStaticBlock(blockX, blockY);

                for (var x = 0; x < BlockTiles && x < block.Length; x++)
                {
                    for (var y = 0; y < BlockTiles && y < block[x].Length; y++)
                    {
                        foreach (var tile in block[x][y] ?? [])
                        {
                            if (SmeltRules.IsForgeId(tile.ID))
                            {
                                forges.Add(new Point3D((blockX << BlockShift) + x, (blockY << BlockShift) + y, tile.Z));
                            }
                        }
                    }
                }
            }
        }

        return forges;
    }
}
