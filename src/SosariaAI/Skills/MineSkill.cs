using System.Collections.Generic;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using SosariaAI.Configuration;
using SosariaAI.Mobiles;

namespace SosariaAI.Skills;

public sealed class MineSkill : HarvestSkill
{
    private const string OreNoun = "ore";

    public MineSkill(Rectangle2D area, double fillFraction) : base(area, fillFraction)
    {
    }

    public override string Name => SkillKinds.Mine;

    protected override string HaulNoun => OreNoun;

    protected override int AreaWidenings => HarvestArea.MineWidenings;

    protected override HarvestSystem System => Mining.System;

    protected override HarvestDefinition Definition => Mining.System.OreAndStone;

    protected override Item FindTool(SosariaCharacter character) => FindHeldTool<Pickaxe>(character);

    protected override int CountResource(Container pack) => CountInPack<BaseOre>(pack);

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

        var land = map.Tiles.GetLandTile(tile.X, tile.Y);
        var tileId = land.ID & TileData.MaxLandValue;

        if (!Definition.Validate(tileId, isLand: true))
        {
            return false;
        }

        var loc = new Point3D(tile.X, tile.Y, land.Z);

        if (!SpotHasResources(map, new HarvestSpot(loc, loc, tileId, isLand: true)) ||
            !TryStandBeside(map, loc, from, out var standAt))
        {
            return false;
        }

        spot = new HarvestSpot(standAt, loc, tileId, isLand: true);
        return true;
    }
}
