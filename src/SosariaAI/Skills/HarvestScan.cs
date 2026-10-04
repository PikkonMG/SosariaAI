using System;
using System.Collections.Generic;
using Server;
using SosariaAI.Navigation;
using SosariaAI.Spawning;

namespace SosariaAI.Skills;

/// <summary>
/// Walk tiles from the worker outward. A full 180x180 scan on the game thread
/// stalls the shard. One work patch is scanned whole: a scan of 400 tiles from the
/// patch's walk tile missed a third of it.
/// </summary>
public static class HarvestScan
{
    public const int MaxTiles = WorkSites.PatchSize * WorkSites.PatchSize;

    public static List<Point2D> NearFirst(Point3D from, Rectangle2D area, int maxTiles = MaxTiles)
    {
        var tiles = new List<Point2D>();

        if (area.Width <= 0 || area.Height <= 0 || maxTiles <= 0)
        {
            return tiles;
        }

        var minX = area.X;
        var minY = area.Y;
        var maxX = area.X + area.Width - 1;
        var maxY = area.Y + area.Height - 1;
        var startX = Math.Clamp(from.X, minX, maxX);
        var startY = Math.Clamp(from.Y, minY, maxY);
        var maxRadius = Math.Max(maxX - minX, maxY - minY);

        TryAdd(tiles, startX, startY, minX, minY, maxX, maxY, maxTiles);

        for (var radius = 1; radius <= maxRadius && tiles.Count < maxTiles; radius++)
        {
            for (var dx = -radius; dx <= radius && tiles.Count < maxTiles; dx++)
            {
                TryAdd(tiles, startX + dx, startY - radius, minX, minY, maxX, maxY, maxTiles);
                TryAdd(tiles, startX + dx, startY + radius, minX, minY, maxX, maxY, maxTiles);
            }

            for (var dy = -radius + 1; dy <= radius - 1 && tiles.Count < maxTiles; dy++)
            {
                TryAdd(tiles, startX - radius, startY + dy, minX, minY, maxX, maxY, maxTiles);
                TryAdd(tiles, startX + radius, startY + dy, minX, minY, maxX, maxY, maxTiles);
            }
        }

        return tiles;
    }

    /// <summary>
    /// The tile of <paramref name="area"/> nearest <paramref name="from"/> with a floor near
    /// its height, at that floor's height. A hashed walk tile of a mine patch can sit in the
    /// rock, with no tile to stand on within a walk's reach. World thread only.
    /// </summary>
    public static bool TryNearestFloor(Map map, Point3D from, Rectangle2D area, out Point3D floor)
    {
        var tiles = NearFirst(from, area);

        for (var i = 0; i < tiles.Count; i++)
        {
            if (Standable.TryFind(map, tiles[i].X, tiles[i].Y, from.Z, out var z))
            {
                floor = new Point3D(tiles[i].X, tiles[i].Y, z);
                return true;
            }
        }

        floor = from;
        return false;
    }

    private static void TryAdd(
        List<Point2D> tiles,
        int x,
        int y,
        int minX,
        int minY,
        int maxX,
        int maxY,
        int maxTiles
    )
    {
        if (tiles.Count >= maxTiles || x < minX || x > maxX || y < minY || y > maxY)
        {
            return;
        }

        tiles.Add(new Point2D(x, y));
    }
}
