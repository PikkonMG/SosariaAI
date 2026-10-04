using System.Text;
using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Navigation;

/// <summary>
/// What the walkers see at one tile: the land, every static with the flags that matter,
/// and the answers of the stand checks. For the tile-explain staff command, run while
/// standing on a bridge or a doorstep that a route refuses.
/// </summary>
public static class TileExplain
{
    public static string Describe(Map map, int x, int y, int z)
    {
        var text = new StringBuilder();

        if (map == null || map == Map.Internal)
        {
            return "No map.";
        }

        var land = map.Tiles.GetLandTile(x, y);
        var landData = TileData.LandTable[land.ID & TileData.MaxLandValue];
        text.AppendLine($"Tile ({x}, {y}) on {map.Name}, asked at z {z}");
        text.AppendLine($"Land {land.ID} z {land.Z} average {map.GetAverageZ(x, y)} {(landData.Flags.HasFlag(TileFlag.Impassable) ? "impassable" : "passable")}{(landData.Flags.HasFlag(TileFlag.Wet) ? " wet" : "")}");

        foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
        {
            var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
            text.AppendLine($"Static {tile.ID} z {tile.Z} height {data.CalcHeight}{(data.Surface ? " surface" : "")}{(data.Impassable ? " impassable" : "")}{(data.Roof ? " roof" : "")}{(data.Bridge ? " bridge" : "")}{(data.Wet ? " wet" : "")}");
        }

        foreach (var item in map.GetItemsAt(x, y))
        {
            var data = item.ItemData;
            text.AppendLine($"Item {item.GetType().Name} {item.ItemID} z {item.Z}{(data.Surface ? " surface" : "")}{(data.Impassable ? " impassable" : "")}{(item.Movable ? " movable" : "")}");
        }

        text.AppendLine(Standable.TryFindGround(map, x, y, out var ground)
            ? $"Ground surface: z {ground}"
            : "Ground surface: none within span of the land average");
        text.AppendLine(Standable.TryFind(map, x, y, z, out var near)
            ? $"Surface near z {z}: z {near}"
            : $"Surface near z {z}: none");
        text.AppendLine($"Walker's floor near z {z}: {(Standable.Walker(map).FloorNear(x, y, z) is { } floor ? $"z {floor}" : "none")}, indoor: {(IndoorTiles.IsBuilding(map, x, y, z) ? "yes" : "no")}, fits a person at z {z}: {(map.CanFit(x, y, z, PersonBody.Height, false, false) ? "yes" : "no")}");
        return text.ToString();
    }
}
