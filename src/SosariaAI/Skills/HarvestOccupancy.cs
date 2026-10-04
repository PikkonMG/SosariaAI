using Server;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

/// <summary>
/// One worker per resource tile. NearFirst always returns the same nearest vein, so
/// without this every miner in a patch stands in a ring around one rock. A worker the
/// miner cannot see, a hidden GM on the rock, does not hold it.
/// </summary>
public static class HarvestOccupancy
{
    public const int WorkerReach = 1;

    public static bool HasOccupant(Mobile self, Point3D resource)
    {
        var map = self?.Map;

        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var mobile in map.GetMobilesInRange(resource, WorkerReach))
        {
            if (mobile != self && mobile is { Deleted: false, Alive: true } && People.Perceives(self, mobile))
            {
                return true;
            }
        }

        return false;
    }
}
