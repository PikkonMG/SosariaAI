using System.Collections.Generic;
using Server;
using Server.Items;
using SosariaAI.Economy;
using SosariaAI.Mobiles;
using SosariaAI.Navigation;

namespace SosariaAI.Skills;

/// <summary>
/// How a loaded gatherer unloads its haul, the way a 1999 player did: a miner whose Mining
/// gives the smelt a chance smelts its iron ore to ingots standing at a forge, then sells or
/// banks the ingots; ore it cannot smelt, and any haul its trip to the forge and the counters
/// could not unload, goes into the bank box. Tilda Hallow mined 27 ore in thirty seconds, banked
/// nothing ("deposited 0 items": the box left the ore for the smith), failed "no walk to the
/// forge" three times, and carried the ore all night. World thread only.
/// </summary>
public static class SmeltOrBank
{
    /// <summary>
    /// True when the worker's Mining gives the engine's smelt a chance at iron ore. The engine
    /// rolls Mining against the ore's difficulty less and plus a band (Ore.cs smelt target), and
    /// a skill at or below the low end never succeeds (SkillCheck.CheckTarget): every try burns
    /// half the pile. Pure.
    /// </summary>
    public static bool MaySmelt(double mining) => mining > SmeltRules.MinSkill(SmeltRules.IronDifficulty);

    /// <summary>
    /// True when a piece of the plain harvest a shop buys goes into the bank box all the same:
    /// the worker's trip found no forge or counter for it (<paramref name="noOutlet"/>), or it is
    /// ore the worker's Mining cannot smelt. Pure.
    /// </summary>
    public static bool BanksHaul(bool sellableHarvest, bool isOre, bool noOutlet, double mining) =>
        sellableHarvest && (noOutlet || isOre && !MaySmelt(mining));

    /// <summary>
    /// The tile <paramref name="smelter"/> stands on to smelt at <paramref name="forge"/>: one
    /// that stands within the smelt reach of the forge (<see cref="SmeltRules.ForgeRange"/>),
    /// nearest the smelter (<see cref="NearestStand"/>). The walk to the forge itself ended up to
    /// a tile past its reach, a tile route's slack, and the next walk to it was refused:
    /// "no walk to the forge" ended 147 sale trips of one night, the Skara Brae seller three
    /// tiles from the forge it went to.
    /// </summary>
    public static bool TryForgeStand(Map map, Point3D forge, Point3D smelter, out Point3D stand)
    {
        var walker = Standable.Walker(map);
        var standing = new List<Point3D>();

        for (var dx = -SmeltRules.ForgeRange; dx <= SmeltRules.ForgeRange; dx++)
        {
            for (var dy = -SmeltRules.ForgeRange; dy <= SmeltRules.ForgeRange; dy++)
            {
                if (walker.FloorNear(forge.X + dx, forge.Y + dy, forge.Z) is { } floor)
                {
                    standing.Add(new Point3D(forge.X + dx, forge.Y + dy, floor));
                }
            }
        }

        return NearestStand(standing, forge, smelter, out stand);
    }

    /// <summary>
    /// The tile of <paramref name="standing"/> nearest <paramref name="smelter"/>, on the side it
    /// comes from, the nearer the forge on a tie; never the forge's own tile. Pure.
    /// </summary>
    public static bool NearestStand(IReadOnlyList<Point3D> standing, Point3D forge, Point3D smelter, out Point3D stand)
    {
        stand = default;
        var found = false;

        for (var i = 0; i < standing.Count; i++)
        {
            var tile = standing[i];

            if (tile.X == forge.X && tile.Y == forge.Y)
            {
                continue;
            }

            if (found && !Nearer(tile, stand, forge, smelter))
            {
                continue;
            }

            stand = tile;
            found = true;
        }

        return found;
    }

    private static bool Nearer(Point3D tile, Point3D best, Point3D forge, Point3D smelter)
    {
        var fromSmelter = NavMetric.Chebyshev(smelter, tile).CompareTo(NavMetric.Chebyshev(smelter, best));
        return fromSmelter < 0 || fromSmelter == 0 && NavMetric.Chebyshev(forge, tile) < NavMetric.Chebyshev(forge, best);
    }

    /// <summary>
    /// True when the box takes this piece of <paramref name="worker"/>'s haul though a shop in
    /// reach buys it (<see cref="HarvestPatches.HasNoOutlet"/>).
    /// </summary>
    public static bool BanksHaul(SosariaCharacter worker, Item item) =>
        BanksHaul(
            HarvestPack.IsSellable(item),
            item is BaseOre,
            HarvestPatches.HasNoOutlet(worker.Serial.Value, Core.Now),
            worker.Skills[SkillName.Mining].Value
        );
}
