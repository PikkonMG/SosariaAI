using System.Collections.Generic;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

public sealed class LumberjackSkill : HarvestSkill
{
    private const string LogsNoun = "logs";

    public LumberjackSkill(Rectangle2D area, double fillFraction) : base(area, fillFraction)
    {
    }

    public override string Name => SkillKinds.Lumberjack;

    protected override string HaulNoun => LogsNoun;

    protected override HarvestSystem System => Lumberjacking.System;

    protected override HarvestDefinition Definition => Lumberjacking.System.Definitions[0];

    protected override Item FindTool(SosariaCharacter character) => FindHeldTool<Hatchet>(character);

    protected override int CountResource(Container pack) => CountInPack<Log>(pack);

    protected override bool TryResourceAt(
        Map map,
        Point2D tile,
        Point3D from,
        HashSet<Point2D> excluded,
        out HarvestSpot spot
    )
    {
        spot = default;

        if (map == null || map == Map.Internal || excluded.Contains(tile))
        {
            return false;
        }

        var treeIds = TreeTileIds();

        foreach (var found in map.Tiles.GetStaticTiles(tile.X, tile.Y))
        {
            if (!treeIds.Contains(found.ID))
            {
                continue;
            }

            var loc = new Point3D(tile.X, tile.Y, found.Z);

            if (!SpotHasResources(map, new HarvestSpot(loc, loc, found.ID, isLand: false)) ||
                !TryStandBeside(map, loc, from, out var standAt))
            {
                continue;
            }

            spot = new HarvestSpot(standAt, loc, found.ID, isLand: false);
            return true;
        }

        return false;
    }

    private static HashSet<int> _treeTileIds;

    private static HashSet<int> TreeTileIds()
    {
        if (_treeTileIds != null)
        {
            return _treeTileIds;
        }

        var tiles = new HashSet<int>();

        foreach (var id in Lumberjacking.System.Definitions[0].StaticTiles)
        {
            tiles.Add(id);
        }

        _treeTileIds = tiles;
        return tiles;
    }
}
