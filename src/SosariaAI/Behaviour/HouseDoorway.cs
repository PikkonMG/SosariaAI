using Server;
using Server.Items;
using Server.Multis;
using SosariaAI.Navigation;

namespace SosariaAI.Behaviour;

/// <summary>
/// The way into a house: the engine house, its front door, and the standing tiles before and
/// behind that door. An owner goes into its home this way, and its chest is found or set
/// down here.
/// </summary>
public sealed record HouseDoorway(BaseHouse House, BaseDoor Door, Point3D Front, Point3D Inside)
{
    /// <summary>A house's chest stands this close to the house's middle.</summary>
    public const int FootprintTiles = 8;

    /// <summary>The door and the tiles before and behind it; null for a house with no door or no floor at it.</summary>
    public static HouseDoorway Of(BaseHouse house)
    {
        if (house is not { Deleted: false } || house.Map is not { } map || map == Map.Internal ||
            house.Doors is not { Count: > 0 } doors || doors[0] is not { Deleted: false } door)
        {
            return null;
        }

        // Read from where the door stands shut: an open leaf stands a tile off its doorway.
        var shut = DoorTiles.ShutSpot(door);
        var walker = Standable.Walker(map);
        var front = Standing(walker, new Point3D(shut.X, shut.Y + 1, house.Z));
        var inside = Standing(walker, new Point3D(shut.X, shut.Y - 1, shut.Z));
        return front is { } before && inside is { } behind ? new HouseDoorway(house, door, before, behind) : null;
    }

    public bool Stands => !House.Deleted && !Door.Deleted;

    /// <summary>Opens the door as a player's double-click does: the engine opens it for a key in the pack.</summary>
    public void Open(Mobile by)
    {
        if (!Door.Open)
        {
            Door.Use(by);
        }
    }

    public void Shut(Mobile by)
    {
        if (Door.Open)
        {
            Door.Use(by);
        }
    }

    /// <summary>
    /// Gives the owner a copy of its house key when it has none, as an owner had a spare cut.
    /// The engine's door opens only for a key in the pack.
    /// </summary>
    public void HandKey(Mobile to)
    {
        if (to.Backpack is not { } pack || Key.ContainsKey(pack, Door.KeyValue))
        {
            return;
        }

        to.AddToBackpack(new Key(KeyType.Gold, Door.KeyValue) { LootType = LootType.Newbied });
    }

    /// <summary>The locked-down chest inside the house, or null.</summary>
    public MetalChest Chest()
    {
        foreach (var chest in House.Map.GetItemsInRange<MetalChest>(House.Location, FootprintTiles))
        {
            if (chest is { Deleted: false } && House.IsInside(chest))
            {
                return chest;
            }
        }

        return null;
    }

    /// <summary>The chest, or a new one set down in the middle of the floor and locked down by the owner.</summary>
    public MetalChest ChestOrNew(Mobile owner)
    {
        if (Chest() is { } chest)
        {
            return chest;
        }

        var middle = Standing(Standable.Walker(House.Map), new Point3D(House.X, House.Y, Inside.Z));
        var made = new MetalChest();
        made.MoveToWorld(middle ?? Inside, House.Map);

        if (!House.LockDown(owner, made))
        {
            made.Movable = false;
        }

        return made;
    }

    public static Point3D? Standing(TileWalker walker, Point3D at) =>
        walker.FloorNear(at.X, at.Y, at.Z) is { } floor ? new Point3D(at.X, at.Y, floor) : null;
}
