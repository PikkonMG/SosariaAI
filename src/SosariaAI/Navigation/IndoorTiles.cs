using Server;
using Server.Regions;

namespace SosariaAI.Navigation;

/// <summary>
/// A tile is inside a town building when a roof sits above it. Dungeon ceilings
/// are not buildings: those nodes must stay on ordinary routes.
/// </summary>
public static class IndoorTiles
{
    public static bool IsBuilding(Map map, int x, int y, int z)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        try
        {
            var region = Region.Find(new Point3D(x, y, z), map);

            if (region?.IsPartOf<DungeonRegion>() == true)
            {
                return false;
            }

            foreach (var tile in map.Tiles.GetStaticTiles(x, y))
            {
                if (tile.Z <= z)
                {
                    continue;
                }

                var id = tile.ID & TileData.MaxItemValue;

                if ((uint)id < (uint)TileData.ItemTable.Length && TileData.ItemTable[id].Roof)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
