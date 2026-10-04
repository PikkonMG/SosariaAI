using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Logging;
using Server.Multis;
using SosariaAI.Logging;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// Small boats need a water land tile whose Z matches the place point.
/// Britain dock land is Z 0; the water next to it is about Z -2.
/// Search uses MultiData plus the same water/pier rules as BaseBoat.CanFit.
/// </summary>
public static class BoatRules
{
    private static readonly ILogger logger = SosariaLog.For(typeof(BoatRules));

    public const int SearchRadius = 20;
    public const int MinOpenWater = 0;

    /// <summary>
    /// Open water south of Britain east docks, away from pier posts.
    /// </summary>
    public static readonly Point3D BritainOpenWater = new(1508, 1790, DeepWaterZ);

    public const int WaterLandMin = 168;
    public const int WaterLandMax = 171;
    public const int WaterLandAltMin = 310;
    public const int WaterLandAltMax = 311;
    public const int StaticWaterMin = 0x1796;
    public const int StaticWaterMax = 0x17B2;
    public const int DeepWaterZ = -5;
    public const int HarborWaterZ = -2;
    public const int ShoreZ = 0;
    public const int PlaceZCapacity = 5;
    public const int SmallBoatNorthId = 0x0;
    public const int SmallBoatEastId = 0x1;
    public const int SmallBoatSouthId = 0x2;
    public const int SmallBoatWestId = 0x3;
    public static readonly int[] SmallBoatItemIds =
    [
        SmallBoatNorthId,
        SmallBoatEastId,
        SmallBoatSouthId,
        SmallBoatWestId
    ];

    public const int NoBoatSerial = 0;

    /// <summary>How far from a plank its user may stand (Plank.OnDoubleClick).</summary>
    public const int PlankUseRange = 8;

    public static bool IsWaterLand(int landId) =>
        landId is >= WaterLandMin and <= WaterLandMax or >= WaterLandAltMin and <= WaterLandAltMax;

    public static BaseBoat OwnedBoat(int boatSerial)
    {
        if (boatSerial <= NoBoatSerial)
        {
            return null;
        }

        return World.FindItem((Serial)(uint)boatSerial) is BaseBoat { Deleted: false } boat
            ? boat
            : null;
    }

    /// <summary>
    /// The plank of <paramref name="boat"/> nearer <paramref name="from"/>, or null for a boat
    /// with no plank left.
    /// </summary>
    public static Plank NearerPlank(BaseBoat boat, Point3D from)
    {
        var port = boat?.PPlank is { Deleted: false } p ? p : null;
        var starboard = boat?.SPlank is { Deleted: false } s ? s : null;

        if (port == null || starboard == null)
        {
            return port ?? starboard;
        }

        return NavMetric.Chebyshev(from, starboard.Location) < NavMetric.Chebyshev(from, port.Location) ? starboard : port;
    }

    public static bool IsWaterStatic(int itemId) =>
        itemId is >= StaticWaterMin and <= StaticWaterMax;

    public static int FillPlaceZs(int waterZ, int landZ, Span<int> dest)
    {
        var n = 0;
        n = AddPlaceZ(dest, n, waterZ);
        n = AddPlaceZ(dest, n, landZ);
        n = AddPlaceZ(dest, n, DeepWaterZ);
        n = AddPlaceZ(dest, n, HarborWaterZ);
        n = AddPlaceZ(dest, n, ShoreZ);
        return n;
    }

    public static bool TryFindFit(Map map, Point3D near, out Point3D fit)
    {
        fit = Point3D.Zero;

        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var land = map.Tiles.GetLandTile(near.X, near.Y);
        logger.Information(
            "Boat search near {Near} land {LandId} z {LandZ} valid {Valid}",
            near,
            land.ID,
            land.Z,
            BaseBoat.IsValidLocation(near, map)
        );

        var facings = new MultiComponentList[SmallBoatItemIds.Length];
        var any = false;

        for (var i = 0; i < SmallBoatItemIds.Length; i++)
        {
            var parts = MultiData.GetComponents(SmallBoatItemIds[i]);
            facings[i] = parts;

            if (parts.Width > 0 && parts.Height > 0)
            {
                any = true;
            }
        }

        var waterCells = 0;
        var found = Point3D.Zero;

        if (any && SearchArea(
            near,
            (x, y) =>
            {
                if (IsWaterCell(map, x, y))
                {
                    waterCells++;
                }

                return TryCell(map, x, y, facings, out found);
            }
        ))
        {
            fit = found;
            logger.Information("Boat fits at {Fit}", fit);
            return true;
        }

        logger.Warning(
            "Boat found no fit near {Near} land {LandId} z {LandZ}; water cells {Water}",
            near,
            land.ID,
            land.Z,
            waterCells
        );
        return false;
    }

    private static bool TryCell(Map map, int x, int y, IReadOnlyList<MultiComponentList> facings, out Point3D fit)
    {
        fit = Point3D.Zero;
        var land = map.Tiles.GetLandTile(x, y);
        var waterZ = land.Z;
        var water = IsWaterLand(land.ID);

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            if (IsWaterStatic(tile.ID))
            {
                water = true;
                waterZ = tile.Z;
                break;
            }
        }

        if (!water)
        {
            return false;
        }

        Span<int> heights = stackalloc int[PlaceZCapacity];
        var heightCount = FillPlaceZs(waterZ, land.Z, heights);

        for (var i = 0; i < heightCount; i++)
        {
            var at = new Point3D(x, y, heights[i]);

            if (!BaseBoat.IsValidLocation(at, map))
            {
                continue;
            }

            for (var facing = 0; facing < facings.Count; facing++)
            {
                if (CanFit(map, at, facings[facing]))
                {
                    fit = at;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Tries cells in rings round <paramref name="near"/>, nearest ring first, out to
    /// <see cref="SearchRadius"/>. True at the first cell <paramref name="tryCell"/> takes.
    /// </summary>
    internal static bool SearchArea(Point3D near, Func<int, int, bool> tryCell)
    {
        for (var radius = MinOpenWater; radius <= SearchRadius; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    if (tryCell(near.X + dx, near.Y + dy))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool CanFit(Map map, Point3D p, MultiComponentList parts) =>
        FootprintFits(
            p,
            parts,
            (tx, ty) =>
            {
                var land = map.Tiles.GetLandTile(tx, ty);
                return CellFits(map, land.ID, land.Z, p.Z, tx, ty);
            }
        ) && !ItemsBlock(map, p, parts);

    /// <summary>True when every filled cell of the boat's <paramref name="parts"/> placed at <paramref name="p"/> fits.</summary>
    internal static bool FootprintFits(Point3D p, MultiComponentList parts, Func<int, int, bool> cellFits)
    {
        if (parts == null || parts.Width <= 0 || parts.Height <= 0 || cellFits == null)
        {
            return false;
        }

        for (var x = 0; x < parts.Width; x++)
        {
            for (var y = 0; y < parts.Height; y++)
            {
                if (parts.Tiles[x][y].Length == 0)
                {
                    continue;
                }

                if (!cellFits(p.X + parts.Min.X + x, p.Y + parts.Min.Y + y))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool CellFits(Map map, int landId, int landZ, int placeZ, int x, int y)
    {
        var hasWater = LandWaterAt(landId, landZ, placeZ);

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            if (!ConsiderStatic(tile.ID, tile.Z, placeZ, ref hasWater))
            {
                return false;
            }
        }

        return hasWater;
    }

    /// <summary>True when water land lies at the boat's place height.</summary>
    internal static bool LandWaterAt(int landId, int landZ, int placeZ) => landZ == placeZ && IsWaterLand(landId);

    /// <summary>
    /// False when the static stands at or above the place height and is no water: a pier post.
    /// Water at the place height sets <paramref name="hasWater"/>.
    /// </summary>
    internal static bool ConsiderStatic(int id, int z, int placeZ, ref bool hasWater)
    {
        if (z == placeZ && IsWaterStatic(id))
        {
            hasWater = true;
            return true;
        }

        return z < placeZ || IsWaterStatic(id);
    }

    private static bool IsWaterCell(Map map, int x, int y)
    {
        var land = map.Tiles.GetLandTile(x, y);

        if (IsWaterLand(land.ID))
        {
            return true;
        }

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            if (IsWaterStatic(tile.ID))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ItemsBlock(Map map, Point3D p, MultiComponentList parts)
    {
        var bounds = new Rectangle2D(
            p.X + parts.Min.X,
            p.Y + parts.Min.Y,
            parts.Width,
            parts.Height
        );

        foreach (var item in map.GetItemsInBounds(bounds))
        {
            if (item is BaseMulti || item.ItemID > TileData.MaxItemValue || item.Z < p.Z || !item.Visible)
            {
                continue;
            }

            var x = item.X - p.X + parts.Min.X;
            var y = item.Y - p.Y + parts.Min.Y;

            if (x < 0 || x >= parts.Width || y < 0 || y >= parts.Height || parts.Tiles[x][y].Length != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int AddPlaceZ(Span<int> dest, int n, int z)
    {
        if (n >= dest.Length)
        {
            return n;
        }

        for (var i = 0; i < n; i++)
        {
            if (dest[i] == z)
            {
                return n;
            }
        }

        dest[n] = z;
        return n + 1;
    }
}
