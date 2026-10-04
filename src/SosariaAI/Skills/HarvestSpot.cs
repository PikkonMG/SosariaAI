using Server;

namespace SosariaAI.Skills;

public readonly struct HarvestSpot
{
    public HarvestSpot(Point3D standAt, Point3D resource, int tileId, bool isLand)
    {
        StandAt = standAt;
        Resource = resource;
        TileId = tileId;
        IsLand = isLand;
    }

    public Point3D StandAt { get; }

    public Point3D Resource { get; }

    public int TileId { get; }

    public bool IsLand { get; }
}
